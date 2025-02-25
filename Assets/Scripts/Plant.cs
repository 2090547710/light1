using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public enum StageType
{        
    Seed,
    Flower,
    Fruit,
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

    void Start()
    {
        InitializeLightSources();
    }

    void InitializeLightSources()
    {
        currentStage=0;
        lightSources.Clear();
        if (growthStages.Count > 0 && currentStage <= growthStages.Count)
        {
            ApplyStageConfig(currentStage);
        }
    }

    public void Grow()
    {
        if (isWithered || currentStage >= maxStages) return;

        // 禁用并移除所有现有光源组件
        lightSources.ForEach(l => {
            l.enabled = false;
            Destroy(l);
        });
        lightSources.Clear();

        currentStage++;
        ApplyStageConfig(currentStage);
    }

    void ApplyStageConfig(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= growthStages.Count)
        {
            Debug.LogWarning($"无效的生长阶段索引: {stageIndex}");
            return;
        }

        var stage = growthStages[stageIndex];
        // 根据数据创建并初始化光源组件
        // stage.associatedLights.ForEach(data => {
        //     var newLight = gameObject.AddComponent<Lighting>();
        //     newLight.InitializeFromData(data);
        //     lightSources.Add(newLight); // 添加到光源列表
        // });
    }

    public void Wither()
    {
        isWithered = true;
        // 修改为禁用并销毁组件
        lightSources.ForEach(l => {
            l.enabled = false;
            Destroy(l);
        });
        lightSources.Clear();
    }

    [System.Serializable]
    public class PlantStage
    {
        public StageType stageType;
        // public List<LightingData> associatedLights; // 改为存储光照数据
    }
} 