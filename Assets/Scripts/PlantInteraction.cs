using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PlantInteraction : MonoBehaviour
{
    // 添加单例模式
    public static PlantInteraction Instance { get; private set; }
    
    // 是否允许交互
    private bool interactionEnabled = true;
    
    // 定义检测模式枚举
    public enum DetectionModeType
    {
        Dig,      // 铲土模式
        Water,    // 浇水模式
        Seed,     // 种子模式
        Fire,     // 火模式
        Info      // 信息查看模式
    }
    
    // 添加检测范围设置
    [Header("检测范围设置")]
    public float digDetectionRange = 3f;    // 铲除模式检测范围
    public float waterDetectionRange = 2f;  // 浇水模式检测范围
    public float seedDetectionRange = 4f;   // 种子模式检测范围
    public float fireDetectionRange = 3f;   // 火模式检测范围
    public float infoDetectionRange = 5f;   // 信息查看模式检测范围
    
    [Header("测试设置")]
    public List<GameObject> prefabToSpawn; // 改为预制体列表
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float darkCooldown = 0.5f; // 新增障碍物冷却时间
    
    [Header("交互设置")]
    public int interactiveLayer = 6; // 交互对象层级，默认为6

    public static QuadTree quadTree;
    public Camera mainCamera;
    
    private List<GameObject> objects = new List<GameObject>();
    private float darkCooldownTimer; // 障碍物冷却计时器

    private string seedName = "SmallSlow"; // 默认种子名称
    private bool isAnimationPlaying = false; // 添加标志位，用于判断是否正在播放动画
    
    // 添加检测状态变量
    private bool isInDetectionMode = false;
    private DetectionModeType currentDetectionMode = DetectionModeType.Dig; // 默认铲土模式
    private GameObject detectedObject = null;
    private RaycastHit currentHit;
    private bool hasValidDetection = false;

    // 在类的顶部添加事件定义
    public static event System.Action<Plant> OnPlantRemoved;
    
    // 添加检测模式状态改变事件 - 增加模式类型作为参数
    public static event System.Action<bool, DetectionModeType> OnDetectionModeChanged;
    
    // 定义事件（与PlayerPathfinding保持一致）
    public static event Action OnInteractiveObjectClicked;
    public static event Action<Plant> OnPlantClicked;

    [Header("模式按钮")]
    public Button digButton;    // 铲土模式按钮
    public Button waterButton;  // 浇水模式按钮  
    public Button seedButton;   // 种子模式按钮
    public Button fireButton;   // 火模式按钮
    public Button infoButton;   // 信息查看模式按钮

    [Header("实时范围显示")]
    public bool useRangeIndicator = true;         // 是否使用范围指示器
    private GameObject rangeIndicator;            // 范围指示器游戏对象

    [Header("范围可视化设置")]      // 是否显示检测范围
    public Color digRangeColor = new Color(1, 0, 0, 0.2f);       // 铲土模式范围颜色
    public Color waterRangeColor = new Color(0, 0, 1, 0.2f);     // 浇水模式范围颜色
    public Color seedRangeColor = new Color(0, 1, 0, 0.2f);      // 种子模式范围颜色
    public Color fireRangeColor = new Color(1, 0.5f, 0, 0.2f);   // 火模式范围颜色
    public Color infoRangeColor = new Color(1, 1, 0, 0.2f);      // 信息查看模式范围颜色

    [Header("圆环描边设置")]
    public Color outlineColor = new Color(0, 0, 0, 0.7f);        // 描边颜色
    public float mainLineWidth = 0.1f;                           // 主圆环线宽
    public float outlineWidth = 0.14f;                           // 描边线宽
    public int circleSegments = 50;                              // 圆环分段数

    [Header("动画控制")]
    public List<Animator> animatorList = new List<Animator>(); // 添加Animator列表

    private void Awake()
    {
        // 设置单例
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        mainCamera = Camera.main;
        
        // 添加订阅StartUI事件
        StartUI.OnStartUIVisibilityChanged += HandleStartUIVisibilityChange;
        
        // 创建范围指示器
        if (useRangeIndicator)
        {
            CreateRangeIndicator();
        }
        
        // 为模式按钮添加监听
        if (digButton != null)
            digButton.onClick.AddListener(() => ToggleDetectionMode(DetectionModeType.Dig));
        
        if (waterButton != null)
            waterButton.onClick.AddListener(() => ToggleDetectionMode(DetectionModeType.Water));
        
        if (seedButton != null)
            seedButton.onClick.AddListener(() => ToggleDetectionMode(DetectionModeType.Seed));
        
        if (fireButton != null)
            fireButton.onClick.AddListener(() => ToggleDetectionMode(DetectionModeType.Fire));
            
        if (infoButton != null)
            infoButton.onClick.AddListener(() => ToggleDetectionMode(DetectionModeType.Info));
    }

    void Update()
    {
        // 如果交互被禁用，直接返回
        if (!interactionEnabled)
            return;
            
        // 更新冷却计时器
        darkCooldownTimer -= Time.deltaTime;

        // 在检测状态下
        if (isInDetectionMode)
        {
            // 执行射线检测
            PerformDetection();
            
            // 如果按下左键且有有效检测
            if (Input.GetMouseButtonDown(0) && hasValidDetection)
            {
                // 检查是否有UI元素遮挡点击
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    Debug.Log("UI元素遮挡了操作");
                    return;
                }
                
                ExecuteActionBasedOnMode();
                // 延迟0.1秒退出检测状态
                StartCoroutine(DelayedExitDetectionMode(0.1f));
            }

        }
        // 使用if-else if结构确保每帧只响应一个按键
            
        // 按Q键切换信息查看模式
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ToggleDetectionMode(DetectionModeType.Info);
        }
        // 按C键切换检测状态 - 铲土模式
        else if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleDetectionMode(DetectionModeType.Dig);
        }
        // 按Z键枯萎所有植物
        else if (Input.GetKeyDown(KeyCode.Z))
        {
            WitherAllPlants();
        }
        // 按空格键更新光照
        else if (Input.GetKeyDown(KeyCode.Space))
        {
            LightingManager.UpdateLighting();
        }
        // 按键1插入Seed - 修改为GetKeyDown
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ToggleDetectionMode(DetectionModeType.Seed);
        }
        // 按键2插入障碍物
        else if (Input.GetKeyDown(KeyCode.Alpha3) && darkCooldownTimer <= 0)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                TestDark(hit.point);
                darkCooldownTimer = darkCooldown; // 重置冷却时间
                LightingManager.UpdateDirtyLights();
            }
        }
        // 按键4插入火 - 修改为GetKeyDown
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("火");
            ToggleDetectionMode(DetectionModeType.Fire);
        }
        // 处理V键功能 - 浇水
        else if (Input.GetKeyDown(KeyCode.V))
        {
            ToggleDetectionMode(DetectionModeType.Water);
        }

        // 更新范围指示器
        if (useRangeIndicator && rangeIndicator != null)
        {
            UpdateRangeIndicator();
        }
    }
    
    // 进入检测模式 - 增加模式类型参数
    private void EnterDetectionMode(DetectionModeType mode)
    {
        isInDetectionMode = true;
        currentDetectionMode = mode;
        
        // 根据不同模式显示不同提示
        switch (mode)
        {
            case DetectionModeType.Dig:
                Debug.Log("进入铲土模式，点击左键铲除植物");
                MessageManager.instance.SendMessage("进入铲土模式，点击左键铲除植物", PlayerPathfinding.Instance.transform, MessageType.Auto, 3f);
                break;
            case DetectionModeType.Water:
                Debug.Log("进入浇水模式，点击左键浇水");
                MessageManager.instance.SendMessage("进入浇水模式，点击左键浇水", PlayerPathfinding.Instance.transform, MessageType.Auto, 3f);
                break;
            case DetectionModeType.Seed:
                Debug.Log("进入种子模式，点击左键种植");
                MessageManager.instance.SendMessage("进入种子模式，点击左键种植", PlayerPathfinding.Instance.transform, MessageType.Auto, 3f);
                break;
            case DetectionModeType.Fire:
                Debug.Log("进入火模式，点击左键点火");
                MessageManager.instance.SendMessage("进入火模式，点击左键点火", PlayerPathfinding.Instance.transform, MessageType.Auto, 3f);
                break;
            case DetectionModeType.Info:
                Debug.Log("进入信息查看模式，点击左键查看信息");
                MessageManager.instance.SendMessage("进入信息查看模式，点击左键查看信息", PlayerPathfinding.Instance.transform, MessageType.Auto, 3f);
                break;
        }
        
        // 触发事件通知其他组件，同时传递模式类型
        OnDetectionModeChanged?.Invoke(true, mode);

        // 显示范围指示器
        if (useRangeIndicator && rangeIndicator != null)
        {
            rangeIndicator.SetActive(true);
            UpdateRangeIndicator();
        }
    }

    // 执行射线检测并高亮显示可选择的对象
    private void PerformDetection()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        hasValidDetection = false;

        // 根据不同的检测模式执行不同的检测逻辑
        switch (currentDetectionMode)
        {
            case DetectionModeType.Dig:
                PerformDigDetection(ray);
                break;
                
            case DetectionModeType.Water:
                PerformWaterDetection(ray);
                break;
                
            case DetectionModeType.Seed:
                PerformSeedDetection(ray);
                break;
                
            case DetectionModeType.Fire:
                PerformFireDetection(ray);
                break;
                
            case DetectionModeType.Info:
                PerformInfoDetection(ray);
                break;
        }
        
        if (!hasValidDetection && detectedObject != null)
        {
            // 清除之前的高亮效果
            detectedObject = null;
        }
    }

    // 执行铲土检测
    private void PerformDigDetection(Ray ray)
    {
        if (!CheckInRange(digDetectionRange)) return;
        
        // 检测种子和植物
        if (Physics.Raycast(ray, out RaycastHit digHit, 100f, 1 << 6))
        {
            // 检测到种子
            currentHit = digHit;
            detectedObject = digHit.transform.gameObject;
            hasValidDetection = true;
            Debug.Log("检测到种子，点击左键铲除");
        }
        else if (!isAnimationPlaying && Physics.Raycast(ray, out digHit, 100f, 1 << 13))
        {
            // 检测到植物
            GameObject hitObject = digHit.transform.gameObject;
            Plant[] plants = hitObject.GetComponentsInParent<Plant>(true);
            
            if (plants != null && plants.Length > 0)
            {
                foreach (Plant plant in plants)
                {
                    if (plant.currentStage == 1)
                    {
                        currentHit = digHit;
                        detectedObject = hitObject;
                        hasValidDetection = true;
                        Debug.Log("检测到植物，点击左键铲除");
                        break;
                    }
                }
            }
        }
    }

    // 执行浇水检测
    private void PerformWaterDetection(Ray ray)
    {
        if (!CheckInRange(waterDetectionRange)) return;
        
        // 检测植物以浇水
        if (Physics.Raycast(ray, out RaycastHit waterHit, 100f, 1 << 13))
        {
            GameObject hitObject = waterHit.transform.gameObject;
            Plant[] plants = hitObject.GetComponentsInParent<Plant>(true);
            
            if (plants != null && plants.Length > 0)
            {
                foreach (Plant plant in plants)
                {
                    if (plant.currentStage == 1)
                    {
                        currentHit = waterHit;
                        detectedObject = hitObject;
                        hasValidDetection = true;
                        Debug.Log("检测到植物，点击左键浇水");
                        break;
                    }
                }
            }
        }
    }

    // 执行种植检测
    private void PerformSeedDetection(Ray ray)
    {
        if (!CheckInRange(seedDetectionRange)) return;
        
        // 检测地面以种植
        if (Physics.Raycast(ray, out RaycastHit seedHit, 100f, groundLayer))
        {
            currentHit = seedHit;
            detectedObject = null; // 不需要特定对象，只需要位置
            hasValidDetection = true;
            Debug.Log("检测到地面，点击左键种植");
        }
    }

    // 执行火焰检测
    private void PerformFireDetection(Ray ray)
    {
        if (!CheckInRange(fireDetectionRange)) return;
        
        // 检测地面以放置火
        if (Physics.Raycast(ray, out RaycastHit fireHit, 100f, groundLayer))
        {
            currentHit = fireHit;
            detectedObject = null; // 不需要特定对象，只需要位置
            hasValidDetection = true;
            Debug.Log("检测到地面，点击左键放置火");
        }
    }

    // 执行信息查看检测
    private void PerformInfoDetection(Ray ray)
    {
        if (!CheckInRange(infoDetectionRange)) return;
        
        // 使用RaycastAll检测所有碰撞体
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        
        // 检查是否有交互层的对象被指向
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject.layer == interactiveLayer)
            {
                currentHit = hit;
                detectedObject = hit.transform.gameObject;
                hasValidDetection = true;
                Debug.Log("检测到交互对象，点击左键查看信息");
                return;
            }
        }
        
        // 检查是否指向植物
        foreach (RaycastHit hit in hits)
        {
            // 尝试获取植物组件（包括父对象）
            Plant plant = hit.collider.GetComponent<Plant>();
            
            // 如果直接组件没有找到，尝试在所有父对象中查找
            if (plant == null)
            {
                plant = hit.collider.GetComponentInParent<Plant>();
            }
            
            if (plant != null)
            {
                currentHit = hit;
                detectedObject = hit.transform.gameObject;
                hasValidDetection = true;
                Debug.Log("检测到植物，点击左键查看信息");
                return;
            }
        }
        
        // 检查是否有地面被指向
        if (Physics.Raycast(ray, out RaycastHit groundHit, 100f, groundLayer))
        {
            currentHit = groundHit;
            detectedObject = null; // 地面不需要特定对象
            hasValidDetection = true;
            Debug.Log("检测到地面，点击左键查看位置信息");
        }
    }

    // 退出检测模式
    private void ExitDetectionMode()
    {
        // 如果当前是种子模式或火模式，触发所有Animator的SeedHide动画
        if (currentDetectionMode == DetectionModeType.Seed || currentDetectionMode == DetectionModeType.Fire)
        {
            foreach (Animator animator in animatorList)
            {
                if (animator != null && HasParameter(animator, "SeedHide"))
                {
                    animator.SetTrigger("SeedHide");
                }
            }
        }
        
        isInDetectionMode = false;
        hasValidDetection = false;
        detectedObject = null;
        Debug.Log($"退出{GetModeName(currentDetectionMode)}模式");
        // 触发事件通知其他组件
        OnDetectionModeChanged?.Invoke(false, currentDetectionMode);

        // 隐藏范围指示器
        if (useRangeIndicator && rangeIndicator != null)
        {
            rangeIndicator.SetActive(false);
        }
    }
    
    // 获取模式名称
    private string GetModeName(DetectionModeType mode)
    {
        switch (mode)
        {
            case DetectionModeType.Dig: return "铲土";
            case DetectionModeType.Water: return "浇水";
            case DetectionModeType.Seed: return "种子";
            case DetectionModeType.Fire: return "火";
            case DetectionModeType.Info: return "信息查看";
            default: return "未知";
        }
    }

    // 基于当前模式执行对应操作
    private void ExecuteActionBasedOnMode()
    {
        switch (currentDetectionMode)
        {
            case DetectionModeType.Dig:
                ExecuteDigAction();
                break;
                
            case DetectionModeType.Water:
                ExecuteWaterAction();
                break;
                
            case DetectionModeType.Seed:
                ExecuteSeedAction();
                break;
                
            case DetectionModeType.Fire:
                ExecuteFireAction();
                break;
                
            case DetectionModeType.Info:
                ExecuteInfoAction();
                break;
        }
    }

    // 执行铲土操作
    private void ExecuteDigAction()
    {
        if (detectedObject == null) return;
        
        // 检查是否为layer=6（种子层）
        if (detectedObject.layer == 6)
        {
            FindAndRemoveSeed(detectedObject);
        }
        else
        {
            // 假设为植物对象（layer=13）
            GameObject hitObject = detectedObject;
            Plant[] plants = hitObject.GetComponentsInParent<Plant>(true);
            
            if (plants != null && plants.Length > 0)
            {
                foreach (Plant plant in plants)
                {
                    if (plant.currentStage == 1)
                    {
                        RemovePlant(plant);
                        break;
                    }
                }
            }
        }
    }

    // 执行浇水操作
    private void ExecuteWaterAction()
    {
        if (detectedObject == null) return;
        
        // 浇水逻辑 - 修改植物生长速度
        GameObject waterHitObject = detectedObject;
        Plant[] waterPlants = waterHitObject.GetComponentsInParent<Plant>(true);
        
        if (waterPlants != null && waterPlants.Length > 0)
        {
            foreach (Plant plant in waterPlants)
            {
                if (plant.currentStage == 1)
                {
                    string plantName = plant.plantName;
                    string newName = plantName;
                    SizeLevel size = SizeLevel.Small; // 默认值
                    GrowthRateLevel newGrowthRate = GrowthRateLevel.Slow; // 默认值
                    bool parseSuccess = false;
                    
                    // 尝试从植物名称解析SizeLevel和GrowthRateLevel
                    foreach (string sizeStr in Enum.GetNames(typeof(SizeLevel)))
                    {
                        if (plantName.StartsWith(sizeStr, StringComparison.OrdinalIgnoreCase))
                        {
                            // 获取除尺寸外的部分，作为生长速度字符串
                            string growthRatePart = plantName.Substring(sizeStr.Length);
                            
                            // 尝试解析生长速度枚举和尺寸枚举
                            if (Enum.TryParse(growthRatePart, true, out GrowthRateLevel parsedGrowthRate) &&
                                Enum.TryParse(sizeStr, true, out SizeLevel parsedSize))
                            {
                                // 解析成功
                                parseSuccess = true;
                                size = parsedSize;
                                
                                // 在当前GrowthRateLevel基础上+1，如果超过范围则重置为0
                                int growthRateIndex = (int)parsedGrowthRate;
                                growthRateIndex = (growthRateIndex + 1) % Enum.GetValues(typeof(GrowthRateLevel)).Length;
                                newGrowthRate = (GrowthRateLevel)growthRateIndex;
                                
                                // 创建新的名称
                                newName = size.ToString() + newGrowthRate.ToString();
                                break;
                            }
                        }
                    }
                    
                    // 如果解析失败就使用默认的SmallSlow
                    if (!parseSuccess)
                    {
                        newName = "SmallSlow";
                    }
                    
                    // 对所有光源调用 RemoveLighting()
                    foreach (var light in plant.lightSources.ToList())
                    {
                        light.RemoveLighting();
                        plant.lightSources.Remove(light);
                        Destroy(light);
                    }

                    // 清空现有的生长阶段并添加新种子阶段
                    plant.growthStages.Clear();
                    
                    // 获取种子阶段
                    Plant.PlantStage seedStage = PlantManager.Instance.GetSeedPlantStageFromName(newName);
                    if (seedStage != null)
                    {
                        // 添加种子阶段
                        plant.growthStages.Add(seedStage);
                        plant.maxStages = plant.growthStages.Count;
                        
                        // 应用变化
                        plant.currentStage = 0;
                        plant.Grow();
                        Debug.Log($"通过浇水，植物生长速度被更新为：{newName}");
                    }
                    else
                    {
                        Debug.LogWarning($"无法获取种子阶段: {newName}");
                    }
                    break;
                }
            }
        }
    }

    // 执行种植操作
    private void ExecuteSeedAction()
    {
        if (!hasValidDetection) return;
        
        // 种植逻辑 - 在点击位置种植新的种子
        TestSeed(currentHit.point);
        LightingManager.UpdateDirtyLights();
        Debug.Log($"在 {currentHit.point} 位置种植了新的种子");
    }

    // 执行火焰操作
    private void ExecuteFireAction()
    {
        if (!hasValidDetection) return;
        
        // 火模式逻辑 - 在点击位置放置火
        TestFire(currentHit.point);
        LightingManager.UpdateDirtyLights();
        Debug.Log($"在 {currentHit.point} 位置放置了火");
    }
    
    // 执行信息查看操作
    private void ExecuteInfoAction()
    {
        // 使用RaycastAll检测所有碰撞体
        RaycastHit[] hits = Physics.RaycastAll(mainCamera.ScreenPointToRay(Input.mousePosition), 100f);
        
        // 检查是否有交互层的对象被点击
        bool interactiveHit = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject.layer == interactiveLayer)
            {
                interactiveHit = true;
                // 触发事件
                OnInteractiveObjectClicked?.Invoke();
                Debug.Log("触发交互对象点击事件");
                break;
            }
        }
        
        // 先检查是否点击到了植物
        Plant clickedPlant = null;
        foreach (RaycastHit hit in hits)
        {
            // 尝试获取植物组件（包括父对象）
            Plant plant = hit.collider.GetComponent<Plant>();
            
            // 如果直接组件没有找到，尝试在所有父对象中查找
            if (plant == null)
            {
                plant = hit.collider.GetComponentInParent<Plant>();
            }
            
            if (plant != null)
            {
                clickedPlant = plant;
                break;
            }
        }
        
        // 如果点击到了植物，触发植物点击事件
        if (clickedPlant != null)
        {
            OnPlantClicked?.Invoke(clickedPlant);
            Debug.Log($"触发植物点击事件: {clickedPlant.name}");
            return; // 点击到植物后不再处理其他点击逻辑
        }
        
        if (interactiveHit)
        {
            Debug.Log("检测到交互对象点击");
            return;
        }
        
        // 检查地面点击，显示位置信息
        if (Physics.Raycast(mainCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit groundHit, 100f, groundLayer))
        {
            Debug.Log($"位置: {groundHit.point}");
        }
    }
    
    // 更改植物大小的逻辑
    private void ChangePlantSize(Plant plant)
    {
        if (plant == null || isAnimationPlaying) return;
        
        string plantName = plant.plantName;
        string newName = plantName;
        SizeLevel newSize = SizeLevel.Small; // 默认值
        GrowthRateLevel growthRate = GrowthRateLevel.Slow; // 默认值
        bool parseSuccess = false;
        
        // 尝试从植物名称解析StageType和SizeLevel
        foreach (string sizeStr in Enum.GetNames(typeof(SizeLevel)))
        {
            if (plantName.StartsWith(sizeStr, StringComparison.OrdinalIgnoreCase))
            {
                // 获取除尺寸外的部分，作为生长速度字符串
                string growthRatePart = plantName.Substring(sizeStr.Length);
                
                // 尝试解析生长速度枚举和尺寸枚举
                if (Enum.TryParse(growthRatePart, true, out GrowthRateLevel parsedGrowthRate) &&
                    Enum.TryParse(sizeStr, true, out SizeLevel parsedSize))
                {
                    // 解析成功
                    parseSuccess = true;
                    growthRate = parsedGrowthRate;
                    
                    // 在当前SizeLevel基础上+1，如果超过范围则重置为0
                    int sizeIndex = (int)parsedSize;
                    sizeIndex = (sizeIndex + 1) % Enum.GetValues(typeof(SizeLevel)).Length;
                    newSize = (SizeLevel)sizeIndex;
                    
                    // 创建新的名称
                    newName = newSize.ToString() + growthRate.ToString();
                    break;
                }
            }
        }
        
        // 如果解析失败就使用默认的SmallSlow
        if (!parseSuccess)
        {
            newName = "SmallSlow";
        }
        
        // 获取种子阶段
        Plant.PlantStage seedStage = PlantManager.Instance.GetSeedPlantStageFromName(newName);
        if (seedStage != null)
        {
            // 查找新阶段中非障碍物光源的最大size值
            float maxNewSize = 0f;
            foreach (var lightData in seedStage.associatedLights)
            {
                if (!lightData.isObstacle && lightData.size > maxNewSize)
                {
                    maxNewSize = lightData.size;
                }
            }
            
            // 设置动画标志位为true，表示开始播放动画
            isAnimationPlaying = true;
            
            // 启动大小变化动画，指定更新间隔为0.1秒
            StartCoroutine(plant.AnimateLightSizeChange(maxNewSize, 1.0f, () => {
                // 动画完成后，移除旧光源
                foreach (var light in plant.lightSources.ToList())
                {
                    light.RemoveLighting();
                    plant.lightSources.Remove(light);
                    Destroy(light);
                }

                // 清空现有的生长阶段并添加新种子阶段
                plant.growthStages.Clear();
                plant.growthStages.Add(seedStage);
                plant.maxStages = plant.growthStages.Count;
                
                // 应用变化
                plant.currentStage = 0;
                plant.Grow(false);
                Debug.Log($"植物被更新为：{newName}");
                
                // 动画结束后，将标志位设置回false
                isAnimationPlaying = false;
            }, 0.05f));
        }
        else
        {
            Debug.LogWarning($"无法获取种子阶段: {newName}");
        }
    }
    
    // 处理V键的生长速度变化功能
    private void HandleGrowthRateChange()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        // 检测layer=13
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, 1 << 13))
        {
            // 找出射线碰撞点的GameObject，并获取其所有Plant组件
            GameObject hitObject = hit.transform.gameObject;
            Plant[] plants = hitObject.GetComponentsInParent<Plant>(true);
            
            if (plants != null && plants.Length > 0)
            {
                foreach (Plant plant in plants)
                {
                    // 检查是否为种子阶段(currentStage=1)
                    if (plant.currentStage == 1)
                    {
                        ChangePlantGrowthRate(plant);
                    }
                }
            }
            else
            {
                Debug.Log("检测到layer=13碰撞，但未找到Plant组件");
            }
        }
    }
    
    // 修改植物的生长速度
    private void ChangePlantGrowthRate(Plant plant)
    {
        if (plant == null) return;
        
        string plantName = plant.plantName;
        string newName = plantName;
        SizeLevel size = SizeLevel.Small; // 默认值
        GrowthRateLevel newGrowthRate = GrowthRateLevel.Slow; // 默认值
        bool parseSuccess = false;
        
        // 尝试从植物名称解析StageType和SizeLevel
        foreach (string sizeStr in Enum.GetNames(typeof(SizeLevel)))
        {
            if (plantName.StartsWith(sizeStr, StringComparison.OrdinalIgnoreCase))
            {
                // 获取除尺寸外的部分，作为生长速度字符串
                string growthRatePart = plantName.Substring(sizeStr.Length);
                
                // 尝试解析生长速度枚举和尺寸枚举
                if (Enum.TryParse(growthRatePart, true, out GrowthRateLevel parsedGrowthRate) &&
                    Enum.TryParse(sizeStr, true, out SizeLevel parsedSize))
                {
                    // 解析成功
                    parseSuccess = true;
                    size = parsedSize;
                    
                    // 在当前GrowthRateLevel基础上+1，如果超过范围则重置为0
                    int growthRateIndex = (int)parsedGrowthRate;
                    growthRateIndex = (growthRateIndex + 1) % Enum.GetValues(typeof(GrowthRateLevel)).Length;
                    newGrowthRate = (GrowthRateLevel)growthRateIndex;
                    
                    // 创建新的名称
                    newName = size.ToString() + newGrowthRate.ToString();
                    break;
                }
            }
        }
        
        // 如果解析失败就使用默认的SmallSlow
        if (!parseSuccess)
        {
            newName = "SmallSlow";
        }
        
        // 对所有光源调用 RemoveLighting()
        foreach (var light in plant.lightSources.ToList())
        {
            light.RemoveLighting();
            plant.lightSources.Remove(light);
            Destroy(light);
        }

        // 参考TestSeed方法，清空现有的生长阶段并添加新种子阶段
        plant.growthStages.Clear();
        
        // 获取种子阶段
        Plant.PlantStage seedStage = PlantManager.Instance.GetSeedPlantStageFromName(newName);
        if (seedStage != null)
        {
            // 添加种子阶段
            plant.growthStages.Add(seedStage);
            plant.maxStages = plant.growthStages.Count;
            
            // 应用变化
            plant.currentStage = 0;
            plant.Grow();
            Debug.Log($"植物生长速度被更新为：{newName}");
        }
        else
        {
            Debug.LogWarning($"无法获取种子阶段: {newName}");
        }
    }
    
    // 使所有植物枯萎
    private void WitherAllPlants()
    {
        if (objects.Count >= 1)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                // 获取所有 Plant 组件
                Plant[] plants = objects[i].GetComponents<Plant>();
                foreach(Plant plant in plants){
                    plant.Wither(); // 对每个Plant组件调用 Wither 方法
                }
            }
        }
    }

    public void TestSeed(Vector3 position){
        
        // 创建一个空物体
        GameObject newObj = new GameObject("Seed");
        newObj.transform.position = position;
        objects.Add(newObj);
        
        // 添加Plant组件
        Plant plant = newObj.AddComponent<Plant>();
        if (plant != null)
        {
            // 清空现有的生长阶段
            plant.growthStages.Clear();
            
            // 获取种子阶段
            Plant.PlantStage seedStage = PlantManager.Instance.GetSeedPlantStageFromName(seedName);
            if (seedStage != null)
            {
                // 添加种子阶段
                plant.growthStages.Add(seedStage);
                plant.maxStages = plant.growthStages.Count;
            }
            else
            {
                Debug.LogWarning($"无法获取种子阶段: {seedName}");
            }
        }
        
        // 插入四叉树
        bool success = quadTree.Insert(newObj);
    } 

     public void TestDark(Vector3 position){
        GameObject prefab = prefabToSpawn[1];
        GameObject newObj = Instantiate(
            prefab,
            position,
            Quaternion.identity
        );
        objects.Add(newObj);
        // 插入四叉树
        bool success = quadTree.Insert(newObj);
    } 

    public void TestFire(Vector3 position){
        GameObject prefab = prefabToSpawn[2];
        GameObject newObj = Instantiate(
            prefab,
            position,
            Quaternion.identity
        );
        objects.Add(newObj);
        // 插入四叉树
        bool success = quadTree.Insert(newObj);
    }

    // 添加新方法用于寻找 Plant 脚本并铲除种子
    private void FindAndRemoveSeed(GameObject obj)
    {
        Transform current = obj.transform;
        
        // 从当前物体开始，逐级向上查找父物体
        while (current != null)
        {
            Plant plant = current.GetComponent<Plant>();
            if (plant != null && plant.currentStage == 1)
            {
                // 找到满足条件的 Plant 组件，铲除种子
                RemovePlant(plant);
                break;
            }
            
            // 继续向上查找父物体
            current = current.parent;
        }
    }

    // 铲除种子的方法
    private void RemovePlant(Plant plant)
    {
        if (plant == null) return;
        
        // 触发植物被铲除事件
        OnPlantRemoved?.Invoke(plant);
        
        // 对所有光源调用 RemoveLighting()
        foreach (var light in plant.lightSources.ToList())
        {
            light.RemoveLighting();
            plant.lightSources.Remove(light);
            Destroy(light);
        }
        
        // 从四叉树中移除
        LightingManager.tree.Remove(plant.gameObject);
        
        // 禁用 Plant 组件
        plant.enabled = false;
        
        // 从 objects 列表中移除
        if (objects.Contains(plant.gameObject))
        {
            objects.Remove(plant.gameObject);
        }
        
        // 销毁游戏对象
        Destroy(plant.gameObject);
    }

    // 延迟退出检测模式的协程
    private IEnumerator DelayedExitDetectionMode(float delay)
    {
        yield return new WaitForSeconds(delay);
        ExitDetectionMode();
    }


    // 检测模式开关切换方法 - 可以从外部调用
    public bool ToggleDetectionMode(DetectionModeType mode)
    {
        // 如果当前不在检测模式或者模式不同，则进入新的模式
        if (!isInDetectionMode || currentDetectionMode != mode)
        {
            // 特殊情况处理：如果是从火模式到种子模式或从种子模式到火模式的直接切换
            bool isSpecialTransition = isInDetectionMode && 
                ((currentDetectionMode == DetectionModeType.Fire && mode == DetectionModeType.Seed) || 
                 (currentDetectionMode == DetectionModeType.Seed && mode == DetectionModeType.Fire));
            
            // 普通情况：如果当前在检测模式但模式不同，先退出当前模式
            if (isInDetectionMode && currentDetectionMode != mode && !isSpecialTransition)
            {
                ExitDetectionMode();
            }
            else if (isSpecialTransition)
            {
                // 特殊切换不需要隐藏种子动画，只需要更新内部状态
                isInDetectionMode = false;
                hasValidDetection = false;
                detectedObject = null;
                
                // 触发事件通知其他组件
                OnDetectionModeChanged?.Invoke(false, currentDetectionMode);
            }
            
            // 如果进入种子模式或火模式，触发所有Animator的Seed动画
            if (mode == DetectionModeType.Seed || mode == DetectionModeType.Fire)
            {
                foreach (Animator animator in animatorList)
                {
                    if (animator != null && HasParameter(animator, "Seed"))
                    {
                        animator.SetTrigger("Seed");
                    }
                }
            }
            
            EnterDetectionMode(mode);
            return true;
        }
        // 如果当前已在指定模式，则退出
        else
        {
            StartCoroutine(DelayedExitDetectionMode(0.1f));
            return false;
        }
    }

    // 检测并种植种子
    private void DetectAndPlaceSeed()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            TestSeed(hit.point);
            LightingManager.UpdateDirtyLights();
        }
    }

    // 检测并浇水
    private void DetectAndWaterPlant()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        // 检测layer=13
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, 1 << 13))
        {
            // 找出射线碰撞点的GameObject，并获取其所有Plant组件
            GameObject hitObject = hit.transform.gameObject;
            Plant[] plants = hitObject.GetComponentsInParent<Plant>(true);
            
            if (plants != null && plants.Length > 0)
            {
                foreach (Plant plant in plants)
                {
                    // 浇水促进生长
                    Debug.Log($"给植物浇水: {plant.name}");
                    plant.Grow();
                    break;
                }
            }
            else
            {
                Debug.Log("检测到layer=13碰撞，但未找到Plant组件");
            }
        }
    }

    // 检测并放置火
    private void DetectAndPlaceFire()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
        {
            TestFire(hit.point);
            LightingManager.UpdateDirtyLights();
        }
    }

    // 检查目标是否在指定范围内的辅助方法
    private bool CheckInRange(float range)
    {
        if (PlayerPathfinding.Instance == null) return true; // 如果没有找到玩家引用，允许任何操作
        
        // 获取玩家位置
        Vector3 playerPosition = PlayerPathfinding.Instance.transform.position;
        
        // 检查点击位置（世界坐标）
        if (Physics.Raycast(mainCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit))
        {
            // 计算XZ平面上的距离
            float distance = Vector2.Distance(
                new Vector2(playerPosition.x, playerPosition.z),
                new Vector2(hit.point.x, hit.point.z)
            );
            
            // 如果距离超过范围，返回false
            if (distance > range)
            {
                Debug.Log($"超出{GetModeName(currentDetectionMode)}模式的操作范围");
                return false;
            }
            
            return true;
        }
        
        return false; // 如果射线没有命中，则不在范围内
    }

    // 创建范围指示器
    private void CreateRangeIndicator()
    {
        // 创建一个空游戏对象作为圆环的容器
        rangeIndicator = new GameObject("DetectionRangeIndicator");
        
        // 设置父对象为当前对象
        rangeIndicator.transform.SetParent(transform);
        
        // 初始时隐藏
        rangeIndicator.SetActive(false);
        
        // 添加主圆环的线渲染器组件
        LineRenderer mainLineRenderer = rangeIndicator.AddComponent<LineRenderer>();
        
        // 设置线渲染器属性
        mainLineRenderer.positionCount = circleSegments + 1;
        mainLineRenderer.useWorldSpace = false;
        mainLineRenderer.startWidth = mainLineWidth;
        mainLineRenderer.endWidth = mainLineWidth;
        
        // 创建半透明材质
        Material mainMaterial = new Material(Shader.Find("Sprites/Default"));
        mainMaterial.color = new Color(1, 1, 1, 0.5f);
        mainLineRenderer.material = mainMaterial;
        mainLineRenderer.name = "MainCircle";
        
        // 添加第二个线渲染器作为描边
        GameObject outlineObj = new GameObject("OutlineCircle");
        outlineObj.transform.SetParent(rangeIndicator.transform);
        outlineObj.transform.localPosition = Vector3.zero;
        
        LineRenderer outlineRenderer = outlineObj.AddComponent<LineRenderer>();
        outlineRenderer.positionCount = circleSegments + 1;
        outlineRenderer.useWorldSpace = false;
        outlineRenderer.startWidth = outlineWidth;
        outlineRenderer.endWidth = outlineWidth;
        
        // 创建描边材质
        Material outlineMaterial = new Material(Shader.Find("Sprites/Default"));
        outlineMaterial.color = outlineColor;
        outlineRenderer.material = outlineMaterial;
        
        // 创建圆环形状 - 两个渲染器使用相同的点
        for (int i = 0; i <= circleSegments; i++)
        {
            float angle = i * (2 * Mathf.PI / circleSegments);
            float x = Mathf.Sin(angle);
            float z = Mathf.Cos(angle);
            Vector3 pos = new Vector3(x, 0, z);
            
            // 设置两个渲染器的位置
            mainLineRenderer.SetPosition(i, pos);
            outlineRenderer.SetPosition(i, pos);
        }
    }

    // 更新范围指示器
    private void UpdateRangeIndicator()
    {
        if (PlayerPathfinding.Instance == null) return;
        
        // 获取玩家位置
        Vector3 playerPosition = PlayerPathfinding.Instance.transform.position;
        
        // 根据是否在检测模式来显示或隐藏
        rangeIndicator.SetActive(isInDetectionMode);
        
        if (isInDetectionMode)
        {
            // 设置位置
            rangeIndicator.transform.position = new Vector3(
                playerPosition.x,
                0.05f, // 稍微抬高避免Z-fighting
                playerPosition.z
            );
            
            // 获取当前模式的范围
            float range = GetCurrentModeRange();
            
            // 设置圆环大小
            rangeIndicator.transform.localScale = new Vector3(range, 1, range);
            
            // 设置颜色
            Color color = GetCurrentModeColor();
            
            // 更新主圆环颜色
            LineRenderer mainLineRenderer = rangeIndicator.GetComponent<LineRenderer>();
            if (mainLineRenderer != null)
            {
                Material material = mainLineRenderer.material;
                material.color = color;
            }
            
            // 保持描边颜色为设置的颜色
            Transform outlineObj = rangeIndicator.transform.Find("OutlineCircle");
            if (outlineObj != null)
            {
                LineRenderer outlineRenderer = outlineObj.GetComponent<LineRenderer>();
                if (outlineRenderer != null)
                {
                    outlineRenderer.material.color = outlineColor;
                }
            }
        }
    }

    // 获取当前模式的范围
    private float GetCurrentModeRange()
    {
        switch (currentDetectionMode)
        {
            case DetectionModeType.Dig:
                return digDetectionRange;
            case DetectionModeType.Water:
                return waterDetectionRange;
            case DetectionModeType.Seed:
                return seedDetectionRange;
            case DetectionModeType.Fire:
                return fireDetectionRange;
            case DetectionModeType.Info:
                return infoDetectionRange;
            default:
                return 1f;
        }
    }

    // 获取当前模式的颜色
    private Color GetCurrentModeColor()
    {
        switch (currentDetectionMode)
        {
            case DetectionModeType.Dig:
                return digRangeColor;
            case DetectionModeType.Water:
                return waterRangeColor;
            case DetectionModeType.Seed:
                return seedRangeColor;
            case DetectionModeType.Fire:
                return fireRangeColor;
            case DetectionModeType.Info:
                return infoRangeColor;
            default:
                return Color.white;
        }
    }

    // 新增方法：启用交互
    public void EnableInteraction()
    {
        interactionEnabled = true;
        Debug.Log("植物交互功能已启用");
    }
    
    // 新增方法：禁用交互
    public void DisableInteraction()
    {
        // 如果正在检测模式中，先退出
        if (isInDetectionMode)
        {
            ExitDetectionMode();
        }
        
        interactionEnabled = false;
        Debug.Log("植物交互功能已禁用");
    }

    // 处理UI可见性变化的方法
    private void HandleStartUIVisibilityChange(bool isVisible)
    {
        if (isVisible)
        {
            DisableInteraction();
        }
        else
        {
            EnableInteraction();
        }
    }

    // 在OnDestroy中取消订阅
    void OnDestroy()
    {
        // 取消订阅StartUI事件
        StartUI.OnStartUIVisibilityChanged -= HandleStartUIVisibilityChange;
    }

    private static bool HasParameter(Animator animator, string paramName)
    {
        if (animator == null) return false;
        
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }
} 