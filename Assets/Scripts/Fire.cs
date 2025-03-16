using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Fire : Plant
{
    // 灯塔位置相对于原点的偏移
    private Vector3 lighthousePosition;
    
    // 安全区域边界
    private Bounds safetyZone;
    
    // 添加可在Inspector中修改的参数
    [Header("火光源大小设置")]
    [Range(1f, 10f)] public float minLightSize = 5f;  // 最小size
    [Range(10f, 50f)] public float maxLightSize = 20f; // 最大size
    
    // 重新实现Start方法
    void Start()
    {
        // 初始化灯塔位置
        if (GameManager.Instance != null)
        {
            Vector2 size = GameManager.Instance.size;
            lighthousePosition = new Vector3(-size.x/2, 0, -size.y/2);
        }
        else
        {
            Debug.LogError("GameManager实例不存在，无法初始化灯塔位置");
        }
        // 初始化安全区域
        UpdateSafetyZone();
        // 初始化当前阶段
        currentStage = 0;
        lightSources.Clear();
        if (growthStages.Count > 0 && currentStage <= growthStages.Count)
        {
            Grow();
        }
        
        
        
        // 创建并设置名称显示
        CreateNameDisplay();
    }
    
    // 重载Grow方法
    public override void Grow()
    {
        // 首先检查基本条件
        if (isWithered || currentStage >= maxStages) return;

        // 添加碰撞检测逻辑
        if (currentStage < growthStages.Count && HasCollisionWithOtherPlants())
        {
            Debug.Log($"火植物 {plantName} 无法生长，检测到与其他植物的碰撞");
            return;
        }
        
        // 调用PlantManager中的方法来确保只有一个火
        bool isOnlyFire = PlantManager.Instance.EnsureSingleFire(this);

        // 如果是唯一的火，跳过亮度检查
        if (!isOnlyFire)
        {
            // 检查火区域亮度条件
            float fireAreaBrightness = CalculateFireAreaBrightness();
            float minRequiredBrightness = 0.9f; // 可以根据需要调整
            
            if (fireAreaBrightness < minRequiredBrightness)
            {
                Debug.Log($"火植物 {plantName} 无法生长，火区域亮度不足: {fireAreaBrightness:F2}");
                return;
            }
            
            // 通过亮度检查后，使其他火枯萎
            PlantManager.Instance.WitherOtherFires(this);
        }
        
        // 继续其他生长逻辑
        // 禁用并移除所有现有光源组件
        lightSources.ForEach(l => {
            l.RemoveLighting();
            LightingManager.tree.Remove(l.gameObject);
            Destroy(l);
        });
        lightSources.Clear();
        
        // 应用阶段配置前，先计算并更新光源大小
        if (currentStage < growthStages.Count)
        {
            UpdateLightSizeBasedOnSafetyZone(currentStage);
        }
        
        ApplyStageConfig(currentStage);
        currentStage++;
    }
    
    // 基于安全区面积更新光源大小
    private void UpdateLightSizeBasedOnSafetyZone(int stageIndex)
    {
        // 更新安全区域
        UpdateSafetyZone();
        
        // 计算安全区域的面积
        float safetyZoneArea = safetyZone.size.x * safetyZone.size.z;
        
        // 获取游戏总面积作为参考
        float totalGameArea = 0;
        if (GameManager.Instance != null)
        {
            Vector2 gameSize = GameManager.Instance.size;
            totalGameArea = gameSize.x * gameSize.y;
        }
        else
        {
            Debug.LogWarning("GameManager实例不存在，使用默认面积");
            totalGameArea = 10000f; // 默认 100x100
        }
        
        // 根据安全区面积与总面积的比例确定大小档位
        float areaRatio = safetyZoneArea / totalGameArea;
        float lightSize;
        
        if (areaRatio < 1f/64f) // 小于1/64总面积 (1/8的平方)
        {
            lightSize = minLightSize;
            Debug.Log($"火光源使用最小尺寸 - 安全区比例: {areaRatio:F4} (< 1/64)");
        }
        else if (areaRatio < 1f/16f) // 小于1/16总面积
        {
            lightSize = minLightSize + (maxLightSize - minLightSize) * 0.33f;
            Debug.Log($"火光源使用第二档尺寸 - 安全区比例: {areaRatio:F4} (< 1/16)");
        }
        else if (areaRatio < 1f/4f) // 小于1/4总面积
        {
            lightSize = minLightSize + (maxLightSize - minLightSize) * 0.67f;
            Debug.Log($"火光源使用第三档尺寸 - 安全区比例: {areaRatio:F4} (< 1/4)");
        }
        else // 大于或等于1/4总面积
        {
            lightSize = maxLightSize;
            Debug.Log($"火光源使用最大尺寸 - 安全区比例: {areaRatio:F4} (>= 1/4)");
        }
        
        // 更新当前阶段所有光源的size
        List<LightingData> updatedLights = new List<LightingData>();
        
        foreach (var lightData in growthStages[stageIndex].associatedLights)
        {
            // 创建新的 LightingData 对象并复制原有属性
            LightingData newLightData = new LightingData(
                size: lightSize,
                isObstacle: lightData.isObstacle,
                isSeed: lightData.isSeed,
                lightHeight: lightData.lightHeight,
                heightMap: lightData.heightMap,
                tiling: lightData.tiling,
                offset: lightData.offset
            );
            updatedLights.Add(newLightData);
        }
        
        // 替换原有列表
        growthStages[stageIndex].associatedLights = updatedLights;
    }
    
    // 更新安全区域边界
    private void UpdateSafetyZone()
    {
        if (GameManager.Instance != null)
        {
            // 获取火的当前位置
            Vector3 firePosition = transform.position;
            
            // 计算灯塔和火之间的中点作为安全区域的中心
            Vector3 center = (lighthousePosition + firePosition) / 2f;
            
            // 计算安全区域的尺寸
            Vector3 size = new Vector3(
                Mathf.Abs(firePosition.x - lighthousePosition.x),
                0.1f,  // 高度很小，因为我们主要关注xz平面
                Mathf.Abs(firePosition.z - lighthousePosition.z)
            );
            
            // 创建安全区域边界
            safetyZone = new Bounds(center, size);
            
        }
    }
    
    // 计算火区域亮度
    private float CalculateFireAreaBrightness()
    {
        if (LightingManager.tree == null)
        {
            Debug.LogWarning("四叉树未初始化，无法计算安全区域亮度");
            return 0;
        }
        
        // 获取GameManager中的size
        Vector2 size = Vector2.zero;
        if (GameManager.Instance != null)
        {
            size = GameManager.Instance.size;
        }
        else
        {
            Debug.LogWarning("GameManager实例不存在，使用默认大小");
            size = new Vector2(100, 100);
        }
        
        
        // 确保安全区域内的节点完全分裂到最小尺寸
        LightingManager.tree.PreSplitArea(safetyZone);

        // 使用边界框方法获取区域内的所有叶子节点
        List<QuadTree.QuadTreeNode> leafNodes = LightingManager.tree.GetNeighborLeafNodes(safetyZone);
        
        if (leafNodes.Count == 0)
        {
            Debug.LogWarning("安全区域内没有叶子节点");
            return 0;
        }
        
        // 计算实际亮度总和，超过1的亮度按1计算
        float totalBrightness = leafNodes.Sum(node => Mathf.Min(node.Brightness, 1f));
        
        // 计算理论最大亮度总和（每个节点亮度为1）
        float maxPossibleBrightness = leafNodes.Count;
        
        // 计算亮度比例
        float brightnessRatio = totalBrightness / maxPossibleBrightness;

        return brightnessRatio;
    }
    
    // 在Unity编辑器中可视化安全区域
    private void OnDrawGizmos()
    {
        if (GameManager.Instance != null && this.enabled)
        {
            Vector2 size = GameManager.Instance.size;
            Vector3 lighthouse = new Vector3(-size.x/2, 0, -size.y/2);
            
            // 绘制灯塔位置
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(lighthouse, 1f);
            
            // 绘制火的当前位置
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.5f);
            
            // 更新并绘制安全区域
            Gizmos.color = new Color(0, 1, 0, 0.2f); // 绿色表示安全区域
            Gizmos.DrawCube(safetyZone.center, safetyZone.size);
        
        }
    }
} 