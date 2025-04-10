using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 新增光照系统管理器
public class LightingManager : MonoBehaviour
{
   
    #region 字段和属性
    // 单例实例
    public static LightingManager instance;

    // 新增Compute Shader相关字段
    public ComputeShader lightingComputeShader;
    private static int kernelObstacle;
    private static int kernelNormal;
    private static RenderTexture compositeRT;
    
    // 添加公共访问器属性
    public static RenderTexture CompositeRT => compositeRT;
    
    // 新增合成高度图相关字段
    public static int compositeSize = 4096;
    public static Texture2D compositeHeightmap;

    public static QuadTree tree { get; set; }
    public static List<Lighting> activeLights = new List<Lighting>();

    // 新增参数更新方法
    public static Vector4 _heightmapParams;

    // 边界线段可视化相关字段
    private static List<Vector4> simplifiedBoundarySegments = new List<Vector4>();
    private static bool showSimplifiedBoundary = false;
    public static float targetSegmentLength = 1.0f;
    
    // 新增用于储存显示的图片对象
    private static List<GameObject> displayedImages = new List<GameObject>();
    // 默认图片设置
    public static string defaultImageResource = "1";
    public static float defaultImageHeight = 2.0f;
    public static float defaultImageWidth = 0.0f;
    public static float defaultRotationAngle = 0.0f;
    public static bool autoUpdateBoundaryImages = true;

    // 添加缓存变量，存储上一次的边界线段
    private static List<Vector4> cachedSimplifiedBoundarySegments = new List<Vector4>();
    #endregion

    #region Unity生命周期方法
    void Awake()
    {
        instance = this;
               
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
        
        UpdateLighting();

    }

    void LateUpdate()
    {

    }
    
    void OnDrawGizmos()
    {
        tree?.DrawGizmos();

         if (showSimplifiedBoundary && simplifiedBoundarySegments != null && simplifiedBoundarySegments.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (var segment in simplifiedBoundarySegments)
            {
                Vector3 start = new Vector3(segment.x, 0.1f, segment.y);
                Vector3 end = new Vector3(segment.z, 0.1f, segment.w);
                Gizmos.DrawLine(start, end);
                
                Gizmos.DrawSphere(start, 0.2f);
                Gizmos.DrawSphere(end, 0.2f);
            }
        }
    }
    
    void OnDestroy()
    {
        if (compositeRT != null)
        {
            compositeRT.Release();
            compositeRT = null;
        }
    }
    #endregion

    #region 光源注册与管理
    public static void RegisterLight(Lighting light)
    {
        if (!activeLights.Contains(light))
        {
            activeLights.Add(light);
        }
    }
    
    public static void UnregisterLight(Lighting light)
    {
        activeLights.Remove(light);
    }
    #endregion

    #region 光照更新方法
    // 更新全体光照
    public static void UpdateLighting()
    {
        // 将所有光源标记为脏
        foreach (var light in activeLights)
        {
            light.MarkDirty();
        }
        
        // 使用增量更新方法
        UpdateDirtyLights();
    }
    
    // 添加只更新脏光源的方法
    public static void UpdateDirtyLights()
    {
        // 获取所有脏光源
        var dirtyLights = activeLights.Where(light => light.IsDirty).ToList();
        
        if(dirtyLights.Count == 0) {
            UpdateHeightmapParams(tree.RootCenter, tree.RootSize);
            return; // 没有脏光源需要更新
        }
        
        // 将脏光源分为障碍物和非障碍物两组
        var dirtyObstacleLights = dirtyLights.Where(light => light.isObstacle).ToList();
        var dirtyNormalLights = dirtyLights.Where(light => !light.isObstacle).ToList();

        // 创建待更新的光源列表（受影响的重叠光源）
        HashSet<Lighting> affectedLights = new HashSet<Lighting>();
        
        // ===== 第一步：基于障碍物脏光源收集重叠光源 =====
        foreach (var light in dirtyObstacleLights)
        {
            // 1.1 收集基于缓存数据的重叠光源
            light.UpdateOverlappingLights(true); // 使用缓存数据更新重叠关系
            var cachedOverlappingLights = light.OverlappingLights.Values.ToList();
            // 1.2 收集基于新数据的重叠光源
            light.UpdateOverlappingLights(false); // 使用当前数据更新重叠关系
            var newOverlappingLights = light.OverlappingLights.Values.ToList();
            // 1.3 合并两次收集的重叠光源（去除重复和已经是脏光源的）
            foreach (var overlappingLight in cachedOverlappingLights.Concat(newOverlappingLights))
            {
    
                if (!dirtyLights.Contains(overlappingLight))
                {
                    affectedLights.Add(overlappingLight);
                }
            }
        }
        
        // ===== 第二步：普通脏光源和受影响重叠光源移除光照 =====
        foreach (var light in dirtyNormalLights.Concat(affectedLights))
        {
            // 2.1 只有光照影响大于阈值才移除光照
            if (light.TotalBrightnessImpact >= 0.01f)
            {
                bool useCache = dirtyNormalLights.Contains(light); // 脏光源使用缓存数据，受影响光源使用当前数据
                // 获取是否为种子光源的状态
                bool isSeed = useCache ? light.GetCachedIsSeed() : light.isSeed;
                
                // 移除光照 - 种子光源移除时使用加法操作
                light.ApplyLighting(isSeed, useCache);
                // 同时更新合成高度图 - 种子光源移除时使用加法
                ProcessLightingGPU(
                    light, 
                    useCache ? light.GetCachedWorldBounds() : light.GetWorldBounds(), 
                    useCache ? light.GetCachedHeightMap() : light.heightMap, 
                    useCache ? light.GetCachedLightHeight() : light.lightHeight,
                    isSeed // isAdditive = isSeed，种子光源移除时执行加法操作
                );
            }
        }
        
        // ===== 第三步：障碍物脏光源的移除光照和更新光照 =====
        foreach (var light in dirtyObstacleLights)
        {
            // 3.1 只有光照影响大于阈值才移除光照
            if (light.TotalBrightnessImpact >= 0.01f)
            {
                // 移除光照（使用缓存数据）
                light.ApplyLighting(false, true);
                // 同时更新合成高度图
                ProcessLightingGPU(
                    light, 
                    light.GetCachedWorldBounds(), 
                    light.GetCachedHeightMap(), 
                    light.GetCachedLightHeight(),                  
                    false // isAdditive = false，执行减法操作
                );
            }
            
            // 3.2 更新光照（使用新数据）
            light.ApplyLighting(true, false);
            // 同时更新合成高度图
            ProcessLightingGPU(
                light, 
                light.GetWorldBounds(), 
                light.heightMap, 
                light.lightHeight,
                true // isAdditive = true，执行加法操作
            );
            
            // 重置脏标记
            light.ResetDirtyFlag();
        }
        
        // ===== 第四步：普通脏光源和受影响重叠光源更新光照并更新重叠关系 =====
        foreach (var light in dirtyNormalLights.Concat(affectedLights))
        {
            // 获取是否为种子光源状态
            bool isSeed = light.isSeed;
            
            // 4.1 更新光照 - 种子光源更新时使用减法操作
            light.ApplyLighting(!isSeed, false);
            // 同时更新合成高度图 - 种子光源更新时使用减法
            ProcessLightingGPU(
                light, 
                light.GetWorldBounds(), 
                light.heightMap, 
                light.lightHeight,
                !isSeed // isAdditive = !isSeed，种子光源更新时执行减法操作
            );
            
            // 4.2 更新重叠关系
            light.UpdateOverlappingLights(false);
            
            // 对于脏光源，重置脏标记
            if (dirtyNormalLights.Contains(light))
            {
                light.ResetDirtyFlag();
            }
        }

        // 更新GPU中的合成高度图参数
        UpdateHeightmapParams(tree.RootCenter, tree.RootSize);
    
        // 自动更新边界和显示图片
        if (autoUpdateBoundaryImages)
        {
            // 使用增量更新代替完全重建
            UpdateBoundaryImagesIncremental(
                defaultImageResource, 
                defaultImageHeight, 
                defaultImageWidth, 
                defaultRotationAngle, 
                targetSegmentLength
            );
        }
    }
    #endregion

    #region 合成高度图操作
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
    #endregion

    #region GPU处理方法
    // 修改参数更新方法
    public static void UpdateHeightmapParams(Vector2 center, Vector2 size)
    {
        _heightmapParams = new Vector4(center.x, center.y, size.x, size.y);
        // 直接使用GPU中的RenderTexture
        Shader.SetGlobalVector("_HeightmapParams", _heightmapParams);
        Shader.SetGlobalTexture("_CompositeMap", compositeRT);
    }

    // 新增GPU处理方法带加减法参数
    public static void ProcessLightingGPU(Lighting light, Bounds lightBounds, Texture2D heightMap, float lightHeight, bool isAdditive = true)
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
        
        // 修改UV范围计算
        float uvMinX = (min.x - rootBounds.min.x) / rootBounds.size.x;
        float uvMaxX = (max.x - rootBounds.min.x) / rootBounds.size.x;
        float uvMinY = (min.z - rootBounds.min.z) / rootBounds.size.z;
        float uvMaxY = (max.z - rootBounds.min.z) / rootBounds.size.z;
        
        // 保存原始UV值用于光照计算，不限制在[0,1]范围内
        Vector4 lightBoundsRawParam = new Vector4(uvMinX, uvMinY, uvMaxX, uvMaxY);
        
        // 现在再限制UV在[0,1]范围内，用于确定合成区域
        uvMinX = Mathf.Clamp01(uvMinX);
        uvMaxX = Mathf.Clamp01(uvMaxX);
        uvMinY = Mathf.Clamp01(uvMinY);
        uvMaxY = Mathf.Clamp01(uvMaxY);
        
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
        // 添加加减操作标记
        instance.lightingComputeShader.SetFloat("_IsAdditive", isAdditive ? 1 : 0);
        
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
        
        // 发送原始UV值到Compute Shader
        instance.lightingComputeShader.SetVector("_LightBoundsRaw", lightBoundsRawParam);
        
        // 派发计算
        instance.lightingComputeShader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
    }
    #endregion

    #region 边界线段可视化
    // 获取并显示简化边界线段
    public static void ShowSimplifiedBoundary(float segmentLength = 1.0f)
    {
        if (tree == null) return;
        
        targetSegmentLength = segmentLength;
        simplifiedBoundarySegments = tree.GetSimplifiedBoundarySegments(targetSegmentLength);
        showSimplifiedBoundary = true;
        
        Debug.Log($"已生成{simplifiedBoundarySegments.Count}条等长边界线段，目标长度: {targetSegmentLength}");
    }

    // 隐藏边界线段
    public static void HideSimplifiedBoundary()
    {
        showSimplifiedBoundary = false;
    }

    // 切换边界显示状态
    public static void ToggleSimplifiedBoundary(float segmentLength = 1.0f)
    {
        if (showSimplifiedBoundary)
        {
            HideSimplifiedBoundary();
        }
        else
        {
            ShowSimplifiedBoundary(segmentLength);
        }
    }

#if UNITY_EDITOR
[UnityEditor.MenuItem("Tools/显示简化边界线段 (1.0长度)")]
static void ShowSimplifiedBoundaryMenu()
{
    ShowSimplifiedBoundary(1.0f);
}

[UnityEditor.MenuItem("Tools/显示简化边界线段 (0.5长度)")]
static void ShowDenseSimplifiedBoundaryMenu()
{
    ShowSimplifiedBoundary(0.5f);
}

[UnityEditor.MenuItem("Tools/显示简化边界线段 (2.0长度)")]
static void ShowSparseSimplifiedBoundaryMenu()
{
    ShowSimplifiedBoundary(2.0f);
}

[UnityEditor.MenuItem("Tools/隐藏边界线段")]
static void HideSimplifiedBoundaryMenu()
{
    HideSimplifiedBoundary();
}
#endif
    #endregion

    // 修改方法：基于线段底边显示图片，并支持旋转
    public static GameObject DisplayImageOnSegment(Vector4 segment, Texture2D texture, float height = 1.0f, float width = 0.0f, bool maintainAspect = true, float rotationAngle = 0.0f)
    {
        if (texture == null)
        {
            Debug.LogError("无法显示图片：纹理为空");
            return null;
        }
        
        // 创建一个新的游戏对象作为图片容器
        GameObject imageObj = new GameObject("SegmentImage");
        
        // 创建一个Quad作为图片显示
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.SetParent(imageObj.transform);
        
        // 计算线段属性
        Vector3 startPoint = new Vector3(segment.x, 0, segment.y);
        Vector3 endPoint = new Vector3(segment.z, 0, segment.w);
        Vector3 midPoint = (startPoint + endPoint) * 0.5f;
        float segmentLength = Vector3.Distance(startPoint, endPoint);
        
        // 计算线段方向向量，用于旋转
        Vector3 segmentDirection = (endPoint - startPoint).normalized;
        Vector3 normal = new Vector3(-segmentDirection.z, 0, segmentDirection.x); // 垂直于线段的法向量
        
        // 计算宽度（如果未指定则基于纹理比例）
        if (width <= 0 && maintainAspect)
        {
            // 保持纹理比例
            float aspect = (float)texture.width / texture.height;
            width = height * aspect;
        }
        else if (width <= 0)
        {
            // 默认使用线段长度作为宽度
            width = segmentLength;
        }
        
        // 设置Quad的变换
        imageObj.transform.position = midPoint + new Vector3(0, height * 0.5f, 0);
        
        // 设置Quad的旋转，使其垂直于线段并面向法线方向
        Quaternion baseRotation = Quaternion.LookRotation(normal, Vector3.up);
        Quaternion addedRotation = Quaternion.Euler(0, rotationAngle, 0);
        imageObj.transform.rotation = baseRotation * addedRotation;
            
        // 设置Quad的缩放以匹配所需尺寸
        quad.transform.localScale = new Vector3(width, height, 1);
        quad.transform.localPosition = Vector3.zero;
        
        // 创建一个材质并分配纹理 - 修改为支持透明度的着色器
        Material material = new Material(Shader.Find("Unlit/Transparent"));
        material.mainTexture = texture;
        material.renderQueue = 3000; // 设置渲染队列为透明队列
        
        // 应用材质
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.material = material;
        
        return imageObj;
    }

    // 修改辅助方法：从资源加载纹理并显示，支持旋转角度
    public static GameObject DisplayImageFromResource(Vector4 segment, string resourcePath, float height = 1.0f, float width = 0.0f, float rotationAngle = 0.0f)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture == null)
        {
            Debug.LogError($"无法加载纹理：{resourcePath}");
            return null;
        }
        
        return DisplayImageOnSegment(segment, texture, height, width, false, rotationAngle);
    }


    // 添加新方法：按指定长度简化边界线段并返回结果
    public static List<Vector4> GetSimplifiedBoundaryWithLength(float segmentLength)
    {
        if (tree == null) return new List<Vector4>();
        
        targetSegmentLength = segmentLength;
        return tree.GetSimplifiedBoundarySegments(targetSegmentLength);
    }


    // 添加新方法：清除所有线段图片并清空列表
    public static void ClearAllSegmentImages()
    {
        // 销毁所有已创建的图片对象
        foreach (var img in displayedImages)
        {
            if (img != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(img);
                }
                else
                {
                    #if UNITY_EDITOR
                    DestroyImmediate(img);
                    #endif
                }
            }
        }
        
        // 清空列表
        displayedImages.Clear();
        
        // 查找场景中可能遗漏的图片对象并销毁
        GameObject[] segmentImages = GameObject.FindObjectsOfType<GameObject>().Where(go => go.name.StartsWith("SegmentImage")).ToArray();
        foreach (var img in segmentImages)
        {
            if (Application.isPlaying)
            {
                Destroy(img);
            }
            else
            {
                #if UNITY_EDITOR
                DestroyImmediate(img);
                #endif
            }
        }
        
        Debug.Log($"已清除所有线段图片");
    }

    // 添加新方法：使用差集对边界线段进行增量更新
    public static void UpdateBoundaryImagesIncremental(string resourcePath, float height = 1.0f, float width = 0.0f, float rotationAngle = 0.0f, float segmentLength = 1.0f)
    {
        if (tree == null) return;
        
        // 获取当前的边界线段
        List<Vector4> currentSegments = GetSimplifiedBoundaryWithLength(segmentLength);
        
        // 计算需要添加的新线段（当前线段中不在缓存中的线段）
        List<Vector4> segmentsToAdd = new List<Vector4>();
        foreach (var segment in currentSegments)
        {
            if (!cachedSimplifiedBoundarySegments.Any(s => 
                Mathf.Approximately(s.x, segment.x) && 
                Mathf.Approximately(s.y, segment.y) && 
                Mathf.Approximately(s.z, segment.z) && 
                Mathf.Approximately(s.w, segment.w)))
            {
                segmentsToAdd.Add(segment);
            }
        }
        
        // 计算需要移除的线段（缓存中不在当前线段的线段）
        List<Vector4> segmentsToRemove = new List<Vector4>();
        foreach (var segment in cachedSimplifiedBoundarySegments)
        {
            if (!currentSegments.Any(s => 
                Mathf.Approximately(s.x, segment.x) && 
                Mathf.Approximately(s.y, segment.y) && 
                Mathf.Approximately(s.z, segment.z) && 
                Mathf.Approximately(s.w, segment.w)))
            {
                segmentsToRemove.Add(segment);
            }
        }
        
        // 移除不再需要的图片
        List<GameObject> imagesToRemove = new List<GameObject>();
        foreach (var segment in segmentsToRemove)
        {
            // 找到对应这个线段的图片
            for (int i = 0; i < displayedImages.Count; i++)
            {
                GameObject img = displayedImages[i];
                if (img == null) continue;
                
                // 比较图片位置与线段中点位置来确定是否为该线段上的图片
                Vector3 segmentMidPoint = new Vector3(
                    (segment.x + segment.z) * 0.5f,
                    0,
                    (segment.y + segment.w) * 0.5f
                );
                
                Vector3 imgPosition = img.transform.position;
                // 只比较xz平面上的位置
                Vector3 imgPositionXZ = new Vector3(imgPosition.x, 0, imgPosition.z);
                
                if (Vector3.Distance(imgPositionXZ, segmentMidPoint) < 0.1f) // 使用小阈值判断
                {
                    imagesToRemove.Add(img);
                    displayedImages.RemoveAt(i);
                    i--; // 调整索引
                    break;
                }
            }
        }
        
        // 销毁移除的图片
        foreach (var img in imagesToRemove)
        {
            if (img != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(img);
                }
                else
                {
                    #if UNITY_EDITOR
                    DestroyImmediate(img);
                    #endif
                }
            }
        }
        
        // 为新增的线段添加图片
        if (segmentsToAdd.Count > 0)
        {
            // 从Resources加载纹理
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogError($"无法加载纹理：{resourcePath}");
                return;
            }
            
            int count = displayedImages.Count;
            foreach (var segment in segmentsToAdd)
            {
                GameObject imageObj = DisplayImageOnSegment(segment, texture, height, width, false, rotationAngle);
                if (imageObj != null)
                {
                    imageObj.name = $"SegmentImage_{count++}";
                    displayedImages.Add(imageObj);
                }
            }
        }
        
        // 更新缓存
        cachedSimplifiedBoundarySegments = new List<Vector4>(currentSegments);
        
        Debug.Log($"边界图片增量更新完成: 添加了 {segmentsToAdd.Count} 个, 移除了 {segmentsToRemove.Count} 个");
    }
}
