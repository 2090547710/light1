using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// 新增光照系统管理器
public class LightingManager : MonoBehaviour
{
    public static LightingManager instance;
    
    // 新增Compute Shader相关字段
    public ComputeShader lightingComputeShader;
    private static int kernelObstacle;
    private static int kernelNormal;
    private static RenderTexture compositeRT;
    
    // 新增合成高度图相关字段
    public static int compositeSize = 4096;
    public static Texture2D compositeHeightmap;
    
    public static QuadTree tree { get; set; }
    public static List<Lighting> activeLights = new List<Lighting>();

    // 新增参数更新方法
    public static Vector4 _heightmapParams;

    // 添加调试用浮点参数
    [SerializeField]
    private static float _TestFloat = 2.0f;
    public static float TestFloat => _TestFloat;

    void Awake()
    {
        instance = this;
        
        // 初始化时设置默认值
        Shader.SetGlobalFloat("_TestFloat", _TestFloat);
        
        // 初始化Compute Shader
        if (lightingComputeShader != null)
        {
            kernelObstacle = lightingComputeShader.FindKernel("CSObstacleLight");
            kernelNormal = lightingComputeShader.FindKernel("CSNormalLight");
            
            // 创建RenderTexture作为GPU处理目标
            compositeRT = new RenderTexture(compositeSize, compositeSize, 0, RenderTextureFormat.ARGBFloat);
            compositeRT.enableRandomWrite = true;
            compositeRT.Create();
        }
        
        // 初始化合成高度图
        compositeHeightmap = new Texture2D(
            compositeSize, 
            compositeSize, 
            TextureFormat.R8, 
            false
        );
        compositeHeightmap.wrapMode = TextureWrapMode.Clamp;
        compositeHeightmap.filterMode = FilterMode.Point;
        ClearComposite();

    }

    void Start()
    {
        
    }

    void LateUpdate()
    {

    }

    public static void RegisterLight(Lighting light)
    {
        if (!activeLights.Contains(light))
        {
            activeLights.Add(light);
        }
    }

    // 更新全体光照
    public static void UpdateLighting()
    {
        UpdateLightingMarkers();
        UpdateCompositeHeightmap();
    }
    
    public static void UnregisterLight(Lighting light)
    {
        activeLights.Remove(light);
    }

    void OnDrawGizmos()
    {
        tree?.DrawGizmos();
    }

    public static void ClearComposite()
    {
        if (instance.lightingComputeShader != null && compositeRT != null)
        {
            // GPU清空
            RenderTexture rt = RenderTexture.active;
            RenderTexture.active = compositeRT;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = rt;
        }

    }

    // 新增保存方法
    public static void SaveCompositeToFile()
    {
        // 从GPU/CPU获取最新数据
        if (instance.lightingComputeShader != null && compositeRT != null)
        {
            RenderTexture.active = compositeRT;
            compositeHeightmap.ReadPixels(new Rect(0, 0, compositeSize, compositeSize), 0, 0);
            compositeHeightmap.Apply();
            RenderTexture.active = null;
        }
        // 直接从纹理获取像素数据
        Color[] heightmapPixels = compositeHeightmap.GetPixels();
        
        Texture2D saveTex = new Texture2D(
            compositeSize, 
            compositeSize, 
            TextureFormat.RGBA32, 
            false
        );
        
        // 转换单通道到RGBA格式
        for(int i = 0; i < heightmapPixels.Length; i++)
        {
            float r = heightmapPixels[i].r;  // 使用实际纹理数据
            saveTex.SetPixel(
                i % compositeSize, 
                i / compositeSize, 
                new Color(r, r, r, 1.0f)     // 添加Alpha通道
            );
        }
        
        byte[] bytes = saveTex.EncodeToPNG();
        string filename = $"composite_{System.DateTime.Now:yyyyMMddHHmmss}.png";
        
        // 修改保存路径到Assets/HeightMap
        string folderPath = "Assets/HeightMap";
        // 确保目录存在
        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }
        
        string path = System.IO.Path.Combine(folderPath, filename);
        
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log($"合成高度图已保存至：{path}");
        
#if UNITY_EDITOR
        // 刷新资源数据库，使Unity能够识别新文件
        UnityEditor.AssetDatabase.Refresh();
#endif
        
        Destroy(saveTex);
    }

#if UNITY_EDITOR
[UnityEditor.MenuItem("Tools/Save Composite Heightmap")]
static void SaveCompositeMenuItem()
{
    LightingManager.SaveCompositeToFile();
}
#endif

    // 修改参数更新方法
    public static void UpdateHeightmapParams(Vector2 center, Vector2 size)
    {
        _heightmapParams = new Vector4(center.x, center.y, size.x, size.y);
        // 直接使用GPU中的RenderTexture
        Shader.SetGlobalVector("_HeightmapParams", _heightmapParams);
        Shader.SetGlobalTexture("_CompositeMap", compositeRT);
    }

    // 新增GPU处理方法
    public static void ProcessLightingGPU(Lighting light, Bounds lightBounds, Texture2D heightMap, float lightHeight)
    {
        if (instance.lightingComputeShader == null || compositeRT == null || heightMap == null)
            return;
        
        float centerHeight = tree.GetNodeHeightAtPosition(new Vector3(lightBounds.center.x, 0, lightBounds.center.z));

        // 计算根节点范围
        var rootSize = tree.RootSize;
        var rootCenter = tree.RootCenter;
        Bounds rootBounds = new Bounds(
            new Vector3(rootCenter.x, 0, rootCenter.y),
            new Vector3(rootSize.x, 0, rootSize.y)
        );
        
        // 计算当前光源的UV范围, 使用世界空间进行归一化计算
        Vector3 min = lightBounds.center - lightBounds.extents;
        Vector3 max = lightBounds.center + lightBounds.extents;
        
        float uvMinX = Mathf.InverseLerp(rootBounds.min.x, rootBounds.max.x, min.x);
        float uvMaxX = Mathf.InverseLerp(rootBounds.min.x, rootBounds.max.x, max.x);
        float uvMinY = Mathf.InverseLerp(rootBounds.min.z, rootBounds.max.z, min.z);
        float uvMaxY = Mathf.InverseLerp(rootBounds.min.z, rootBounds.max.z, max.z);
        
        Vector4 lightBoundsParam = new Vector4(uvMinX, uvMinY, uvMaxX, uvMaxY);
        Vector4 rootBoundsParam = new Vector4(rootCenter.x, rootCenter.y, rootSize.x, rootSize.y);
        
        // 根据光源是否为障碍物决定使用的kernel
        int kernel = light.isObstacle ? kernelObstacle : kernelNormal;
        instance.lightingComputeShader.SetTexture(kernel, "_HeightMap", heightMap);
        instance.lightingComputeShader.SetTexture(kernel, "_CompositeMap", compositeRT);
        instance.lightingComputeShader.SetVector("_LightBounds", lightBoundsParam);
        instance.lightingComputeShader.SetVector("_RootBounds", rootBoundsParam);
        instance.lightingComputeShader.SetFloat("_IsObstacle", light.isObstacle ? 1 : 0);
        instance.lightingComputeShader.SetFloat("_LightHeight", lightHeight + centerHeight);
        
        // ===== 计算合成区域（反向映射） =====
        // compositeRT为正方形，尺寸为 compositeSize
        int compSize = compositeSize;
        // 计算在合成图上对应光源UV区域的像素边界
        int compositeOffsetX = Mathf.FloorToInt(lightBoundsParam.x * (compSize - 1));
        int compositeOffsetY = Mathf.FloorToInt(lightBoundsParam.y * (compSize - 1));
        int compositeXEnd = Mathf.CeilToInt(lightBoundsParam.z * (compSize - 1));
        int compositeYEnd = Mathf.CeilToInt(lightBoundsParam.w * (compSize - 1));
        int regionWidth = compositeXEnd - compositeOffsetX + 1;
        int regionHeight = compositeYEnd - compositeOffsetY + 1;
        
        // 将计算好的区域参数传递给Compute Shader
        instance.lightingComputeShader.SetInts("_CompositeOffset", new int[] { compositeOffsetX, compositeOffsetY });
        instance.lightingComputeShader.SetInts("_CompositeRegionSize", new int[] { regionWidth, regionHeight });
        
        // 计算Dispatch所需的组数（每组16×16线程）
        int threadGroupsX = Mathf.CeilToInt(regionWidth / 16.0f);
        int threadGroupsY = Mathf.CeilToInt(regionHeight / 16.0f);
        
        // 派发计算
        instance.lightingComputeShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
    }

    void OnDestroy()
    {
        if (compositeRT != null)
        {
            compositeRT.Release();
            compositeRT = null;
        }
    }

    // 更新四叉树光照标记
    public static void UpdateLightingMarkers()
    {
        tree.ResetIllumination();
        
        if(activeLights.Count > 0)
        {
            // 优先处理障碍物光源标记
            foreach (var light in activeLights.Where(l => l.isObstacle))
            {
                light.ApplyLighting();
            }
            // 处理其他光源标记
            foreach (var light in activeLights.Where(l => !l.isObstacle))
            {
                light.ApplyLighting();
            }
        }
    }

    // 更新合成高度图
    public static void UpdateCompositeHeightmap()
    {
        ClearComposite();
        
        if(activeLights.Count > 0)
        {
            // 优先处理障碍物光源的高度图
            foreach (var light in activeLights.Where(l => l.isObstacle))
            {
                ProcessLightingGPU(
                    light, 
                    light.GetWorldBounds(), 
                    light.heightMap, 
                    light.lightHeight
                );
            }
            // 处理其他光源的高度图
            foreach (var light in activeLights.Where(l => !l.isObstacle))
            {
                ProcessLightingGPU(
                    light, 
                    light.GetWorldBounds(), 
                    light.heightMap, 
                    light.lightHeight
                );
            }
        }
        
        UpdateHeightmapParams(tree.RootCenter, tree.RootSize);
    }
}
