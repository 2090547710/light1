using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 新增光照系统管理器
public class LightingManager : MonoBehaviour
{
    public static LightingManager instance;
    
    // 新增合成高度图相关字段
    public static int compositeSize = 2048;
    public static Color[] compositePixels;
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
        
        // 初始化合成高度图
        compositeHeightmap = new Texture2D(
            compositeSize, 
            compositeSize, 
            TextureFormat.R8, 
            false
        );
        compositeHeightmap.wrapMode = TextureWrapMode.Clamp;
        compositeHeightmap.filterMode = FilterMode.Point;
        compositePixels = new Color[compositeSize * compositeSize];
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

    public static void UpdateLighting()
    {
        tree.ResetIllumination();
        ClearComposite();
        
        if(activeLights.Count > 0)
        {
            foreach (var light in activeLights)
            {
                light.ApplyLighting();
            }
        }
        
        compositeHeightmap.SetPixels(compositePixels);
        compositeHeightmap.Apply();
        // 调试时自动保存（可选）
        // SaveCompositeToFile(); 
        LightingManager.UpdateHeightmapParams(
            tree.RootCenter,
            tree.RootSize
        );
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
        for (int i = 0; i < compositePixels.Length; i++)
        {
            compositePixels[i] = Color.black;
        }
    }

    // 新增保存方法
    public static void SaveCompositeToFile()
    {
        // 创建临时纹理确保保存正确格式
        Texture2D saveTex = new Texture2D(
            compositeSize, 
            compositeSize, 
            TextureFormat.RGBA32, 
            false
        );
        
        // 转换R8单通道到RGBA格式
        for(int i = 0; i < compositePixels.Length; i++)
        {
            float r = compositePixels[i].r;
            saveTex.SetPixel(i % compositeSize, i / compositeSize, new Color(r, r, r));
        }
        
        byte[] bytes = saveTex.EncodeToPNG();
        string filename = $"composite_{System.DateTime.Now:yyyyMMddHHmmss}.png";
        string path = System.IO.Path.Combine(Application.persistentDataPath, filename);
        
        System.IO.File.WriteAllBytes(path, bytes);
        Debug.Log($"合成高度图已保存至：{path}");
        
        Destroy(saveTex);
    }

#if UNITY_EDITOR
[UnityEditor.MenuItem("Tools/Save Composite Heightmap")]
static void SaveCompositeMenuItem()
{
    LightingManager.SaveCompositeToFile();
}
#endif

    // 新增参数更新方法
    public static void UpdateHeightmapParams(Vector2 center, Vector2 size)
    {
        _heightmapParams = new Vector4(center.x, center.y, size.x, size.y);
        // 确保正确设置全局着色器参数
        Shader.SetGlobalTexture("_CompositeHeightmap", compositeHeightmap);
        Shader.SetGlobalVector("_HeightmapParams", _heightmapParams);
        // 添加调试输出确认值已正确设置
        Debug.Log($"设置高度图参数: 中心({_heightmapParams.x}, {_heightmapParams.y}), 尺寸({_heightmapParams.z}, {_heightmapParams.w})");
        Shader.SetGlobalFloat("_TestFloat", _TestFloat);
    }

    // 添加参数验证（仅在编辑器生效）
    void OnValidate()
    {
        if(tree != null)
        {
            UpdateHeightmapParams(tree.RootCenter, tree.RootSize);
            Shader.SetGlobalFloat("_TestFloat", _TestFloat);
        }
    }
}
