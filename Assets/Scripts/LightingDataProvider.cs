using UnityEngine;
using System.Collections.Generic;

public class LightingDataProvider : MonoBehaviour
{
    // 从原LightingManager转移的字段
    private Vector4[] lightPositions = new Vector4[1000];
    private float[] lightRadii = new float[1000];
    [SerializeField] private List<Material> terrainMaterials = new List<Material>();

    // 暴露给LightingManager的接口
    public void UpdateLightingData(List<Lighting> activeLights, float lightVariance, float darkVariance)
    {
        // 收集光照数据（从原LateUpdate转移的逻辑）
        int lightCount = Mathf.Min(activeLights.Count, 1000);
        for (int i = 0; i < lightCount; i++)
        {
            Vector3 pos = activeLights[i].transform.position;
            lightPositions[i] = new Vector4(pos.x, 0, pos.z, 1);
            lightRadii[i] = activeLights[i].Radius > 0 ? 
                activeLights[i].Radius + lightVariance : 
                activeLights[i].Radius - darkVariance;
        }

        // 更新材质参数
        foreach (var material in terrainMaterials)
        {
            material.SetInt(LightingManager.LightCount, lightCount);
            material.SetVectorArray(LightingManager.LightPositions, lightPositions);
            material.SetFloatArray(LightingManager.LightRadii, lightRadii);
        }
    }

    // 材质管理接口
    public void AddTerrainMaterial(Material mat) => terrainMaterials.Add(mat);
    public void RemoveTerrainMaterial(Material mat) => terrainMaterials.Remove(mat);
} 