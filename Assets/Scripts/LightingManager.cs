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
        LightRadiusVariance = tree.MinNodeSize.x*0.7f;//根号二除以二
        DarkRadiusVariance = tree.MinNodeSize.x / 4f;
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
        // 保持原有的四叉树光照逻辑
        foreach (var light in activeLights)
        {
            light.ApplyLighting();
        }
        
        // 调用数据提供者更新光照数据
        instance.dataProvider.UpdateLightingData(
            activeLights, 
            LightRadiusVariance, 
            DarkRadiusVariance
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

}
