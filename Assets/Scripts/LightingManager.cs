using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 新增光照系统管理器
public class LightingManager : MonoBehaviour
{
    public static LightingManager instance;
    
    public static QuadTree tree { get; set; }
    public static List<Lighting> activeLights = new List<Lighting>();
    
    // 新增数据提供者引用
       
    void Awake() => instance = this;

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

    public static void UpdateLighting(){
        tree.ResetIllumination();
        
        // 添加空列表保护
        if(activeLights.Count > 0)
        {
            foreach (var light in activeLights)
            {
                light.ApplyLighting();
            }
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
