using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class PlantManager : MonoBehaviour
{
#region 字段和属性定义
    // 单例模式
    public static PlantManager Instance { get; private set; }
        
    // 植物数据库
    private Dictionary<int, Plant.PlantStage> plantDatabase = new Dictionary<int, Plant.PlantStage>();
       
    // 种子映射数据
    public List<SeedMapping> seedMappings = new List<SeedMapping>();
    
    // 文件路径
    public string plantDatabasePath = "PlantData/PlantDatabase";
    public string seedMappingPath = "PlantData/SeedMapping";
    
    // 修改前置植物关系图，加入数量
    private Dictionary<int, List<KeyValuePair<int, float>>> plantPrerequisites = new Dictionary<int, List<KeyValuePair<int, float>>>();
    
    // 添加植物ID和数量的字典
    private Dictionary<int, int> activePlantsCounts = new Dictionary<int, int>();
    
    // 添加活跃植物列表
    public List<Plant> activePlants = new List<Plant>();
    
    // 添加可更新植物列表
    public List<int> updatablePlants = new List<int>();
    
    [Serializable]
    public class SeedMapping
    {
        public SizeLevel size;
        public GrowthRateLevel growthRate;
        public List<LightingData> lightData;
        public List<int> targetPlantIdList = new List<int>();
        public List<string> plantNameList = new List<string>();
        public List<float> weightList = new List<float>();
        public float growthRateValue;
    }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }       
        // 加载数据库
        LoadPlantDatabase();
        LoadSeedMappings();
        // PrintPlantDatabaseInfo();
        // PrintPrerequisiteGraph();
    }
#endregion

#region 数据库加载与解析
    // 加载植物数据库
    private void LoadPlantDatabase()
    {
        TextAsset csvFile = Resources.Load<TextAsset>(plantDatabasePath);
        if (csvFile == null)
        {
            Debug.LogError("找不到植物数据库文件: " + plantDatabasePath);
            return;
        }
        
        // 使用System.Text.Encoding.GetEncoding(936)来处理ANSI中文编码
        byte[] bytes = csvFile.bytes;
        string content = System.Text.Encoding.GetEncoding(936).GetString(bytes);
        string[] lines = content.Split('\n');
        for (int i = 1; i < lines.Length; i++) // 跳过表头
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            
            try
            {
                string[] values = line.Split(',');
                int plantId = int.Parse(values[0]);
                
                // 解析植物阶段
                Plant.PlantStage stage = ParsePlantStage(values);
                // 添加到数据库
                if (stage.associatedLights != null && stage.associatedLights.Count > 0)
                {
                    plantDatabase[plantId] = stage;
                   
                }
                else
                {
                    Debug.LogWarning($"植物ID={plantId}没有光源数据，跳过添加到数据库");
                }
                
            }
            catch (Exception e)
            {
                Debug.LogError($"解析植物数据行失败: {line}\n{e.Message}");
            }
        }
        
        BuildPrerequisiteGraph();
    }
    
    // 加载种子映射数据
    private void LoadSeedMappings()
    {
        TextAsset csvFile = Resources.Load<TextAsset>(seedMappingPath);
        if (csvFile == null)
        {
            Debug.LogError("找不到种子映射文件: " + seedMappingPath);
            return;
        }
        
        // 使用System.Text.Encoding.GetEncoding(936)来处理ANSI中文编码
        byte[] bytes = csvFile.bytes;
        string content = System.Text.Encoding.GetEncoding(936).GetString(bytes);
        string[] lines = content.Split('\n');
        for (int i = 1; i < lines.Length; i++) // 跳过表头
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;
            
            try
            {
                string[] values = line.Split(',');
                
                // 解析种子映射
                SeedMapping mapping = new SeedMapping();
                mapping.size = (SizeLevel)Enum.Parse(typeof(SizeLevel), values[0]);
                mapping.growthRate = (GrowthRateLevel)Enum.Parse(typeof(GrowthRateLevel), values[1]);
                
                // 解析光源数据 - 根据CSV格式直接读取固定位置的值
                mapping.lightData = new List<LightingData>();
                
                // 创建单个光照数据
                float size = float.Parse(values[2]);
                bool isObstacle = bool.Parse(values[3]);
                bool isSeed = bool.Parse(values[4]);
                float lightHeight = float.Parse(values[5]);
                
                // 高度图处理
                Texture2D heightMap = null;
                string heightMapPath = values[6];
                if (!string.IsNullOrEmpty(heightMapPath) && heightMapPath != "null")
                {
                    // 从Resources加载高度图
                    heightMap = Resources.Load<Texture2D>("HeightMaps/" + heightMapPath);
                }
                
                // 创建光照数据并添加到列表
                LightingData lightData = new LightingData(size, isObstacle, isSeed, lightHeight, heightMap);
                mapping.lightData.Add(lightData);
                
                // 解析生长速度值
                if (values.Length > 7 && float.TryParse(values[7], out float growthRateValue))
                {
                    mapping.growthRateValue = growthRateValue;
                }
                else
                {
                    // 默认生长速度值基于枚举
                    mapping.growthRateValue = GetDefaultGrowthRate(mapping.growthRate);
                }
                
                // 解析目标植物ID、名称和权重
                // 从索引8开始，每三个字段为一组(ID、名称和权重)
                for (int j = 8; j < values.Length - 2; j += 3)
                {
                    if (!string.IsNullOrEmpty(values[j]) && int.TryParse(values[j], out int plantId))
                    {
                        mapping.targetPlantIdList.Add(plantId);
                        
                        // 添加对应的植物名称
                        if (j + 1 < values.Length)
                        {
                            mapping.plantNameList.Add(values[j + 1]);
                        }
                        else
                        {
                            // 如果没有对应的名称，添加空字符串
                            mapping.plantNameList.Add("");
                        }
                        
                        // 添加对应的权重
                        if (j + 2 < values.Length && float.TryParse(values[j + 2], out float weight))
                        {
                            mapping.weightList.Add(weight);
                        }
                        else
                        {
                            // 如果没有对应的权重或无法解析，添加默认权重1.0
                            mapping.weightList.Add(1.0f);
                        }
                    }
                }
                
                seedMappings.Add(mapping);
            }
            catch (Exception e)
            {
                Debug.LogError($"解析种子映射行失败: {line}\n{e.Message}");
            }
        }
        
        // Debug.Log($"成功加载 {seedMappings.Count} 个种子映射");
    }
    
    // 解析植物阶段
    private Plant.PlantStage ParsePlantStage(string[] values)
    {
        Plant.PlantStage stage = new Plant.PlantStage();
        
        // 解析基本信息
        int plantId = int.Parse(values[0]);
        stage.stageType = (StageType)Enum.Parse(typeof(StageType), values[1]);
        stage.plantName = values[2];
        stage.plantID = plantId;
        
        // 初始化光源列表
        stage.associatedLights = new List<LightingData>();
        
        int currentIndex = 3;
        bool foundLightMarker = false;
        bool foundGrMarker = false;
        bool foundPreMarker = false;
        bool foundUpMarker = false;
        
        // 遍历所有字段
        while (currentIndex < values.Length)
        {
            // 检查标记
            if (values[currentIndex] == "li")
            {
                foundLightMarker = true;
                currentIndex++;
                continue;
            }
            else if (values[currentIndex] == "gr")
            {
                foundGrMarker = true;
                currentIndex++;
                continue;
            }
            else if (values[currentIndex] == "pre")
            {
                foundPreMarker = true;
                currentIndex++;
                continue;
            }
            else if (values[currentIndex] == "up")
            {
                foundUpMarker = true;
                currentIndex++;
                continue;
            }
            
            // 处理光源数据
            if (foundLightMarker && !foundGrMarker)
            {
                // 确保有足够的字段来解析光源数据
                if (currentIndex + 4 < values.Length)
                {
                    try {
                        // 检查是否为空值
                        if (string.IsNullOrEmpty(values[currentIndex]) || 
                            string.IsNullOrEmpty(values[currentIndex + 1]) ||
                            string.IsNullOrEmpty(values[currentIndex + 2]) ||
                            string.IsNullOrEmpty(values[currentIndex + 3]))
                        {
                            // 如果光源数据中有空值，跳过整个光源组
                            currentIndex += 5;
                            continue;
                        }
                        
                        // 解析光源数据
                        float size = float.Parse(values[currentIndex]);
                        bool isObstacle = bool.Parse(values[currentIndex + 1]);
                        bool isSeed = bool.Parse(values[currentIndex + 2]);
                        float lightHeight = float.Parse(values[currentIndex + 3]);
                        
                        // 高度图处理
                        Texture2D heightMap = null;
                        string heightMapPath = values[currentIndex + 4];
                        if (!string.IsNullOrEmpty(heightMapPath) && heightMapPath != "null")
                        {
                            heightMap = Resources.Load<Texture2D>("HeightMaps/" + heightMapPath);
                            if (heightMap == null)
                            {
                                Debug.LogWarning($"植物ID {plantId}: 无法加载高度图: {heightMapPath}");
                            }
                        }
                        
                        // 创建光照数据并添加到列表
                        LightingData lightData = new LightingData(size, isObstacle, isSeed, lightHeight, heightMap);
                        stage.associatedLights.Add(lightData);
                        
                        // 移动到下一组光源数据
                        currentIndex += 5;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"植物ID {plantId}: 解析光源数据失败，位置: {currentIndex}，错误: {e.Message}");
                        // 跳过当前光源组
                        currentIndex += 5;
                    }
                }
                else
                {
                    // 如果剩余字段不足，跳出循环
                    break;
                }
            }
            // 处理生长速率
            else if (foundGrMarker && !foundPreMarker)
            {
                if (!string.IsNullOrEmpty(values[currentIndex]) && 
                    float.TryParse(values[currentIndex], out float growthRate))
                {
                    stage.growthRate = growthRate;
                    currentIndex++;
                }
                else
                {
                    // 如果没有找到生长速率或为空，设置默认值
                    stage.growthRate = 1.0f;
                    // 如果当前字段为空，跳过
                    if (currentIndex < values.Length && string.IsNullOrEmpty(values[currentIndex]))
                    {
                        currentIndex++;
                    }
                }
            }
            // 处理前置植物
            else if (foundPreMarker && !foundUpMarker)
            {
                // 初始化前置植物列表
                if (stage.prerequisitePlantIDs == null)
                {
                    stage.prerequisitePlantIDs = new List<int>();
                    stage.prerequisiteWeights = new List<float>();
                }
                
                // 确保有足够的字段来解析前置植物数据
                if (currentIndex + 1 < values.Length)
                {
                    // 检查前置植物ID和权重是否都有效
                    if (!string.IsNullOrEmpty(values[currentIndex]) && 
                        !string.IsNullOrEmpty(values[currentIndex + 1]))
                    {
                        // 尝试解析植物ID和权重
                        if (int.TryParse(values[currentIndex], out int prerequisiteId) && 
                            float.TryParse(values[currentIndex + 1], out float weight))
                        {
                            // 只有当ID和权重都成功解析时，才添加到列表
                            stage.prerequisitePlantIDs.Add(prerequisiteId);
                            stage.prerequisiteWeights.Add(weight);
                        }
                    }
                    
                    // 移动到下一组前置植物数据
                    currentIndex += 2;
                }
                else
                {
                    // 如果剩余字段不足，跳出循环
                    break;
                }
            }
            // 处理更新植物
            else if (foundUpMarker)
            {
                // 初始化更新植物列表
                if (stage.updatePlantIDs == null)
                {
                    stage.updatePlantIDs = new List<int>();
                    stage.updateWeights = new List<float>();
                }
                
                // 确保有足够的字段来解析更新植物数据
                if (currentIndex + 1 < values.Length)
                {
                    // 检查更新植物ID和权重是否都有效
                    if (!string.IsNullOrEmpty(values[currentIndex]) && 
                        !string.IsNullOrEmpty(values[currentIndex + 1]))
                    {
                        // 尝试解析更新植物ID和权重
                        if (int.TryParse(values[currentIndex], out int updateId) && 
                            float.TryParse(values[currentIndex + 1], out float weight))
                        {
                            // 只有当ID和权重都成功解析时，才添加到列表
                            stage.updatePlantIDs.Add(updateId);
                            stage.updateWeights.Add(weight);
                        }
                    }
                    
                    // 移动到下一组更新植物数据
                    currentIndex += 2;
                }
                else
                {
                    // 如果剩余字段不足，跳出循环
                    break;
                }
            }
            else
            {
                // 如果没有找到任何标记，跳过当前字段
                currentIndex++;
            }
        }
        
        return stage;
    }
    
    // 根据生长速度枚举获取默认生长速度值
    private float GetDefaultGrowthRate(GrowthRateLevel level)
    {
        switch (level)
        {
            case GrowthRateLevel.Slow:
                return 0.5f;
            case GrowthRateLevel.Medium:
                return 1.0f;
            case GrowthRateLevel.Fast:
                return 2.0f;
            default:
                return 1.0f;
        }
    }
#endregion

#region 种子和植物阶段获取
    // 根据种子尺寸和生长速度获取种子阶段数据
    public Plant.PlantStage GetSeedPlantStage(SizeLevel size, GrowthRateLevel growthRate)
    {
        // 查找匹配的种子映射
        SeedMapping mapping = seedMappings.FirstOrDefault(m => m.size == size && m.growthRate == growthRate);
        if (mapping == null)
        {
            Debug.LogWarning($"找不到匹配的种子: 尺寸={size}, 生长速度={growthRate}");
            return null;
        }
        
        // 创建种子阶段
        Plant.PlantStage seedStage = new Plant.PlantStage
        {
            stageType = StageType.Seed,
            plantName = size.ToString() + growthRate.ToString(),
            associatedLights = mapping.lightData,
            growthRate = mapping.growthRateValue
        };
        
        return seedStage;
    }
    
    // 根据种子尺寸和生长速度获取植物阶段数据
    public Plant.PlantStage GetPlantStageBySeed(SizeLevel size, GrowthRateLevel growthRate)
    {
        // 查找匹配的种子映射
        SeedMapping mapping = seedMappings.FirstOrDefault(m => m.size == size && m.growthRate == growthRate);
        if (mapping == null)
        {
            Debug.LogWarning($"找不到匹配的种子: 尺寸={size}, 生长速度={growthRate}");
            return null;
        }
        
        // 检查是否有植物
        if (mapping.targetPlantIdList.Count == 0)
        {
            Debug.LogWarning($"种子映射中没有植物数据");
            return null;
        }
        
        // 如果只有一个植物，直接返回
        if (mapping.targetPlantIdList.Count == 1)
        {
            int targetPlantId = mapping.targetPlantIdList[0];
            string plantName = mapping.plantNameList.Count > 0 ? mapping.plantNameList[0] : "";
            
            return GetPlantStageById(targetPlantId, plantName, mapping);
        }
        
        // 根据权重选择植物
        float totalWeight = 0;
        for (int i = 0; i < mapping.weightList.Count; i++)
        {
            totalWeight += mapping.weightList[i];
        }
        
        // 生成随机数
        float randomValue = UnityEngine.Random.Range(0, totalWeight);
        float cumulativeWeight = 0;
        
        // 根据权重选择植物
        for (int i = 0; i < mapping.weightList.Count; i++)
        {
            cumulativeWeight += mapping.weightList[i];
            if (randomValue <= cumulativeWeight)
            {
                int targetPlantId = mapping.targetPlantIdList[i];
                string plantName = i < mapping.plantNameList.Count ? mapping.plantNameList[i] : "";
                
                return GetPlantStageById(targetPlantId, plantName, mapping);
            }
        }
        
        // 如果没有选中任何植物（理论上不应该发生），返回第一个植物
        int fallbackPlantId = mapping.targetPlantIdList[0];
        string fallbackPlantName = mapping.plantNameList.Count > 0 ? mapping.plantNameList[0] : "";
        
        return GetPlantStageById(fallbackPlantId, fallbackPlantName, mapping);
    }
    
    // 辅助方法：根据植物ID获取植物阶段
    private Plant.PlantStage GetPlantStageById(int plantId, string plantName, SeedMapping mapping)
    {
        // 创建种子阶段
        Plant.PlantStage seedStage = new Plant.PlantStage
        {
            stageType = StageType.Seed,
            plantName = plantName,
            plantID = plantId,
            associatedLights = mapping.lightData,
            growthRate = mapping.growthRateValue
        };
        
        // 检查植物数据库中是否存在对应ID的植物
        if (plantDatabase.TryGetValue(plantId, out Plant.PlantStage plantStage))
        {
            // 返回数据库中的植物阶段
            return plantStage;
        }
        else
        {
            Debug.LogWarning($"植物数据库中找不到ID为{plantId}的植物");
            // 如果找不到，至少返回种子阶段
            return seedStage;
        }
    }

     public Plant.PlantStage GetPlantStageBySeedFromName(string seedName)
    {
        // 去除前后空白字符
        seedName = seedName.Trim();
        
        // 遍历所有尺寸枚举值，判断种子名字是否以该字符串开头
        foreach (string sizeStr in Enum.GetNames(typeof(SizeLevel)))
        {
            if (seedName.StartsWith(sizeStr, StringComparison.OrdinalIgnoreCase))
            {
                // 获取种子名称中除尺寸外的部分，作为生长速度字符串
                string growthRatePart = seedName.Substring(sizeStr.Length);
                
                // 尝试解析生长速度枚举（忽略大小写）
                if (Enum.TryParse(growthRatePart, true, out GrowthRateLevel growthRate) &&
                    Enum.TryParse(sizeStr, true, out SizeLevel sizeLevel))
                {
                    // 调用 GetPlantStageBySeed 方法返回植物阶段数据
                    return GetPlantStageBySeed(sizeLevel, growthRate);
                }
            }
        }
        
        Debug.LogWarning("无法从种子名字解析出尺寸和生长速度: " + seedName);
        return null;
    }
#endregion

#region 植物生长与结果系统
    // 更新植物ID和数量字典
    public void UpdatePlantCounts(Plant plant, bool isAdding)
    {
        int plantId = plant.plantID;
        
        // 根据isAdding参数增加或减少植物计数
        if (isAdding)
        {
            // 植物添加：计数增加
            if (activePlantsCounts.ContainsKey(plantId))
            {
                activePlantsCounts[plantId]++;
            }
            else
            {
                activePlantsCounts[plantId] = 1;
            }
        }
        else
        {
            // 植物移除：计数减少
            if (activePlantsCounts.ContainsKey(plantId))
            {
                activePlantsCounts[plantId]--;
                
                // 如果计数为0，移除该植物ID
                if (activePlantsCounts[plantId] <= 0)
                {
                    activePlantsCounts.Remove(plantId);
                }
            }
        }
        
        // 更新可更新植物列表
        UpdateUpdatablePlants(plant);
    }

    // 更新所有植物计数 (向后兼容，用于初始化和批量更新)
    public void UpdateAllPlantCounts()
    {
        // 清空当前字典
        activePlantsCounts.Clear();
        
        // 遍历活跃植物列表
        foreach (Plant plant in activePlants)
        {
            int plantId = plant.plantID;
            
            // 如果字典中已存在该植物ID，则数量加1
            if (activePlantsCounts.ContainsKey(plantId))
            {
                activePlantsCounts[plantId]++;
            }
            // 否则，添加新的植物ID，数量为1
            else
            {
                activePlantsCounts[plantId] = 1;
            }
        }
        
        // 完全重建可更新植物列表
        RebuildUpdatablePlants();
    }
    
    // 更新可更新植物列表 - 增量更新
    private void UpdateUpdatablePlants(Plant plant)
    {
        // 情况1：植物被添加 (isAdding = true)
        // 添加当前植物可能允许哪些植物被解锁，需要检查所有以当前植物为前置的植物
        
        // 情况2：植物被移除 (isAdding = false)
        // 移除当前植物可能导致某些植物不再满足前置条件，需要重新检查
        
        // 获取植物ID
        int plantId = plant.plantID;
        
        // 获取所有以当前植物为前置的植物
        List<int> affectedPlants = new List<int>();
        
        foreach (var entry in plantPrerequisites)
        {
            int potentialPlantId = entry.Key;
            List<KeyValuePair<int, float>> prerequisites = entry.Value;
            
            // 检查当前植物是否为该植物的前置
            bool isPrerequisite = prerequisites.Any(p => p.Key == plantId);
            
            if (isPrerequisite)
            {
                affectedPlants.Add(potentialPlantId);
            }
        }
        
        // 对每个受影响的植物，检查是否满足所有前置条件
        foreach (int affectedPlantId in affectedPlants)
        {
            bool allPrerequisitesMet = true;
            
            foreach (var prerequisite in plantPrerequisites[affectedPlantId])
            {
                int prerequisiteId = prerequisite.Key;
                float requiredCount = prerequisite.Value;
                
                // 获取当前植物数量
                int currentCount = GetPlantCount(prerequisiteId);
                
                // 如果当前数量小于所需数量，则不满足条件
                if (currentCount < requiredCount)
                {
                    allPrerequisitesMet = false;
                    break;
                }
            }
            
            // 更新可更新植物列表
            if (allPrerequisitesMet)
            {
                // 如果满足条件且不在列表中，添加
                if (!updatablePlants.Contains(affectedPlantId))
                {
                    updatablePlants.Add(affectedPlantId);
                }
            }
            else
            {
                // 如果不满足条件但在列表中，移除
                if (updatablePlants.Contains(affectedPlantId))
                {
                    updatablePlants.Remove(affectedPlantId);
                }
            }
        }
        
        // 更新完可更新植物列表后，尝试让活跃植物结果
        TryFruitForEligiblePlants();
    }

    // 完全重建可更新植物列表（用于初始化和批量更新）
    private void RebuildUpdatablePlants()
    {
        // 清空当前可更新植物列表
        updatablePlants.Clear();
        
        // 遍历所有前置植物关系
        foreach (var entry in plantPrerequisites)
        {
            int plantId = entry.Key;
            List<KeyValuePair<int, float>> prerequisites = entry.Value;
            
            // 检查是否满足所有前置条件
            bool allPrerequisitesMet = true;
            
            foreach (var prerequisite in prerequisites)
            {
                int prerequisiteId = prerequisite.Key;
                float requiredCount = prerequisite.Value;
                
                // 获取当前植物数量
                int currentCount = GetPlantCount(prerequisiteId);
                
                // 如果当前数量小于所需数量，则不满足条件
                if (currentCount < requiredCount)
                {
                    allPrerequisitesMet = false;
                    break;
                }
            }
            
            // 如果满足所有前置条件，则添加到可更新植物列表
            if (allPrerequisitesMet)
            {
                updatablePlants.Add(plantId);
            }
        }
        
        // 更新完可更新植物列表后，尝试让活跃植物结果
        TryFruitForEligiblePlants();
    }
    
    // 尝试让符合条件的植物结果
    private void TryFruitForEligiblePlants()
    {
        // 遍历所有活跃植物
        foreach (Plant plant in activePlants)
        {
            // 检查植物是否可以尝试结果
            if (plant.CanTryFruit())
            {
                // 调用植物的TryFruit方法
                plant.TryFruit(); 
            }
        }
    }
    
    // 获取可更新植物列表（只读）
    public IReadOnlyList<int> GetUpdatablePlants()
    {
        return updatablePlants;
    }
    
    // 检查植物是否可更新
    public bool IsPlantUpdatable(int plantId)
    {
        return updatablePlants.Contains(plantId);
    }
    
    // 获取植物数量字典（只读）
    public IReadOnlyDictionary<int, int> GetPlantCounts()
    {
        return activePlantsCounts;
    }
    
    // 获取特定植物ID的数量
    public int GetPlantCount(int plantId)
    {
        if (activePlantsCounts.TryGetValue(plantId, out int count))
        {
            return count;
        }
        return 0;
    }
 
    // 根据当前植物阶段的更新植物列表和权重返回一个更新后的植物阶段
    public Plant.PlantStage GetUpdatedPlantStage(Plant.PlantStage currentStage)
    {
        // 检查当前植物阶段是否有更新植物列表
        if (currentStage.updatePlantIDs == null || currentStage.updatePlantIDs.Count == 0)
        {
            return null;
        }
        
        // 查找可更新植物列表和当前植物阶段更新列表的交集
        List<int> availableUpdatePlants = new List<int>();
        List<float> availableUpdateWeights = new List<float>();
        
        for (int i = 0; i < currentStage.updatePlantIDs.Count; i++)
        {
            int updatePlantId = currentStage.updatePlantIDs[i];
            
            // 检查是否在可更新植物列表中
            if (updatablePlants.Contains(updatePlantId))
            {
                availableUpdatePlants.Add(updatePlantId);
                
                // 获取权重（如果有）
                float weight = 1.0f; // 默认权重
                if (currentStage.updateWeights != null && i < currentStage.updateWeights.Count)
                {
                    weight = currentStage.updateWeights[i];
                }
                
                availableUpdateWeights.Add(weight);
            }
        }
        
        // 检查是否有可用的更新植物
        if (availableUpdatePlants.Count == 0)
        {   
            return null;
        }
        
        // 如果只有一个可用的更新植物，直接返回
        if (availableUpdatePlants.Count == 1)
        {
            int updatePlantId = availableUpdatePlants[0];
            if (plantDatabase.TryGetValue(updatePlantId, out Plant.PlantStage updateStage))
            {
                return updateStage;
            }
            else
            {
                Debug.LogWarning($"无法在植物数据库中找到ID为 {updatePlantId} 的植物");
                return null;
            }
        }
        
        // 如果有多个可用的更新植物，根据权重随机选择一个
        float totalWeight = 0;
        for (int i = 0; i < availableUpdateWeights.Count; i++)
        {
            totalWeight += availableUpdateWeights[i];
        }
        
        // 生成随机数
        float randomValue = UnityEngine.Random.Range(0, totalWeight);
        float cumulativeWeight = 0;
        
        // 根据权重选择植物
        for (int i = 0; i < availableUpdateWeights.Count; i++)
        {
            cumulativeWeight += availableUpdateWeights[i];
            if (randomValue <= cumulativeWeight)
            {
                int selectedPlantId = availableUpdatePlants[i];
                if (plantDatabase.TryGetValue(selectedPlantId, out Plant.PlantStage selectedStage))
                {
                    return selectedStage;
                }
                else
                {
                    Debug.LogWarning($"无法在植物数据库中找到ID为 {selectedPlantId} 的植物");
                    return null;
                }
            }
        }
        
        // 如果没有选中任何植物（理论上不应该发生），返回第一个植物
        int fallbackPlantId = availableUpdatePlants[0];
        if (plantDatabase.TryGetValue(fallbackPlantId, out Plant.PlantStage fallbackStage))
        {
            return fallbackStage;
        }
        else
        {
            Debug.LogWarning($"无法在植物数据库中找到ID为 {fallbackPlantId} 的植物");
            return null;
        }
    }
#endregion

#region 前置植物关系管理
    // 修改前置植物关系图构建
    private void BuildPrerequisiteGraph()
    {
        plantPrerequisites.Clear();
        
        foreach (var entry in plantDatabase)
        {
            int plantId = entry.Key;
            Plant.PlantStage stage = entry.Value;
            
            if (stage.prerequisitePlantIDs != null && stage.prerequisitePlantIDs.Count > 0)
            {
                if (!plantPrerequisites.ContainsKey(plantId))
                {
                    plantPrerequisites[plantId] = new List<KeyValuePair<int, float>>();
                }
                
                for (int i = 0; i < stage.prerequisitePlantIDs.Count; i++)
                {
                    int prerequisiteId = stage.prerequisitePlantIDs[i];
                    float weight = 1.0f; // 默认权重
                    
                    // 如果存在权重列表且有对应权重，则使用该权重
                    if (stage.prerequisiteWeights != null && i < stage.prerequisiteWeights.Count)
                    {
                        weight = stage.prerequisiteWeights[i];
                    }
                    
                    // 添加前置植物ID和权重的键值对
                    if (!plantPrerequisites[plantId].Any(p => p.Key == prerequisiteId))
                    {
                        plantPrerequisites[plantId].Add(new KeyValuePair<int, float>(prerequisiteId, weight));
                    }
                }
            }
        }
    }

    public Plant.PlantStage GetSeedPlantStageFromName(string seedName)
    {
        // 去除前后空白字符
        seedName = seedName.Trim();
        
        // 遍历所有尺寸枚举值，判断种子名字是否以该字符串开头
        foreach (string sizeStr in Enum.GetNames(typeof(SizeLevel)))
        {
            if (seedName.StartsWith(sizeStr, StringComparison.OrdinalIgnoreCase))
            {
                // 获取种子名称中除尺寸外的部分，作为生长速度字符串
                string growthRatePart = seedName.Substring(sizeStr.Length);
                
                // 尝试解析生长速度枚举（忽略大小写）
                if (Enum.TryParse(growthRatePart, true, out GrowthRateLevel growthRate) &&
                    Enum.TryParse(sizeStr, true, out SizeLevel sizeLevel))
                {
                    // 调用已有的 GetSeedPlantStage 方法返回种子阶段数据
                    return GetSeedPlantStage(sizeLevel, growthRate);
                }
            }
        }
        
        Debug.LogWarning("无法从种子名字解析出尺寸和生长速度: " + seedName);
        return null;
    }
#endregion

#region 调试与辅助功能
    // 获取植物数据库（只读）
    public IReadOnlyDictionary<int, Plant.PlantStage> GetPlantDatabase()
    {
        return plantDatabase;
    }
    
    // 修改打印方法以显示权重信息
    public void PrintPlantDatabaseInfo()
    {
        if (plantDatabase == null || plantDatabase.Count == 0)
        {
            Debug.Log("植物数据库为空");
            return;
        }
        
        Debug.Log($"植物数据库中共有 {plantDatabase.Count} 个植物:");
        
        foreach (var entry in plantDatabase)
        {
            int plantId = entry.Key;
            Plant.PlantStage stage = entry.Value;
            
            string prerequisitesStr = "无";
            if (stage.prerequisitePlantIDs != null && stage.prerequisitePlantIDs.Count > 0)
            {
                List<string> prerequisiteWithWeights = new List<string>();
                for (int i = 0; i < stage.prerequisitePlantIDs.Count; i++)
                {
                    int prereqId = stage.prerequisitePlantIDs[i];
                    float weight = (stage.prerequisiteWeights != null && i < stage.prerequisiteWeights.Count) 
                                  ? stage.prerequisiteWeights[i] 
                                  : 1.0f;
                    prerequisiteWithWeights.Add($"{prereqId}(权重:{weight})");
                }
                prerequisitesStr = string.Join(", ", prerequisiteWithWeights);
            }
                
            // 添加更新植物信息
            string updatePlantsStr = "无";
            if (stage.updatePlantIDs != null && stage.updatePlantIDs.Count > 0)
            {
                List<string> updateWithWeights = new List<string>();
                for (int i = 0; i < stage.updatePlantIDs.Count; i++)
                {
                    int updateId = stage.updatePlantIDs[i];
                    float weight = (stage.updateWeights != null && i < stage.updateWeights.Count) 
                                  ? stage.updateWeights[i] 
                                  : 1.0f;
                    updateWithWeights.Add($"{updateId}(权重:{weight})");
                }
                updatePlantsStr = string.Join(", ", updateWithWeights);
            }
            
            string lightsInfo = "";
            if (stage.associatedLights != null)
            {
                for (int i = 0; i < stage.associatedLights.Count; i++)
                {
                    var light = stage.associatedLights[i];
                    lightsInfo += $"\n    光源 {i+1}: 大小={light.size}, 高度={light.lightHeight}, " +
                                 $"是否障碍={light.isObstacle}, 是否种子={light.isSeed}";
                }
            }
            
            Debug.Log($"植物ID: {plantId}\n" +
                     $"  名称: {stage.plantName}\n" +
                     $"  阶段: {stage.stageType}\n" +
                     $"  生长速率: {stage.growthRate}\n" +
                     $"  前置植物: {prerequisitesStr}\n" +
                     $"  更新植物: {updatePlantsStr}\n" +
                     $"  关联光源数量: {(stage.associatedLights != null ? stage.associatedLights.Count : 0)}{lightsInfo}");
        }
    }

    // 打印前置植物关系图用于调试
    public void PrintPrerequisiteGraph()
    {
        if (plantPrerequisites == null || plantPrerequisites.Count == 0)
        {
            Debug.Log("前置植物关系图为空");
            return;
        }
        
        Debug.Log($"前置植物关系图中共有 {plantPrerequisites.Count} 个植物节点:");
        
        foreach (var entry in plantPrerequisites)
        {
            int plantId = entry.Key;
            List<KeyValuePair<int, float>> prerequisites = entry.Value;
            
            // 获取植物名称
            string plantName = "未知";
            if (plantDatabase.TryGetValue(plantId, out Plant.PlantStage stage))
            {
                plantName = stage.plantName;
            }
            
            // 构建前置植物信息字符串
            string prerequisitesStr = "无";
            if (prerequisites != null && prerequisites.Count > 0)
            {
                List<string> prerequisiteWithWeights = new List<string>();
                foreach (var prereq in prerequisites)
                {
                    int prereqId = prereq.Key;
                    float weight = prereq.Value;
                    
                    // 获取前置植物名称
                    string prereqName = "未知";
                    if (plantDatabase.TryGetValue(prereqId, out Plant.PlantStage prereqStage))
                    {
                        prereqName = prereqStage.plantName;
                    }
                    
                    prerequisiteWithWeights.Add($"ID:{prereqId}({prereqName}, 权重:{weight})");
                }
                prerequisitesStr = string.Join(", ", prerequisiteWithWeights);
            }
            
            Debug.Log($"植物ID: {plantId} ({plantName})\n" +
                     $"  前置植物: {prerequisitesStr}");
        }
    }

    // 注册植物方法
    public void RegisterPlant(Plant plant)
    {
        if (!activePlants.Contains(plant))
        {
            activePlants.Add(plant);
            // Debug.Log($"注册植物: {plant.plantName} (ID: {plant.plantID})");
        }
    }
    
    // 注销植物方法
    public void UnregisterPlant(Plant plant)
    {
        if (activePlants.Contains(plant))
        {
            activePlants.Remove(plant);
            // Debug.Log($"注销植物: {plant.plantName} (ID: {plant.plantID})");
        }
    }

    // 检查植物ID是否在数据库中
    public bool IsPlantInDatabase(int plantId)
    {
        return plantDatabase.ContainsKey(plantId);
    }

    // 检查种子名称是否由有效的尺寸和生长速度枚举构成
    public bool IsValidSeedName(string seedName)
    {
        // 去除前后空白字符
        seedName = seedName.Trim();
        
        // 遍历所有尺寸枚举值，判断种子名字是否以该字符串开头
        foreach (string sizeStr in Enum.GetNames(typeof(SizeLevel)))
        {
            if (seedName.StartsWith(sizeStr, StringComparison.OrdinalIgnoreCase))
            {
                // 获取种子名称中除尺寸外的部分，作为生长速度字符串
                string growthRatePart = seedName.Substring(sizeStr.Length);
                
                // 尝试解析生长速度枚举（忽略大小写）
                if (Enum.TryParse(growthRatePart, true, out GrowthRateLevel _) &&
                    Enum.TryParse(sizeStr, true, out SizeLevel _))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
#endregion    
}