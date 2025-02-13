using UnityEngine;
using System.Collections.Generic;

public class LightingDataProvider : MonoBehaviour
{
    // 从原LightingManager转移的字段
    private Vector4[] lightPositions = new Vector4[1000];
    private Vector4[] lightSizes = new Vector4[1000];
    [SerializeField] private List<Material> terrainMaterials = new List<Material>();

    // 暴露给LightingManager的接口
    public void UpdateLightingData(List<Lighting> activeLights)
    {
        int lightCount = Mathf.Min(activeLights.Count, 1000);
        for (int i = 0; i < lightCount; i++)
        {
            var light = activeLights[i];
            Vector3 pos = light.transform.position;
            
            // 获取光照区域参数
            Bounds area = new Bounds(pos, new Vector3(light.areaSizeX, light.areaHeight, light.areaSizeZ));
            bool isRect = (light.areaShape == AreaShape.Rectangle);
            bool isDark = (light.areaType != AreaType.Light);

            // 直接使用原始尺寸
            Vector3 unitSize = new Vector3(
                area.size.x, 
                area.size.y,
                area.size.z  
            );

            // 插入位置
            lightPositions[i] = new Vector4(
                pos.x, 
                light.lightHeight, // y: 光照高度
                pos.z, 
                isRect ? 1 : 0  // w: 是否矩形
            );

            //插入光照区域
            if (isRect)
            {
                lightSizes[i] = new Vector4(
                    unitSize.x,      // x: 宽度
                    unitSize.z,      // y: 长度
                    isDark ? 1 : 0,  // z: 是否黑暗
                    unitSize.y       // w: 区域高度
                );
                
            }
            else
            {
                lightSizes[i] = new Vector4(
                    unitSize.x,     // x: 直径
                    0,              // y: 未使用
                    isDark ? 1 : 0, // z: 是否黑暗
                    unitSize.y      // w: 区域高度
                );
            }
        }

        // 更新材质参数
        foreach (var material in terrainMaterials)
        {
            material.SetInt("_LightCount", lightCount);
            material.SetVectorArray("_LightPositions", lightPositions);
            material.SetVectorArray("_LightSizes", lightSizes);
        }
    }

    // 材质管理接口
    public void AddTerrainMaterial(Material mat) => terrainMaterials.Add(mat);
    public void RemoveTerrainMaterial(Material mat) => terrainMaterials.Remove(mat);
} 