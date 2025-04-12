using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 场景加载时自动生成光照的组件
public class LightingLoader : MonoBehaviour
{
    [Header("光照设置")]
    public float lightSize = 10f; // 默认光照大小
    public Texture2D lightHeightMap; // 指定的高度图
    [Range(0, 1)] public float lightHeight = 0.5f; // 光照高度
    public bool isObstacle = true; // 是否为障碍物光源
    public bool isSeed = false; // 是否为种子光源

    private void Start()
    {
        // 检查是否已经有Lighting组件
        Lighting existingLight = GetComponent<Lighting>();
        
        if (existingLight == null)
        {
            // 如果没有，添加一个新的Lighting组件
            Lighting newLight = gameObject.AddComponent<Lighting>();
            
            // 使用LightingData初始化光照
            LightingData lightData = new LightingData(
                size: lightSize,
                isObstacle: isObstacle,
                isSeed: isSeed,
                lightHeight: lightHeight,
                heightMap: lightHeightMap
            );
            
            // 应用光照数据
            newLight.InitializeFromData(lightData);
            
        }
        else
        {
            // 如果已存在，更新现有的Lighting组件
            existingLight.size = lightSize;
            existingLight.isObstacle = isObstacle;
            existingLight.isSeed = isSeed;
            existingLight.lightHeight = lightHeight;
            existingLight.heightMap = lightHeightMap;

            
            // 标记为脏以更新光照
            existingLight.MarkDirty();
            LightingManager.UpdateDirtyLights();
            
            Debug.Log($"更新了位置 {transform.position} 的现有光照，大小: {lightSize}");
        }
    }
} 