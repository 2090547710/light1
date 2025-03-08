using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

public enum StageType
{        
    Seed,
    Flower,
    Fruit,
}

public enum SizeLevel
{
    Small, // 小
    Medium, // 中
    Large // 大
}

public enum GrowthRateLevel
{
    Slow, // 慢
    Medium, // 中
    Fast // 快
}

public class Plant : MonoBehaviour
{
    [Header("生长设置")]
    public List<Lighting> lightSources = new List<Lighting>(); // 多个光源组件
    public int currentStage;
    public int maxStages = 3;
    public bool isWithered;
    
    [Header("阶段配置")]
    public List<PlantStage> growthStages = new List<PlantStage>();

    [Header("开花设置")]
    public float bloomThreshold = 0.8f; // 开花阈值
    public float bloomSteepness = 10f; // Sigmoid激活函数的陡峭度
    [SerializeField] private float bloomProbability; // 开花概率
    [SerializeField] private float brightnessRatio; // 添加亮度比例字段

    [Header("生长速度设置")]
    public float growthRate = 1.0f; // 恒定生长速度值
    public float growthRateInfluence = 0.3f; // 生长速度对开花概率的影响系数

    // 添加公共属性用于外部访问开花概率和亮度比例
    public float BloomProbability => bloomProbability;
    public float BrightnessRatio => brightnessRatio; // 新增亮度比例属性

    [Header("植物信息")]
    public int plantID; // 植物ID
    public string plantName; // 植物名称
    public List<int> prerequisitePlantIDs; // 前置植物ID列表
    public List<float> prerequisiteWeights; // 新增的权重集合
    public List<int> updatePlantIDs; // 更新植物ID列表 
    public List<float> updateWeights; // 更新权重列表
   
    void Start()
    {
        currentStage=0;
        lightSources.Clear();
        if (growthStages.Count > 0 && currentStage <= growthStages.Count)
        {
            Grow();
        }
    }
   
    
    public void Grow()
    {
        if (isWithered || currentStage >= maxStages) return;

        // 禁用并移除所有现有光源组件
        lightSources.ForEach(l => {
            l.RemoveLighting();
            LightingManager.tree.Remove(l.gameObject);
            Destroy(l);
        });
        lightSources.Clear();
        
        ApplyStageConfig(currentStage);
        
        // 从当前阶段获取植物信息
        if (currentStage < growthStages.Count) {
            plantID = growthStages[currentStage].plantID;
            plantName = growthStages[currentStage].plantName;
            growthRate = growthStages[currentStage].growthRate;
            prerequisitePlantIDs = growthStages[currentStage].prerequisitePlantIDs;
            prerequisiteWeights = growthStages[currentStage].prerequisiteWeights;
            updatePlantIDs = growthStages[currentStage].updatePlantIDs;
            updateWeights = growthStages[currentStage].updateWeights;
        }
        
        PlantManager.Instance.UpdatePlantCounts();
        currentStage++;
    }

    void ApplyStageConfig(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= maxStages)
        {
            Debug.LogWarning($"无效的生长阶段索引: {stageIndex}");
            return;
        }

        var stage = growthStages[stageIndex];
        // 根据数据创建并初始化光源组件
        stage.associatedLights.ForEach(data => {
            var newLight = gameObject.AddComponent<Lighting>();
            newLight.InitializeFromData(data);
            lightSources.Add(newLight); // 添加到光源列表
        });
        LightingManager.tree.Insert(gameObject);
        LightingManager.UpdateDirtyLights(); // 更新所有脏标记的光源
    }

    public void Wither()
    {
        isWithered = true;
        // 修改为禁用并销毁组件
        lightSources.ForEach(l => {
            l.RemoveLighting();
            l.enabled = false;
            Destroy(l);
        });
        lightSources.Clear();
        LightingManager.tree.Remove(gameObject);
    }

    void Update()
    {

    }

    public void TryBloom()
    {
        // 只在种子阶段尝试开花
        if (currentStage != 1 || isWithered) return;
        
        // 计算区域内亮度情况及开花概率
        float brightnessRatio = CalculateBrightnessRatio();

        // 根据概率决定是否开花
        if (UnityEngine.Random.value < bloomProbability)
        {
            Debug.Log($"种子成功开花！亮度比例: {brightnessRatio:F2}, 开花概率: {bloomProbability:F2}");

            // 尝试通过植物名称获取更新后的植物阶段
            PlantStage updatedStage = PlantManager.Instance.GetPlantStageBySeedFromName(plantName);
            if (updatedStage != null) {
                growthStages.Add(updatedStage);
                maxStages = growthStages.Count;
                Debug.Log($"植物已更新为: {updatedStage.plantName} (ID: {updatedStage.plantID})");
            }
            
            
            Grow();
        }
        else
        {
            Debug.Log($"种子尝试开花失败。亮度比例: {brightnessRatio:F2}, 开花概率: {bloomProbability:F2}");
        }
    }

      public void TryFruit()
    {
        // 只在花阶段尝试结果
        if (currentStage != 2 || isWithered) return;
        
        // 计算区域内亮度情况及结果概率
        float brightnessRatio = CalculateBrightnessRatio();

        // 根据概率决定是否结果
        if (UnityEngine.Random.value < bloomProbability)
        {
            Debug.Log($"花朵成功结果！亮度比例: {brightnessRatio:F2}, 结果概率: {bloomProbability:F2}");
            
            // 获取当前阶段的配置
            
            // 尝试获取更新后的植物阶段
            PlantStage updatedStage = PlantManager.Instance.GetUpdatedPlantStage(growthStages[currentStage-1]);
            if (updatedStage != null) {
                // 如果有更新的植物阶段，替换当前阶段
                growthStages.Add(updatedStage);
                maxStages = growthStages.Count;
                Debug.Log($"植物已更新为: {updatedStage.plantName} (ID: {updatedStage.plantID})");
            }

            
            Grow();
        }
        else
        {
            Debug.Log($"花朵尝试结果失败。亮度比例: {brightnessRatio:F2}, 结果概率: {bloomProbability:F2}");
        }
    }

    public float CalculateBrightnessRatio()
    {
        if (LightingManager.tree == null)
        {
            Debug.LogWarning("四叉树未初始化，无法计算亮度比例");
            bloomProbability = 0;
            brightnessRatio = 0; // 更新亮度比例字段
            return 0;
        }
        
        // 使用所有光源中最大的size作为检测区域大小
        float maxLightSize = 0f;
        if (lightSources.Count > 0)
        {
            maxLightSize = lightSources.Max(l => l.size);
        }
        else
        {
            Debug.LogWarning("没有光源，无法计算开花概率");
            bloomProbability = 0;
            brightnessRatio = 0;
            return 0;
        }
        
        Vector3 position = transform.position;
        
        // 创建检测区域的边界框
        Bounds bloomArea = new Bounds(position, new Vector3(maxLightSize, 0, maxLightSize));
        
        // 使用边界框方法获取区域内的所有叶子节点
        List<QuadTree.QuadTreeNode> leafNodes = LightingManager.tree.GetNeighborLeafNodes(bloomArea);
        
        if (leafNodes.Count == 0)
        {
            bloomProbability = 0;
            brightnessRatio = 0; // 更新亮度比例字段
            return 0;
        }
        
        // 计算实际亮度总和
        float totalBrightness = leafNodes.Sum(node => node.Brightness);
        
        // 计算理论最大亮度总和（每个节点亮度为1）
        float maxPossibleBrightness = leafNodes.Count;
        
        // 计算亮度比例
        brightnessRatio = totalBrightness / maxPossibleBrightness; // 更新亮度比例字段
        
        // 计算开花概率并存储为属性
        // 将生长速度作为阈值的调整因子
        float adjustedThreshold = bloomThreshold - (growthRate * growthRateInfluence);
        bloomProbability = 1f / (1f + Mathf.Exp(-bloomSteepness * (brightnessRatio - adjustedThreshold)));
        
        return brightnessRatio;
    }

    private void OnEnable()
    {
        // 确保 PlantManager 实例存在
        if (PlantManager.Instance != null)
        {
            PlantManager.Instance.RegisterPlant(this);
        }
    }
    
    private void OnDisable()
    {
        // 确保 PlantManager 实例存在
        if (PlantManager.Instance != null)
        {
            PlantManager.Instance.UnregisterPlant(this);
        }
    }

    [System.Serializable]
    public class PlantStage
    {
        public StageType stageType;//阶段类型
        public List<LightingData> associatedLights; // 改为存储光照数据
        public int plantID; // 植物ID
        public string plantName; // 植物名称
        public float growthRate; // 生长速度
        public List<int> prerequisitePlantIDs; // 前置植物ID列表
        public List<float> prerequisiteWeights; // 新增的权重集合
        public List<int> updatePlantIDs; // 更新植物ID列表 
        public List<float> updateWeights; // 更新权重列表
    }

} 