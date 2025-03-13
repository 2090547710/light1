using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;

public class TerrainGenerator : MonoBehaviour
{
    [Header("地形设置")]
    public Terrain targetTerrain;
    public float heightScale = 10.0f; // 高度缩放系数
    public bool smoothHeights = true;  // 是否平滑处理
    public int smoothIterations = 2;   // 平滑迭代次数
    [Tooltip("地形所在的层级")]
    public int terrainLayer = 0; // 默认为Default层
    
    [Header("材质设置")]
    public Material terrainMaterial; // 在Inspector中指定材质
    
    // 从CompositeMap获取高度数据
    public float[,] ExtractHeightDataFromComposite()
    {
        // 获取LightingManager中的CompositeRT
        RenderTexture compositeRT = LightingManager.CompositeRT;
        if (compositeRT == null)
        {
            Debug.LogError("合成图RenderTexture不存在！");
            return null;
        }
        
        int resolution = compositeRT.width;
        
        // 创建临时纹理读取数据
        RenderTexture.active = compositeRT;
        Texture2D tempTex = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false);
        tempTex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        tempTex.Apply();
        RenderTexture.active = null;
        
        // 提取G通道数据作为高度
        float[,] heights = new float[resolution, resolution];
        Color[] pixels = tempTex.GetPixels();
        
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // 使用G通道作为高度，并应用缩放系数
                heights[y, x] = pixels[y * resolution + x].g * heightScale;
                
                // 确保高度在0-1范围内
                heights[y, x] = Mathf.Clamp01(heights[y, x]);
            }
        }
        
        DestroyImmediate(tempTex);
        
        // 如果需要平滑处理
        if (smoothHeights && smoothIterations > 0)
        {
            heights = SmoothHeightmap(heights, smoothIterations);
        }
        
        return heights;
    }
    
    // 平滑高度图
    private float[,] SmoothHeightmap(float[,] heights, int iterations)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        float[,] result = heights.Clone() as float[,];
        
        for (int iter = 0; iter < iterations; iter++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    // 简单的3x3平均平滑
                    float sum = 0f;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            sum += result[y + dy, x + dx];
                        }
                    }
                    heights[y, x] = sum / 9f;
                }
            }
            
            // 交换缓冲区
            float[,] temp = result;
            result = heights;
            heights = temp;
        }
        
        return result;
    }
    
    // 应用高度到地形
    public void ApplyHeightToTerrain()
    {
        if (targetTerrain == null)
        {
            Debug.LogError("未指定目标地形！");
            return;
        }
        
        float[,] heights = ExtractHeightDataFromComposite();
        if (heights == null) return;
        
        TerrainData terrainData = targetTerrain.terrainData;
        int terrainResolution = terrainData.heightmapResolution;
        int sourceResolution = heights.GetLength(0);
        
        // 如果合成图和地形分辨率不同，需要重采样
        if (sourceResolution != terrainResolution)
        {
            float[,] resampledHeights = new float[terrainResolution, terrainResolution];
            
            // 简单的线性重采样
            for (int y = 0; y < terrainResolution; y++)
            {
                for (int x = 0; x < terrainResolution; x++)
                {
                    float u = (float)x / (terrainResolution - 1);
                    float v = (float)y / (terrainResolution - 1);
                    
                    int sourceX = Mathf.FloorToInt(u * (sourceResolution - 1));
                    int sourceY = Mathf.FloorToInt(v * (sourceResolution - 1));
                    
                    resampledHeights[y, x] = heights[sourceY, sourceX];
                }
            }
            
            heights = resampledHeights;
        }
        
        // 应用到地形
        terrainData.SetHeights(0, 0, heights);
        Debug.Log("已根据CompositeMap的G通道生成地形！");
    }

    // 添加到类成员变量区域
    private GameManager gameManager;

    // 添加Start方法
    void Start()
    {
        // 获取同一对象上的GameManager组件
        gameManager = GetComponent<GameManager>();
        
        if (gameManager != null)
        {
            // 自动创建地形
            CreateTerrainFromGameManager();
        }
        else
        {
            Debug.LogWarning("未找到GameManager组件，无法自动创建地形");
        }
    }

    // 根据GameManager数据创建地形
    public void CreateTerrainFromGameManager()
    {
        if (gameManager == null) return;
        
        // 如果没有目标地形，创建一个
        if (targetTerrain == null)
        {
            GameObject terrainObj = new GameObject("GeneratedTerrain");
            targetTerrain = terrainObj.AddComponent<Terrain>();
            terrainObj.AddComponent<TerrainCollider>();
        }
        
        // 创建TerrainData
        TerrainData terrainData = new TerrainData();
        
#if UNITY_EDITOR
        // 确保目录存在
        if (!System.IO.Directory.Exists("Assets/Terrains"))
        {
            System.IO.Directory.CreateDirectory("Assets/Terrains");
        }
        
        // 在编辑器中创建资产
        UnityEditor.AssetDatabase.CreateAsset(terrainData, "Assets/Terrains/GeneratedTerrainData.asset");
        UnityEditor.AssetDatabase.SaveAssets();
#endif
        
        // 设置地形分辨率
        int resolution = 513; // 2^n+1格式
        terrainData.heightmapResolution = resolution;
        
        // 使用GameManager的尺寸
        float width = gameManager.size.x;
        float length = gameManager.size.y;
        float height = width * 0.2f; // 高度设为宽度的20%
        
        terrainData.size = new Vector3(width, height, length);
        
        // 设置地形位置与四叉树中心对齐
        if (targetTerrain != null && targetTerrain.gameObject != null)
        {
            targetTerrain.transform.position = new Vector3(
                gameManager.center.x - width/2,
                0.5f,
                gameManager.center.y - length/2
            );
            // 直接设置层级
            targetTerrain.gameObject.layer = terrainLayer;
        }
        
        // 分配TerrainData
        targetTerrain.terrainData = terrainData;
        targetTerrain.GetComponent<TerrainCollider>().terrainData = terrainData;
        
        // 应用材质
        if (terrainMaterial != null)
        {
            targetTerrain.materialTemplate = terrainMaterial;
        }
        
        Debug.Log($"已根据GameManager创建地形 (宽:{width} 长:{length} 高:{height})");
    }

    // 添加一个单独的方法来设置材质
    public void ApplyTerrainMaterial()
    {
        if (targetTerrain == null)
        {
            Debug.LogError("未指定目标地形！");
            return;
        }
        
        if (terrainMaterial != null)
        {
            targetTerrain.materialTemplate = terrainMaterial;
            Debug.Log("已应用地形材质");
        }
        else
        {
            Debug.LogWarning("未指定地形材质");
        }
    }
}

#if UNITY_EDITOR
// 编辑器扩展部分
[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        TerrainGenerator generator = (TerrainGenerator)target;
        
        EditorGUILayout.Space();
        if (GUILayout.Button("生成地形"))
        {
            generator.ApplyHeightToTerrain();
        }
    }
}

// 菜单项扩展
public static class TerrainGeneratorMenu
{
    [MenuItem("Tools/地形/从CompositeMap生成地形")]
    static void GenerateTerrainFromComposite()
    {
        // 检查是否有场景中已存在的TerrainGenerator
        TerrainGenerator generator = GameObject.FindObjectOfType<TerrainGenerator>();
        
        // 如果没有，创建一个
        if (generator == null)
        {
            GameObject newGameObject = new GameObject("TerrainGenerator");
            generator = newGameObject.AddComponent<TerrainGenerator>();
            
            // 尝试自动查找场景中的第一个地形
            Terrain[] terrains = GameObject.FindObjectsOfType<Terrain>();
            if (terrains.Length > 0)
            {
                generator.targetTerrain = terrains[0];
            }
        }
        
        // 聚焦到场景中的TerrainGenerator对象
        Selection.activeGameObject = generator.gameObject;
        
        // 如果已设置地形，则直接生成
        if (generator.targetTerrain != null)
        {
            generator.ApplyHeightToTerrain();
        }
        else
        {
            EditorUtility.DisplayDialog("警告", "请先在TerrainGenerator组件中指定目标地形！", "确定");
        }
    }
}
#endif 