using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using System.IO;

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

    [Header("开花设置")]
    public float bloomThreshold = 0.8f; // 开花阈值
    public float bloomSteepness = 10f; // Sigmoid激活函数的陡峭度
    [SerializeField] private float bloomProbability; // 开花概率
    [SerializeField] private float brightnessRatio; // 添加亮度比例字段

    [Header("生长速度设置")]
    public float growthRate = 1.0f; // 恒定生长速度值
    public float growthRateInfluence = 0.3f; // 生长速度对开花概率的影响系数

    // 添加缓存字段
    [Header("缓存系统")]
    [SerializeField] private float cachedBloomThreshold;
    [SerializeField] private float cachedBloomSteepness;
    [SerializeField] private float cachedGrowthRate;
    [SerializeField] private float cachedGrowthRateInfluence;
    [SerializeField] private bool isDirty = true; // 默认为脏，确保首次应用

    // 添加公共属性用于外部访问开花概率和亮度比例
    public float BloomProbability => bloomProbability;
    public float BrightnessRatio => brightnessRatio; // 新增亮度比例属性
    public bool IsDirty => isDirty;

    [Header("植物信息")]
    public int plantID; // 植物ID
    public string plantName; // 植物名称

    void Start()
    {
        LoadGrowthStagesFromResources("PlantData/test");
        currentStage=0;
        lightSources.Clear();
        if (growthStages.Count > 0 && currentStage <= growthStages.Count)
        {
            Grow();
        }
        
        // 初始化缓存值
        UpdateCachedValues();
        // 初始计算开花概率
        if (isDirty) CalculateBrightnessRatio();
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
        }
        
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
        if (Random.value < bloomProbability)
        {
            Debug.Log($"种子成功开花！亮度比例: {brightnessRatio:F2}, 开花概率: {bloomProbability:F2}");
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
        if (Random.value < bloomProbability)
        {
            Debug.Log($"花朵成功结果！亮度比例: {brightnessRatio:F2}, 结果概率: {bloomProbability:F2}");
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
        
        // 重置脏标记
        isDirty = false;
        
        return brightnessRatio;
    }

    // 添加标记为脏的方法
    public void MarkDirty()
    {
        isDirty = true;
    }
    
    // 更新缓存值方法
    private void UpdateCachedValues()
    {
        cachedBloomThreshold = bloomThreshold;
        cachedBloomSteepness = bloomSteepness;
        cachedGrowthRate = growthRate;
        cachedGrowthRateInfluence = growthRateInfluence;
    }
    
    // 添加OnValidate方法监测参数变化
    #if UNITY_EDITOR
    private void OnValidate()
    {
        // 检查参数是否发生变化
        if (cachedBloomThreshold != bloomThreshold ||
            cachedBloomSteepness != bloomSteepness ||
            cachedGrowthRate != growthRate ||
            cachedGrowthRateInfluence != growthRateInfluence)
        {
            // 标记为脏
            isDirty = true;
            
            // 如果在游戏运行时参数发生变化，立即重新计算
            if (Application.isPlaying)
            {
                CalculateBrightnessRatio();
            }
        }
        
        // 更新缓存值
        UpdateCachedValues();
    }
    #endif

    public void LoadGrowthStagesFromCSV(string filePath)
    {
        List<PlantStage> loadedStages = CSVParser.LoadPlantStagesFromCSV(filePath);
        
        if (loadedStages != null && loadedStages.Count > 0)
        {
            growthStages = loadedStages;
            maxStages = growthStages.Count;
            Debug.Log($"成功从CSV加载了{growthStages.Count}个生长阶段");
        }
        else
        {
            Debug.LogWarning("从CSV加载生长阶段失败或没有数据");
        }
    }

    // 添加一个方便的方法，从Resources文件夹加载CSV
    public void LoadGrowthStagesFromResources(string resourcePath)
    {
        // 如果传入的路径包含扩展名，则移除
        resourcePath = Path.GetFileNameWithoutExtension(resourcePath);
        
        // 如果传入的是完整路径（包含PlantData/），则使用原样
        // 否则，假设文件在PlantData文件夹中
        if (!resourcePath.Contains("/"))
        {
            resourcePath = $"PlantData/{resourcePath}";
        }
        
        TextAsset csvFile = Resources.Load<TextAsset>(resourcePath);
        if (csvFile != null)
        {
            // 创建临时文件，确保使用UTF-8编码
            string tempPath = Path.Combine(Application.temporaryCachePath, "temp_plant_stages.csv");
            File.WriteAllText(tempPath, csvFile.text, System.Text.Encoding.UTF8);
            
            // 加载数据
            LoadGrowthStagesFromCSV(tempPath);
            
            // 清理临时文件
            try { File.Delete(tempPath); } catch { }
            
            Debug.Log($"成功从{resourcePath}.csv加载植物数据");
        }
        else
        {
            Debug.LogError($"无法从Resources加载CSV文件: {resourcePath}");
        }
    }

    // 添加保存到CSV的方法
    public void SaveGrowthStagesToCSV(string filePath)
    {
        CSVParser.SavePlantStagesToCSV(growthStages, filePath);
    }

    [System.Serializable]
    public class PlantStage
    {
        public StageType stageType;//阶段类型
        public List<LightingData> associatedLights; // 改为存储光照数据
        public int plantID; // 植物ID
        public string plantName; // 植物名称
    }
} 