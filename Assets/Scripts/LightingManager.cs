using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 新增光照系统管理器
public class LightingManager : MonoBehaviour
{
    public static LightingManager instance;
    
    // 新增合成高度图相关字段
    public static int compositeSize = 1024;
    public static Color[] compositePixels;
    public static Texture2D compositeHeightmap;
    
    public static QuadTree tree { get; set; }
    public static List<Lighting> activeLights = new List<Lighting>();
    
    // 新增数据提供者引用
       
    void Awake()
    {
        instance = this;
        
        // 初始化合成高度图
        compositeHeightmap = new Texture2D(compositeSize, compositeSize, TextureFormat.R8, false);
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

}
