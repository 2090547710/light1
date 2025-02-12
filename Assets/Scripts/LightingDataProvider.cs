using UnityEngine;
using System.Collections.Generic;

public class LightingDataProvider : MonoBehaviour
{
    // 从原LightingManager转移的字段
    private Vector4[] lightPositions = new Vector4[1000];
    private Vector4[] lightSizes = new Vector4[1000];
    [SerializeField] private List<Material> terrainMaterials = new List<Material>();
    private float MinNodeSize;

    void Start()
    {
        MinNodeSize = GameManager.MinNodeSize.x;
    }

    // 暴露给LightingManager的接口
    public void UpdateLightingData(List<Lighting> activeLights, float lightVariance, float darkVariance)
    {
        // 收集光照数据（从原LateUpdate转移的逻辑）
        int lightCount = Mathf.Min(activeLights.Count, 1000);
        for (int i = 0; i < lightCount; i++)
        {
            var light = activeLights[i];
            Vector3 pos = light.transform.position;
            lightPositions[i] = new Vector4(pos.x, light.LightHeight*MinNodeSize, pos.z, 1);

            // 根据光照类型计算尺寸
            if (light.isDark)
            {
                float baseSize = Mathf.Abs(light.Radius);
                Vector2 darkSize = light.Radius > 0 ? 
                    light.customCollider.UnitSizeXZ : // 正半径使用碰撞体尺寸
                    new Vector2(baseSize*2, baseSize*2);   // 负半径使用正方形
                darkSize.x *= MinNodeSize;
                darkSize.y *= MinNodeSize;
                darkSize += new Vector2(darkVariance, darkVariance);
                Debug.Log(darkSize);
                lightSizes[i] = new Vector4(darkSize.x, darkSize.y, 0, 0);
            }
            else
            {
                // 光明区域使用（半径 + 变化量, 0）的格式
                float finalRadius = Mathf.Max(light.Radius*MinNodeSize+ lightVariance, 0);
                lightSizes[i] = new Vector4(finalRadius, 0, 0, 0);
            }
        }

        // 更新材质参数
        foreach (var material in terrainMaterials)
        {
            material.SetInt(LightingManager.LightCount, lightCount);
            material.SetVectorArray(LightingManager.LightPositions, lightPositions);
            material.SetVectorArray("_LightSizes", lightSizes);
        }
    }

    // 材质管理接口
    public void AddTerrainMaterial(Material mat) => terrainMaterials.Add(mat);
    public void RemoveTerrainMaterial(Material mat) => terrainMaterials.Remove(mat);
} 