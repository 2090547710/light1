using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 新增光照系统管理器
public class LightingManager : MonoBehaviour
{
    private static LightingManager instance;
    
    public static QuadTree tree { get; set; }
    public static List<Lighting> activeLights = new List<Lighting>();
    
    // 新增数据提供者引用
    [SerializeField] private LightingDataProvider dataProvider;
    
    void Awake() => instance = this;

    void Start()
    {
        
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
        
        // 移除误差参数传递
        instance.dataProvider.UpdateLightingData(activeLights);     
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
