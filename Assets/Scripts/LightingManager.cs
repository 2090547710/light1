using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 新增光照系统管理器
public class LightingManager : MonoBehaviour
{
    private static LightingManager instance;
    
    // 根据四叉树叶子节点尺寸自动计算误差范围
    private static float LightRadiusVariance { get; set;} // 圆形光照范围误差
    private static float DarkRadiusVariance { get; set;} // 方形暗区范围误差

    
    public static  int LightCount = Shader.PropertyToID("_LightCount");
    public static  int LightPositions = Shader.PropertyToID("_LightPositions");
    public static  int LightRadii = Shader.PropertyToID("_LightRadii");
    
    public static QuadTree tree { get; set; }
    public static List<Lighting> activeLights = new List<Lighting>();
    
    // 新增数据提供者引用
    [SerializeField] private LightingDataProvider dataProvider;
    
    void Awake() {
        instance = this;
    }

    void Start()
    {
        // 计算四叉树叶子节点的实际尺寸
        float leafNodeSize = tree.RootSize.x / Mathf.Pow(2, tree.MaxDepth);
        
        // 圆形光照范围误差设为叶子节点对角线的一半（覆盖整个节点）
        LightRadiusVariance = leafNodeSize * 0.7071f / 2f; // 0.7071 ≈ 1/√2
        
        // 方形暗区误差设为叶子节点边长的一半（精确匹配网格）
        DarkRadiusVariance = leafNodeSize / 2f;
    }

    void LateUpdate()
    {
         if(Input.GetKeyDown(KeyCode.Space)){
            UpdateLighting();
        }
    }

    

    public static void RegisterLight(Lighting light)
    {
        if (!activeLights.Contains(light))
        {
            activeLights.Add(light);
        }
    }

    public static void UpdateLighting(){
        tree.ResetIllumination();
        
        // 调用数据提供者更新光照数据
        instance.dataProvider.UpdateLightingData(
            activeLights, 
            LightRadiusVariance, 
            DarkRadiusVariance
        );

        // 保持原有的四叉树光照逻辑
        foreach (var light in activeLights)
        {
            light.ApplyLighting();
        }
    }
    
    public static void UnregisterLight(Lighting light)
    {
        activeLights.Remove(light);
    }

    void OnDrawGizmos()
    {
        tree?.DrawGizmos();
    }

}
