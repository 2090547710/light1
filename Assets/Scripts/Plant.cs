using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;

#region 枚举
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
#endregion


public class Plant : MonoBehaviour
{
    
    #region 字段和属性
    [Header("生长设置")]
    public List<Lighting> lightSources = new List<Lighting>(); // 多个光源组件
    public int currentStage;
    public int maxStages = 3;
    public bool isWithered;
    [SerializeField] private bool isImmortal = false; // 添加不会枯萎标记，默认为false
    private bool isInFireLight = false; // 添加是否在火光源范围内的标记
    private float lastFireHeight = 0f; // 记录上次火光源的高度值
    

    public bool IsWithered => isWithered;
    public bool IsImmortal => isImmortal; // 添加公共属性用于访问不会枯萎标记
    public bool IsInFireLight => isInFireLight; // 添加公共属性用于访问是否在火光源范围内

    [Header("阶段配置")]
    public List<PlantStage> growthStages = new List<PlantStage>();
    
    [Header("预制体")]
    public GameObject stageModelObject; // 用于存储当前阶段的预制体游戏对象

    [Header("生长特效")]
    public string growthEffectPrefabPath = "烟/烟"; // 生长特效预制体路径
    private GameObject growthEffectObject; // 存储生长特效对象的引用

    [Header("生长速度设置")]
    public float growthRate = 1.0f; // 生长速度
    public float witherRate = 1.0f; // 枯萎速度

    [Header("植物信息")]
    public int plantID; // 植物ID
    public string plantName; // 植物名称
    public List<int> prerequisitePlantIDs; // 前置植物ID列表
    public List<float> prerequisiteWeights; // 新增的权重集合
    public List<int> updatePlantIDs; // 更新植物ID列表 
    public List<float> updateWeights; // 更新权重列表
   
    [Header("UI显示")]
    private TextMesh nameText; // 用于显示植物名称的TextMesh组件
    public float textHeight = 1.5f; // 文本悬浮高度
    public Color textColor = Color.white; // 文本颜色
    public float textSize = 1.0f; // 文本大小

    // 添加字段存储回调
    private Action onEffectComplete;

    public float growthTimer = 0f; // 生长计时器
    public float witherTimer = 0f; // 枯萎计时器

    // 新增火光源检测相关字段
    private float fireCheckTimer = 0f; // 火光源检测计时器
    #endregion
   
    #region Unity生命周期方法
    void Start()
    {
         //阶段0会直接生长为种子
        if(currentStage==0){
            plantID=0;
            plantName="Seed";
            lightSources.Clear();
            // 添加碰撞检测逻辑
            if (currentStage < growthStages.Count && HasCollisionWithOtherPlants())
            {
                LightingManager.tree.Remove(gameObject);
                PlantManager.Instance.UnregisterPlant(this);
                Destroy(gameObject);
                Debug.Log("无法播种，检测到与其他植物的碰撞");
                return;
            }
            if (growthStages.Count > 0 && currentStage <= growthStages.Count)
            {
                Grow();
            }
         }
        // 创建并设置名称显示
        // CreateNameDisplay();
    }
    
    void Update()
    {
        // 更新名称显示
        if (nameText != null)
        {
            // 如果植物已枯萎，在名称后添加"已枯萎"标记
            string displayName = isWithered ? plantName + " (已枯萎)" : plantName;
            
            // 只有当显示名称变化时才更新
            if (nameText.text != displayName)
            {
                nameText.text = displayName;
            }
        }
        
        // 使用定时器每1秒调用一次CheckIfInFireLight
        fireCheckTimer += Time.deltaTime;
        if (fireCheckTimer >= 1.0f)
        {
            CheckIfInFireLight();
            fireCheckTimer = 0f;
        }

        // 生长计时器
        if (currentStage > 0 && growthRate > 0 && currentStage<3)
        {
            growthTimer += Time.deltaTime;
            float growthInterval = 60f / growthRate; // 每分钟调用Grow的次数转换为时间间隔
            
            if (growthTimer >= growthInterval)
            {
                if(currentStage==1){
                    TryBloom();
                }else if(currentStage==2){
                    TryFruit();
                }
                growthTimer = 0f;
            }
        }
        
        // 枯萎计时器
        if (currentStage > 1 && witherRate > 0 && !isWithered)
        {
            witherTimer += Time.deltaTime;
            float witherInterval = 60f / witherRate; // 每分钟调用TryWither的次数转换为时间间隔
            
            if (witherTimer >= witherInterval)
            {
                TryWither();
                witherTimer = 0f;
            }
        }
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
            // 从活跃植物列表中移除
            PlantManager.Instance.UnregisterPlant(this);
        }
    }
    #endregion
   
    #region UI相关方法
    // 创建名称显示
    protected void CreateNameDisplay()
    {
        // 创建一个子物体用于显示名称
        GameObject textObj = new GameObject("NameDisplay");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = new Vector3(0, textHeight, 0);
        
        // 添加TextMesh组件
        nameText = textObj.AddComponent<TextMesh>();
        nameText.text = plantName;
        nameText.fontSize = 90;
        nameText.characterSize = textSize * 0.1f;
        nameText.alignment = TextAlignment.Center;
        nameText.anchor = TextAnchor.LowerCenter;
        nameText.color = textColor;
        
        // 确保文本始终面向摄像机
        textObj.AddComponent<Billboard>();
    }
    #endregion
   
    #region 生长和枯萎方法
    public virtual void Grow(bool useAnimation = true)
    {
        if (currentStage >= maxStages) return;
        
        //更新UpdatePlantCounts
        if (currentStage >= 2 && PlantManager.Instance.IsPlantInDatabase(plantID)) {
            PlantManager.Instance.UpdatePlantCounts(this, false);
        }
        
        // 获取下一阶段的配置
        PlantStage nextStage = growthStages[currentStage];
        
        if (currentStage>= 2 && useAnimation)
        {
            // 查找下一阶段中非障碍物光源的最大size值
            float maxNewSize = 0f;
            foreach (var lightData in nextStage.associatedLights)
            {
                if (!lightData.isObstacle && lightData.size > maxNewSize)
                {
                    maxNewSize = lightData.size;
                }
            }

            // 使用当前非障碍物光源的平均size作为起始值
            float avgCurrentSize = 0f;
            List<Lighting> nonObstacleLights = lightSources.Where(l => !l.isObstacle).ToList();
            if (nonObstacleLights.Count > 0)
            {
                avgCurrentSize = nonObstacleLights.Average(l => l.size);
            }
                
             // 如果有size变化且有非障碍物光源，执行动画
            if (nonObstacleLights.Count > 0 && Mathf.Abs(maxNewSize - avgCurrentSize) > 0.01f)
            {
                // 使用协程执行大小变化动画，完成后应用新阶段
                StartCoroutine(GrowWithAnimation(maxNewSize));
                return; // 结束方法，后续逻辑由协程完成
            }
            
        }
        // 如果不使用动画或没有合适的光源变化，直接执行常规生长
        PerformGrow();
    }

    // 带动画效果的生长协程
    private IEnumerator GrowWithAnimation(float targetSize)
    {
        // 执行大小变化动画
        yield return StartCoroutine(AnimateLightSizeChange(targetSize, 1.0f, null, 0.05f));
        
        // 动画结束后，执行实际的生长
        PerformGrow();
    }

    // 实际执行生长的逻辑
    private void PerformGrow()
    {
        // 禁用并移除所有现有光源组件
        lightSources.ForEach(l => {
            l.RemoveLighting();
            LightingManager.tree.Remove(l.gameObject);
            Destroy(l);
        });
        lightSources.Clear();
        
        ApplyStageConfig(currentStage);
        currentStage++;
        
        //更新UpdatePlantCounts
        if (currentStage >= 2 && PlantManager.Instance.IsPlantInDatabase(plantID)) {
            PlantManager.Instance.UpdatePlantCounts(this, true);
        }
        
        // 植物生长后，检查玩家是否被卡住
        CheckAndTeleportPlayerIfStuck();
    }

    protected void ApplyStageConfig(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= maxStages)
        {
            Debug.LogWarning($"无效的生长阶段索引: {stageIndex}");
            return;
        }

        var stage = growthStages[stageIndex];
        
        // 更新植物信息
        plantID = stage.plantID;
        plantName = stage.plantName;
        growthRate = stage.growthRate;
        witherRate = stage.witherRate;
        prerequisitePlantIDs = stage.prerequisitePlantIDs;
        prerequisiteWeights = stage.prerequisiteWeights;
        updatePlantIDs = stage.updatePlantIDs;
        updateWeights = stage.updateWeights;
        
        // 更新名称显示，考虑枯萎状态
        if (nameText != null)
        {
            nameText.text = isWithered ? plantName + " (已枯萎)" : plantName;
        }
        
        

        // 如果存在当前的模型对象，先将其销毁
        if (stageModelObject != null)
        {
            Destroy(stageModelObject);
            stageModelObject = null;
        }
        
        // 禁用并移除所有现有光源组件
        lightSources.ForEach(l => {
            l.RemoveLighting();
            LightingManager.tree.Remove(l.gameObject);
            Destroy(l);
        });
        lightSources.Clear();

        
        
        LoadPrefabAndCreateLights(stage);
    }

    // 加载预制体和创建光源的方法
    private void LoadPrefabAndCreateLights(PlantStage stage)
    {
        // 首先检查对象是否已被销毁
        if (this == null || gameObject == null)
        {
            return;
        }
        
        // 根据预制体路径加载并创建预制体
        if (!string.IsNullOrEmpty(stage.prefabPath))
        {
            GameObject prefab = Resources.Load<GameObject>(stage.prefabPath);
            if (prefab != null)
            {
                // 实例化预制体作为当前物体的子物体
                stageModelObject = Instantiate(prefab, transform);
                stageModelObject.transform.localPosition = Vector3.zero;
                stageModelObject.transform.localRotation = Quaternion.identity;
            }
            else
            {
                Debug.LogWarning($"无法加载预制体: {stage.prefabPath}");
            }
        }
        
        // 根据数据创建并初始化光源组件
        stage.associatedLights.ForEach(data => {
            // 再次检查对象是否已被销毁
            if (this != null && gameObject != null)
            {
                var newLight = gameObject.AddComponent<Lighting>();
                
                // 如果不是障碍物光源，计算朝向最远终点的旋转角度
                if (!data.isObstacle)
                {
                    data.rotation = CalculateLightRotation();
                }
                
                newLight.InitializeFromData(data);
                lightSources.Add(newLight); // 添加到光源列表
            }
        });
        
        if (this != null && gameObject != null)
        {
            LightingManager.tree.Insert(gameObject);
            LightingManager.UpdateDirtyLights(); // 更新所有脏标记的光源
        }
    }

    // 计算光源朝向最远终点的旋转角度
    private float CalculateLightRotation()
    {
        if (GameManager.Instance == null || GameManager.Instance.beginPoint == null || GameManager.Instance.endPoints.Count == 0)
        {
            return 0f;
        }

        Vector3 plantPosition = transform.position;
        Vector3 beginPosition = GameManager.Instance.beginPoint.transform.position;
        
        // 找到距离起点最远的终点
        GameObject farthestEndPoint = null;
        float maxDistance = 0f;
        
        foreach (var endPoint in GameManager.Instance.endPoints)
        {
            if (endPoint == null) continue;
            
            float distance = Vector3.Distance(beginPosition, endPoint.transform.position);
            if (distance > maxDistance)
            {
                maxDistance = distance;
                farthestEndPoint = endPoint;
            }
        }
        
        if (farthestEndPoint == null)
        {
            return 0f;
        }
        
        // 计算从植物位置到最远终点的方向向量（在XZ平面上）
        Vector3 direction = farthestEndPoint.transform.position - plantPosition;
        direction.y = 0f; // 确保只在XZ平面上计算
        
        // 计算旋转角度（以度为单位）
        float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
        
        return -angle;
    }

    public void TryWither()
    {
        // 如果植物已枯萎或被标记为不会枯萎，则直接返回
        if (isWithered || isImmortal)
        {
            return;
        }
        
        // 计算区域内亮度情况及存活概率
        float brightnessRatio = CalculateBrightnessRatio();
        
        // 根据概率决定是否枯萎
        if (UnityEngine.Random.value < 1 - brightnessRatio)
        {
            Debug.Log($"植物 {plantName} 尝试枯萎成功。亮度比例: {brightnessRatio:F2}, 枯萎概率: {1-brightnessRatio:F2}");
            MessageManager.instance.SendMessage("T - T", transform, MessageType.Auto, 3f);
            Wither();
        }
        else
        {
            Debug.Log($"植物 {plantName} 尝试枯萎失败。亮度比例: {brightnessRatio:F2}, 枯萎概率: {1-brightnessRatio:F2}");
        }
    }

    public void Wither()
    {
        isWithered = true;

        // 更新名称显示
        if (nameText != null)
        {
            nameText.text = plantName + " (已枯萎)";
        }
        
        // 除了currentStage == 1以外的所有情况，移除所有非障碍物光照组件
        if (currentStage != 1)
        {
            // 禁用并移除除了isObstacle的所有现有光源组件
            foreach (var light in lightSources.ToList())
            {
                if (!light.isObstacle)
                {
                    light.RemoveLighting();
                    lightSources.Remove(light);
                    Destroy(light);
                }
            }
        }
    }

    // 添加设置不会枯萎标记的方法
    public void SetImmortal(bool immortal)
    {
        isImmortal = immortal;
    }
    #endregion

    #region 开花和结果方法
    public virtual void TryBloom()
    {          
        // 计算区域内亮度情况及开花概率
        float brightnessRatio = CalculateBrightnessRatio();

        // 根据概率决定是否开花
        if (UnityEngine.Random.value < brightnessRatio)
        {
             if (currentStage < growthStages.Count && HasCollisionWithOtherPlants())
            {
                Debug.Log($"植物 {plantName} 无法生长，检测到与其他植物的碰撞");
                return;
            }
            
            Debug.Log($"种子成功开花！亮度比例: {brightnessRatio:F2}, 开花概率: {brightnessRatio:F2}");
            MessageManager.instance.SendMessage("(̳ˆ_  ̫ _ˆ ̳)", transform, MessageType.Auto, 3f);

            // 尝试通过植物名称获取更新后的植物阶段
            PlantStage updatedStage = PlantManager.Instance.GetPlantStageBySeedFromName(plantName);
            if (updatedStage != null) {
                growthStages.Add(updatedStage);
                maxStages = growthStages.Count;
                Debug.Log($"植物已更新为: {updatedStage.plantName} (ID: {updatedStage.plantID})");
            }
            
            Grow(false);
        }
        else
        {
            Debug.Log($"种子尝试开花失败。亮度比例: {brightnessRatio:F2}, 开花概率: {brightnessRatio:F2}");
        }
    }

    public void TryFruit()
    {

        // 先尝试获取更新后的植物阶段
        PlantStage updatedStage = PlantManager.Instance.GetUpdatedPlantStage(growthStages[currentStage-1]);
        if (updatedStage == null) {
            // 如果没有可用的更新植物阶段，直接返回
            return;
        }
                
        // 计算区域内亮度情况及结果概率
        float brightnessRatio = CalculateBrightnessRatio();

        // 根据概率决定是否结果
        if (UnityEngine.Random.value < brightnessRatio)
        {
            if (currentStage < growthStages.Count && HasCollisionWithOtherPlants())
            {
                Debug.Log($"植物 {plantName} 无法生长，检测到与其他植物的碰撞");
                return;
            }
            Debug.Log($"花朵成功结果！亮度比例: {brightnessRatio:F2}, 结果概率: {brightnessRatio:F2}");
            MessageManager.instance.SendMessage("(̳ˆ_  ̫ _ˆ ̳)", transform, MessageType.Auto, 3f);
            // 添加更新后的植物阶段
            growthStages.Add(updatedStage);
            maxStages = growthStages.Count;
            Debug.Log($"植物已更新为: {updatedStage.plantName} (ID: {updatedStage.plantID})");
            
            Grow(false);
        }
        else
        {
            Debug.Log($"花朵尝试结果失败。亮度比例: {brightnessRatio:F2}, 结果概率: {brightnessRatio:F2}");
        }
    }

    public float CalculateBrightnessRatio()
    {
        if (LightingManager.tree == null)
        {
            Debug.LogWarning("四叉树未初始化，无法计算亮度比例");
            return 0;
        }
        
        // 获取植物上所有非障碍物光源
        List<Lighting> nonObstacleLights = lightSources.Where(l => !l.isObstacle).ToList();
        
        if (nonObstacleLights.Count == 0)
        {
            Debug.LogWarning("没有非障碍物光源，无法计算光照比例");
            return 0;
        }
        
        // 使用四叉树计算光照比例并返回
        return LightingManager.tree.CalculateLightingRatio(nonObstacleLights);
    }
    
    #endregion

    #region 碰撞检测方法
    // 修改碰撞检测方法
    protected bool HasCollisionWithOtherPlants()
    {
        // 如果当前阶段无效或没有下一个阶段的数据，则无法检测碰撞
        if (currentStage >= growthStages.Count)
        {
            return false;
        }

        // 获取当前植物下一阶段的配置
        PlantStage nextStage = growthStages[currentStage];
        
        // 筛选出下一阶段中标记为障碍物的光源和种子光源
        List<LightingData> nextStageObstacleLights = nextStage.associatedLights
            .Where(light => light.isObstacle)
            .ToList();
        
        List<LightingData> nextStageSeedLights = nextStage.associatedLights
            .Where(light => light.isSeed)
            .ToList();
        
        // 情况2：自身无障碍物光源也无isSeed光源，直接返回false
        if (nextStageObstacleLights.Count == 0 && nextStageSeedLights.Count == 0)
        {
            return false;
        }
        
        // 获取当前位置
        Vector3 currentPosition = transform.position;
        
        // 获取所有活跃的障碍光源和种子光源
        List<Lighting> obstacleActiveLights = LightingManager.activeLights
            .Where(light => light.isObstacle)
            .ToList();
        
        List<Lighting> seedActiveLights = LightingManager.activeLights
            .Where(light => light.isSeed)
            .ToList();
        
        // 记录当前位置已经在哪些障碍光源范围内
        HashSet<int> overlapLightIds = new HashSet<int>();
        foreach (Lighting obstacleLight in obstacleActiveLights)
        {
            Bounds obstacleBounds = obstacleLight.GetWorldBounds();
            
            // 检查当前位置是否在该光源范围内
            if (IsPointInXZBounds(currentPosition, obstacleBounds))
            {
                // 记录当前位置已经存在的障碍光源ID
                overlapLightIds.Add(obstacleLight.GetInstanceID());
            }
        }
        
        // 情况1：自身有障碍物光源或者有isSeed光源
        
        // 情况1.1和1.2：处理障碍物光源的碰撞
        // 无论自身是否有障碍物光源或种子光源，都需要检查与其他障碍物光源的碰撞
        if (nextStageObstacleLights.Count > 0 || nextStageSeedLights.Count > 0)
        {
            // 处理自身障碍物光源与其他障碍物光源的碰撞
            foreach (LightingData nextLight in nextStageObstacleLights)
            {
                // 创建下一阶段光源的边界
                Vector3 center = transform.position;
                Vector3 size = new Vector3(nextLight.size, nextLight.lightHeight, nextLight.size);
                Bounds nextLightBounds = new Bounds(center, size);
                
                // 检查与所有活跃障碍光源的碰撞
                foreach (Lighting obstacleLight in obstacleActiveLights)
                {
                    // 如果当前位置已经在该障碍光源内，且该光源不属于任何活跃植物，则跳过这个障碍光源的检测
                    if (overlapLightIds.Contains(obstacleLight.GetInstanceID()) && 
                        !IsLightBelongToActivePlant(obstacleLight))
                    {
                        continue;
                    }
                    
                    Bounds obstacleBounds = obstacleLight.GetWorldBounds();
                    
                    // 情况1.1：对于不在activePlants中植物的lightSources中的障碍物光源
                    // 矩形相交但坐标在障碍物光源内不算碰撞
                    if (!IsLightBelongToActivePlant(obstacleLight))
                    {
                        if (IsOverlappingOnXZPlane(nextLightBounds, obstacleBounds) && 
                            !IsPointInXZBounds(currentPosition, obstacleBounds))
                        {
                            return true; // 发现碰撞
                        }
                    }
                    // 情况1.2：对于activePlants中植物的lightSources中的障碍物光源
                    // 矩形相交就算碰撞，但需要排除自身
                    else
                    {
                        // 检查该光源是否属于自身
                        if (!lightSources.Contains(obstacleLight))
                        {
                            if (IsOverlappingOnXZPlane(nextLightBounds, obstacleBounds))
                            {
                                return true; // 发现碰撞
                            }
                        }
                    }
                }
            }
            
            // 处理自身种子光源与障碍物光源的碰撞
            foreach (LightingData nextSeedLight in nextStageSeedLights)
            {
                // 创建下一阶段种子光源的边界
                Vector3 center = transform.position;
                Vector3 size = new Vector3(nextSeedLight.size, nextSeedLight.lightHeight, nextSeedLight.size);
                Bounds nextSeedBounds = new Bounds(center, size);
                
                // 检查与所有活跃障碍光源的碰撞
                foreach (Lighting obstacleLight in obstacleActiveLights)
                {
                    // 如果当前位置已经在该障碍光源内，且该光源不属于任何活跃植物，则跳过这个障碍光源的检测
                    if (overlapLightIds.Contains(obstacleLight.GetInstanceID()) && 
                        !IsLightBelongToActivePlant(obstacleLight))
                    {
                        continue;
                    }
                    
                    Bounds obstacleBounds = obstacleLight.GetWorldBounds();
                    
                    // 情况1.1：对于不在activePlants中植物的lightSources中的障碍物光源
                    // 矩形相交但坐标在障碍物光源内不算碰撞
                    if (!IsLightBelongToActivePlant(obstacleLight))
                    {
                        if (IsOverlappingOnXZPlane(nextSeedBounds, obstacleBounds) && 
                            !IsPointInXZBounds(currentPosition, obstacleBounds))
                        {
                            return true; // 发现碰撞
                        }
                    }
                    // 情况1.2：对于activePlants中植物的lightSources中的障碍物光源
                    // 矩形相交就算碰撞，但需要排除自身
                    else
                    {
                        // 检查该光源是否属于自身
                        if (!lightSources.Contains(obstacleLight))
                        {
                            if (IsOverlappingOnXZPlane(nextSeedBounds, obstacleBounds))
                            {
                                return true; // 发现碰撞
                            }
                        }
                    }
                }
            }
        }
        
        // 情况1.3：处理种子光源的碰撞
        if (nextStageSeedLights.Count > 0)
        {
            // 情况1.32：自身有isSeed光源，与其他种子光源矩形相交就算碰撞
            foreach (LightingData nextSeedLight in nextStageSeedLights)
            {
                // 创建下一阶段种子光源的边界
                Vector3 center = transform.position;
                Vector3 size = new Vector3(nextSeedLight.size, nextSeedLight.lightHeight, nextSeedLight.size);
                Bounds nextSeedBounds = new Bounds(center, size);
                
                // 检查与所有活跃种子光源的碰撞，排除自身的光源
                foreach (Lighting seedLight in seedActiveLights)
                {
                    // 跳过自身的光源
                    if (lightSources.Contains(seedLight))
                    {
                        continue;
                    }
                    
                    Bounds seedBounds = seedLight.GetWorldBounds();
                    
                    // 矩形相交就算碰撞
                    if (IsOverlappingOnXZPlane(nextSeedBounds, seedBounds))
                    {
                        return true; // 发现碰撞
                    }
                }
            }
        }
        else
        {
            // 情况1.31：自身无isSeed光源，认为不会与其他种子光源发生碰撞
            // 不做任何处理，直接通过
        }
        
        return false; // 没有碰撞
    }

    // 添加方法：检查光源是否属于活跃植物
    private bool IsLightBelongToActivePlant(Lighting light)
    {
        foreach (Plant plant in PlantManager.Instance.activePlants)
        {
            if (plant.lightSources.Contains(light))
            {
                return true;
            }
        }
        return false;
    }

    // 修改碰撞检测方法，考虑旋转
    private bool IsOverlappingOnXZPlane(Bounds a, Bounds b)
    {
        // 尝试获取两个边界对应的Lighting组件
        Lighting lightA = null;
        Lighting lightB = null;
        
        // 查找a对应的光照组件
        foreach (var light in lightSources)
        {
            if (light.GetWorldBounds() == a)
            {
                lightA = light;
                break;
            }
        }
        
        // 查找b对应的光照组件
        foreach (var light in LightingManager.activeLights)
        {
            if (light.GetWorldBounds() == b)
            {
                lightB = light;
                break;
            }
        }
        
        float rotationA = lightA != null ? lightA.rotation * Mathf.Deg2Rad : 0f;
        float rotationB = lightB != null ? lightB.rotation * Mathf.Deg2Rad : 0f;
        
        // 使用分离轴定理检测旋转矩形碰撞
        return AreRotatedRectsOverlapping(
            new Vector2(a.center.x, a.center.z), new Vector2(a.size.x, a.size.z), rotationA,
            new Vector2(b.center.x, b.center.z), new Vector2(b.size.x, b.size.z), rotationB
        );
    }

    // 新增检测两个旋转矩形碰撞的辅助方法
    private bool AreRotatedRectsOverlapping(Vector2 centerA, Vector2 sizeA, float rotationA, 
                                           Vector2 centerB, Vector2 sizeB, float rotationB)
    {
        // 计算两个矩形的四个顶点
        Vector2[] cornersA = GetRotatedRectCorners(centerA, sizeA, rotationA);
        Vector2[] cornersB = GetRotatedRectCorners(centerB, sizeB, rotationB);
        
        // 分离轴定理检测
        // 检查A的两个轴
        Vector2 axisA1 = (cornersA[1] - cornersA[0]).normalized;
        Vector2 axisA2 = (cornersA[3] - cornersA[0]).normalized;
        
        if (!OverlapOnAxis(cornersA, cornersB, axisA1)) return false;
        if (!OverlapOnAxis(cornersA, cornersB, axisA2)) return false;
        
        // 检查B的两个轴
        Vector2 axisB1 = (cornersB[1] - cornersB[0]).normalized;
        Vector2 axisB2 = (cornersB[3] - cornersB[0]).normalized;
        
        if (!OverlapOnAxis(cornersA, cornersB, axisB1)) return false;
        if (!OverlapOnAxis(cornersA, cornersB, axisB2)) return false;
        
        // 所有轴都有重叠，表示矩形相交
        return true;
    }

    // 计算旋转矩形的四个顶点
    private Vector2[] GetRotatedRectCorners(Vector2 center, Vector2 size, float rotation)
    {
        Vector2[] corners = new Vector2[4];
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        
        // 计算旋转后的四个角
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        
        corners[0] = center + new Vector2(cos * -halfWidth - sin * -halfHeight, sin * -halfWidth + cos * -halfHeight);
        corners[1] = center + new Vector2(cos * halfWidth - sin * -halfHeight, sin * halfWidth + cos * -halfHeight);
        corners[2] = center + new Vector2(cos * halfWidth - sin * halfHeight, sin * halfWidth + cos * halfHeight);
        corners[3] = center + new Vector2(cos * -halfWidth - sin * halfHeight, sin * -halfWidth + cos * halfHeight);
        
        return corners;
    }

    // 检查两组顶点在某一轴上是否重叠
    private bool OverlapOnAxis(Vector2[] cornersA, Vector2[] cornersB, Vector2 axis)
    {
        // 计算A投影的最小和最大值
        float minA = float.MaxValue;
        float maxA = float.MinValue;
        
        foreach (Vector2 corner in cornersA)
        {
            float projection = Vector2.Dot(corner, axis);
            minA = Mathf.Min(minA, projection);
            maxA = Mathf.Max(maxA, projection);
        }
        
        // 计算B投影的最小和最大值
        float minB = float.MaxValue;
        float maxB = float.MinValue;
        
        foreach (Vector2 corner in cornersB)
        {
            float projection = Vector2.Dot(corner, axis);
            minB = Mathf.Min(minB, projection);
            maxB = Mathf.Max(maxB, projection);
        }
        
        // 检查投影是否重叠
        return maxA >= minB && maxB >= minA;
    }

    // 修改检测点是否在边界的XZ平面投影内的方法，考虑旋转
    private bool IsPointInXZBounds(Vector3 point, Bounds bounds)
    {
        // 尝试获取边界对应的Lighting组件
        Lighting light = null;
        foreach (var l in LightingManager.activeLights)
        {
            if (l.GetWorldBounds() == bounds)
            {
                light = l;
                break;
            }
        }
        
        // 如果找到对应的光照组件，使用其旋转检测方法
        if (light != null)
        {
            return light.IsPointInRotatedBounds(point);
        }
        
        // 如果没有找到，使用默认的AABB检测
        bool insideX = Mathf.Abs(point.x - bounds.center.x) <= bounds.size.x * 0.5f;
        bool insideZ = Mathf.Abs(point.z - bounds.center.z) <= bounds.size.z * 0.5f;
        
        return insideX && insideZ;
    }
    #endregion

    #region 玩家碰撞处理
    // 检查玩家是否被卡住，如果被卡住则瞬移到安全位置
    protected void CheckAndTeleportPlayerIfStuck()
    {
        // 查找场景中的玩家对象
        PlayerPathfinding player = FindObjectOfType<PlayerPathfinding>();
        if (player == null) return;
        
        // 获取玩家位置
        Vector3 playerPosition = player.transform.position;
        
        // 检查玩家是否在植物的障碍物光源范围内
        bool playerStuck = false;
        
        foreach (var light in lightSources)
        {
            if (light.isObstacle || light.isSeed)
            {
                // 获取光源的世界边界
                Bounds lightBounds = light.GetWorldBounds();
                
                // 检查玩家是否在光源范围内
                if (IsPointInXZBounds(playerPosition, lightBounds))
                {
                    playerStuck = true;
                    break;
                }
            }
        }
        
        // 如果玩家被卡住，尝试瞬移到安全位置
        if (playerStuck)
        {
            TeleportPlayerToSafePosition(player);
        }
    }

    // 将玩家瞬移到安全位置
    private void TeleportPlayerToSafePosition(PlayerPathfinding player)
    {
        // 搜索半径，可以根据需要调整
        float searchRadius = 5.0f;
        
        // 获取四叉树
        QuadTree quadTree = PlayerPathfinding.quadTree;
        if (quadTree == null) return;
        
        // 获取玩家当前位置
        Vector3 playerPosition = player.transform.position;
        
        // 获取当前位置周围的所有叶子节点
        var nearbyNodes = quadTree.GetNeighborLeafNodes(playerPosition, searchRadius)
            .Where(node => node.IsIlluminated) // 只选择被照亮的节点
            .OrderBy(node => Vector3.Distance(
                new Vector3(node.Center.x, playerPosition.y, node.Center.y), 
                playerPosition)) // 按距离排序
            .ToList();
        
        foreach (var node in nearbyNodes)
        {
            // 创建潜在的安全位置
            Vector3 potentialPosition = new Vector3(node.Center.x, playerPosition.y, node.Center.y);
            
            // 检查该位置是否安全（没有植物障碍物光源）
            if (IsSafePosition(potentialPosition))
            {
                // 瞬移玩家到安全位置
                player.transform.position = potentialPosition;
                
                // 更新四叉树中的位置
                quadTree.Remove(player.gameObject);
                player.InsertToQuadTree();
                
                return;
            }
        }
        
        Debug.LogWarning($"无法找到安全位置瞬移玩家！植物: {plantName}");
    }

    // 检查位置是否安全（没有植物障碍物光源）
    private bool IsSafePosition(Vector3 position)
    {
        // 检查位置是否被照亮
        QuadTree quadTree = PlayerPathfinding.quadTree;
        if (quadTree == null) return false;
        
        if (!quadTree.IsPositionIlluminated(position))
        {
            return false;
        }
        
        // 检查所有植物的障碍物光源
        foreach (var plant in PlantManager.Instance.activePlants)
        {
            foreach (var light in plant.lightSources)
            {
                if (light.isObstacle)
                {
                    // 获取光源的世界边界
                    Bounds lightBounds = light.GetWorldBounds();
                    
                    // 检查位置是否在光源范围内
                    if (IsPointInXZBounds(position, lightBounds))
                    {
                        // 该位置有植物的障碍物光源，不安全
                        return false;
                    }
                }
            }
        }
        
        return true;
    }
    #endregion

    #region 内部类-植物阶段
    [System.Serializable]
    public class PlantStage
    {
        public StageType stageType;//阶段类型
        public List<LightingData> associatedLights; // 改为存储光照数据
        public int plantID; // 植物ID
        public string plantName; // 植物名称
        public string plantDescription; // 新增：植物介绍
        public string previewImagePath; // 新增：预览图路径
        public float growthRate; // 生长速度
        public float witherRate; // 新增枯萎速度参数
        public List<int> prerequisitePlantIDs; // 前置植物ID列表
        public List<float> prerequisiteWeights; // 新增的权重集合
        public List<int> updatePlantIDs; // 更新植物ID列表 
        public List<float> updateWeights; // 更新权重列表
        public string prefabPath; // 新增预制体路径字段
    }
    #endregion

    #region 火光源检测方法
    // 检查植物是否在火光源范围内
    public void CheckIfInFireLight()
    {
        // 如果是火植物本身，不需要检查
        if (this is Fire)
        {
            return;
        }
        
        // 获取所有活跃的火光源
        List<Lighting> fireLights = new List<Lighting>();
        
        // 从 PlantManager 获取所有 Fire 类型的植物
        foreach (Plant plant in PlantManager.Instance.activePlants)
        {
            if (plant is Fire && !plant.isWithered)
            {
                // 将火植物的所有光源添加到列表中
                fireLights.AddRange(plant.lightSources);
            }
        }
        
        // 如果没有火光源，重置标记并返回
        if (fireLights.Count == 0)
        {
            if (isInFireLight)
            {
                isInFireLight = false;
                lastFireHeight = 0f;
                // 将growthRate重置为当前阶段的默认值
                if (currentStage > 0 && currentStage <= growthStages.Count)
                {
                    growthRate = growthStages[currentStage - 1].growthRate;
                }
            }
            return;
        }
        
        // 获取植物当前位置
        Vector3 plantPosition = transform.position;
        
        // 使用QuadTree计算火光源的高度值
        float fireHeight = LightingManager.tree.GetFireLightHeightAtPosition(plantPosition, fireLights);
        
        // 检查火光高度的变化
        bool statusChanged = (fireHeight > 0) != isInFireLight || Mathf.Abs(fireHeight - lastFireHeight) > 0.1f;
        
        // 只有当状态改变时才调整生长速度
        if (statusChanged)
        {
            // 更新标记和上次高度值
            isInFireLight = fireHeight > 0;
            lastFireHeight = fireHeight;
            
            if (isInFireLight)
            {
                // 根据高度值调整生长速度：基础倍数2 + 高度值
                float baseGrowthRate = currentStage > 0 && currentStage <= growthStages.Count 
                    ? growthStages[currentStage - 1].growthRate : 1.0f;
                growthRate = baseGrowthRate * (2f + fireHeight);
                // 限制在最大值60
                growthRate = Mathf.Min(growthRate, 60f);
                
                Debug.Log($"植物 {plantName} 进入火光范围，生长速度调整为: {growthRate:F2}");
            }
            else
            {
                // 将growthRate设置为currentStage的growthRate
                if (currentStage > 0 && currentStage <= growthStages.Count)
                {
                    growthRate = growthStages[currentStage - 1].growthRate;
                    Debug.Log($"植物 {plantName} 离开火光范围，生长速度重置为: {growthRate:F2}");
                }
            }
        }
    }
    #endregion
    
    #region 植物数据存档与读取
    // 获取植物存档数据
    public virtual PlantSaveData GetSaveData()
    {
        PlantSaveData saveData = new PlantSaveData();
               
        // 创建可序列化的植物阶段列表
        saveData.growthStages = new List<SerializablePlantStage>();
        for (int i = 0; i < growthStages.Count; i++)
        {
            if (i == currentStage - 1 && currentStage > 0)
            {
                // 对于当前阶段，使用CreateStageFromCurrentLightSources方法获取最新状态
                PlantStage updatedStage = CreateStageFromCurrentLightSources();
                if (updatedStage != null)
                {
                    saveData.growthStages.Add(new SerializablePlantStage(updatedStage));
                }
                else
                {
                    // 如果创建失败，回退到使用原始阶段
                    saveData.growthStages.Add(new SerializablePlantStage(growthStages[i]));
                }
            }
            else
            {
                // 其他阶段直接使用原始数据
                saveData.growthStages.Add(new SerializablePlantStage(growthStages[i]));
            }
        }
        
        // 其他属性保持不变
        saveData.currentStage = currentStage;
        saveData.maxStages = maxStages;
        saveData.isWithered = isWithered;
        saveData.isImmortal = isImmortal;
        
        // 保存位置和旋转
        saveData.position = new SerializableVector3(transform.position);
        saveData.rotation = new SerializableQuaternion(transform.rotation);
        saveData.scale = new SerializableVector3(transform.localScale);
        
        // 保存植物标识信息
        saveData.plantName = plantName;
        saveData.plantID = plantID;
        
        return saveData;
    }

    // 从存档数据创建植物
    public static Plant CreateFromSaveData(PlantSaveData saveData)
    {
        // 创建一个空物体作为植物对象
        GameObject plantObj = new GameObject(saveData.plantName);
        
        // 添加Plant组件
        Plant plant;
        if (saveData.plantType == "Fire")
        {
            plant = plantObj.AddComponent<Fire>();
        }
        else
        {
            plant = plantObj.AddComponent<Plant>();
        }

        // 设置位置和旋转
        plantObj.transform.position = saveData.position.ToVector3();
        plantObj.transform.rotation = saveData.rotation.ToQuaternion();
        plantObj.transform.localScale = saveData.scale.ToVector3();

        // 设置基本属性
        plant.growthStages = saveData.ConvertToPlantStages();
        plant.maxStages = saveData.maxStages;
        plant.plantName = saveData.plantName;
        plant.plantID = saveData.plantID;

        // 设置不会枯萎标记
        plant.SetImmortal(saveData.isImmortal);

        // 应用当前生长阶段
        int stageIndex = saveData.currentStage - 1;
        if (stageIndex >= 0 && stageIndex < plant.growthStages.Count)
        {
            plant.ApplyStageConfig(stageIndex);
            plant.currentStage = saveData.currentStage;
        }

        // 如果植物已枯萎，调用枯萎方法
        if (saveData.isWithered)
        {
            plant.Wither();
        }

        return plant;
    }
#endregion

    // 新增方法：从当前活跃的光源组件获取LightingData
    public List<LightingData> GetLightingDataFromLightSources()
    {
        List<LightingData> lightingDataList = new List<LightingData>();
        
        foreach (Lighting light in lightSources)
        {
            // 创建新的LightingData
            LightingData data = new LightingData(
                size: light.size,
                isObstacle: light.isObstacle,
                isSeed: light.isSeed,
                lightHeight: light.lightHeight,
                heightMap: light.heightMap,
                edgeHeightMap: light.edgeHeightMap, // 新增边缘高度图
                rotation: light.rotation
            );
            
            lightingDataList.Add(data);
        }
        
        return lightingDataList;
    }

    // 新增方法：基于当前光源创建临时阶段
    public PlantStage CreateStageFromCurrentLightSources()
    {
        // 确保当前阶段索引有效
        if (currentStage <= 0 || currentStage > growthStages.Count)
        {
            Debug.LogWarning($"无效的当前阶段索引: {currentStage}");
            return null;
        }
        
        // 获取当前阶段
        PlantStage currentStageData = growthStages[currentStage - 1];
        
        // 创建临时阶段，复制当前阶段的所有属性
        PlantStage tempStage = new PlantStage();
        tempStage.stageType = currentStageData.stageType;
        tempStage.plantID = currentStageData.plantID;
        tempStage.plantName = currentStageData.plantName;
        tempStage.growthRate = currentStageData.growthRate;
        tempStage.witherRate = currentStageData.witherRate; // 复制枯萎速度
        tempStage.prerequisitePlantIDs = currentStageData.prerequisitePlantIDs != null 
            ? new List<int>(currentStageData.prerequisitePlantIDs) 
            : new List<int>();
        tempStage.prerequisiteWeights = currentStageData.prerequisiteWeights != null 
            ? new List<float>(currentStageData.prerequisiteWeights) 
            : new List<float>();
        tempStage.updatePlantIDs = currentStageData.updatePlantIDs != null 
            ? new List<int>(currentStageData.updatePlantIDs) 
            : new List<int>();
        tempStage.updateWeights = currentStageData.updateWeights != null 
            ? new List<float>(currentStageData.updateWeights) 
            : new List<float>();
        tempStage.prefabPath = currentStageData.prefabPath;
        
        // 从当前活跃的光源获取最新的LightingData
        tempStage.associatedLights = GetLightingDataFromLightSources();
        
        return tempStage;
    }

    #region 动画方法
    // 光源大小渐变的协程
    public IEnumerator AnimateLightSizeChange(float targetSize, float duration, System.Action onComplete, float updateInterval = 0.05f)
    {
        // 获取当前所有非障碍物光源
        List<Lighting> nonObstacleLights = lightSources.Where(l => !l.isObstacle).ToList();
        
        if (nonObstacleLights.Count == 0)
        {
            // 如果没有非障碍物光源，直接完成
            onComplete?.Invoke();  
            yield break;
        }
        
        // 记录每个光源的初始大小
        Dictionary<Lighting, float> initialSizes = new Dictionary<Lighting, float>();
        foreach (var light in nonObstacleLights)
        {
            initialSizes[light] = light.size;
        }
        
        float startTime = Time.time;
        float elapsedTime = 0f;
        float lastUpdateTime = 0f;
        
        // 在指定时间内逐渐改变光源大小
        while (elapsedTime < duration)
        {
            elapsedTime = Time.time - startTime;
            
            // 检查是否需要更新（根据指定的更新间隔）
            if (elapsedTime - lastUpdateTime >= updateInterval)
            {
                lastUpdateTime = elapsedTime;
                
                float t = Mathf.Clamp01(elapsedTime / duration); // 归一化时间
                float easedT = EaseInOutCubic(t); // 应用缓动效果
                
                // 更新每个光源的大小
                foreach (var light in nonObstacleLights)
                {
                    if (light != null)
                    {
                        // 计算当前大小
                        light.size = Mathf.Lerp(initialSizes[light], targetSize, easedT);
                        
                        // 标记光源为脏
                        light.MarkDirty();
                    }
                }
                
                // 更新所有脏标记的光源
                LightingManager.UpdateDirtyLights();
            }
            
            yield return null;
        }
        
        // 确保最终大小精确匹配目标大小
        foreach (var light in nonObstacleLights)
        {
            if (light != null)
            {
                light.size = targetSize;
                light.MarkDirty();
            }
        }
        
        // 最终更新所有脏标记的光源
        LightingManager.UpdateDirtyLights();
        
        // 动画完成后执行回调
        onComplete?.Invoke();
    }

    // 缓动函数 - 三次方缓入缓出
    private float EaseInOutCubic(float t)
    {
        return t < 0.5 ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
    }
    #endregion
} 
