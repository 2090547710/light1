using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 新增光照系统管理器
public class LightingManager : MonoBehaviour
{
    [Header("光照设置")]
    public float randomRadiusVariance = 1f; // 可以通过Inspector调整误差范围

    [SerializeField] private List<Material> terrainMaterials = new List<Material>(); // 需要关联的地形材质列表
    private static readonly int LightCount = Shader.PropertyToID("_LightCount");
    private static readonly int LightPositions = Shader.PropertyToID("_LightPositions");
    private static readonly int LightRadii = Shader.PropertyToID("_LightRadii");
    
    public static QuadTree tree { get; set; }
    private static List<Lighting> activeLights = new List<Lighting>();
    
    // 新增临时数组存储光照数据
    private Vector4[] lightPositions = new Vector4[30];
    private float[] lightRadii = new float[30];
    
    void LateUpdate()
    {
        tree.ResetIllumination();
        
        // 收集光照数据
        int lightCount = Mathf.Min(activeLights.Count, 30);
        for (int i = 0; i < lightCount; i++)
        {
            Vector3 pos = activeLights[i].transform.position;
            lightPositions[i] = new Vector4(pos.x, 0, pos.z, 1);
            lightRadii[i] = activeLights[i].Radius > 0 ? 
                activeLights[i].Radius + randomRadiusVariance : 
                activeLights[i].Radius;
            
            // 保持原有的四叉树光照逻辑
            activeLights[i].ApplyLighting();
        }

        // 为所有材质设置参数
        foreach (var material in terrainMaterials)
        {
            material.SetInt(LightCount, lightCount);
            material.SetVectorArray(LightPositions, lightPositions);
            material.SetFloatArray(LightRadii, lightRadii);
        }
      
    }


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

    void OnDrawGizmos()
    {
        tree?.DrawGizmos();
    }

}
