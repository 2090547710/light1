using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Collections;

public class QuadTreeTester : MonoBehaviour
{
    [Header("测试设置")]
    public List<GameObject> prefabToSpawn; // 改为预制体列表
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float seedCooldown = 0.5f; // 新增种子冷却时间
    [SerializeField] private float darkCooldown = 0.5f; // 新增障碍物冷却时间
    [SerializeField] private float fireCooldown = 0.5f; // 新增火冷却时间

    public static QuadTree quadTree;
    public Camera mainCamera;

    private List<GameObject> objects = new List<GameObject>();
    private float seedCooldownTimer; // 种子冷却计时器
    private float darkCooldownTimer; // 障碍物冷却计时器
    private float fireCooldownTimer; // 火冷却计时器

    private string seedName = "SmallSlow"; // 默认种子名称
    private bool isAnimationPlaying = false; // 添加标志位，用于判断是否正在播放动画

    // 在类的顶部添加事件定义
    public static event System.Action<Plant> OnPlantRemoved;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 更新冷却计时器
        seedCooldownTimer -= Time.deltaTime;
        darkCooldownTimer -= Time.deltaTime;
        fireCooldownTimer -= Time.deltaTime;

        // 按Z键枯萎所有植物
        if (Input.GetKeyDown(KeyCode.Z))
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

        if (Input.GetKeyDown(KeyCode.Space))
        {
            LightingManager.UpdateLighting();
        }
        
        // 按键1插入Seed（添加冷却时间判断）
        if (Input.GetKey(KeyCode.Alpha1) && seedCooldownTimer <= 0)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                TestSeed(hit.point);
                seedCooldownTimer = seedCooldown; // 重置冷却时间
                LightingManager.UpdateDirtyLights();
            }
        }

        // 按键2插入障碍物（添加冷却时间判断）
        if (Input.GetKey(KeyCode.Alpha2) && darkCooldownTimer <= 0)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                TestDark(hit.point);
                darkCooldownTimer = darkCooldown; // 重置冷却时间
                LightingManager.UpdateDirtyLights();
            }
        }

        // 按键4插入火（添加冷却时间判断）
        if (Input.GetKey(KeyCode.Alpha4) && fireCooldownTimer <= 0)
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                TestFire(hit.point);
                fireCooldownTimer = fireCooldown; // 重置冷却时间
                LightingManager.UpdateDirtyLights();
            }
        }

        // 在 Update 方法内部添加按 C 键的逻辑
        if (Input.GetKeyDown(KeyCode.C))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            // 使用 layer=6 进行射线检测
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, 1 << 6))
            {
                FindAndRemoveSeed(hit.transform.gameObject);
            }
            // 如果没有检测到layer=7，则尝试检测layer=13，并且确保当前没有动画在播放
            else if (!isAnimationPlaying && Physics.Raycast(ray, out hit, 100f, 1 << 13))
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
                    }
                }
                else
                {
                    Debug.Log("检测到layer=13碰撞，但未找到Plant组件");
                }
            }
        }

        // 在 Update 方法内部添加按 V 键的逻辑
        if (Input.GetKeyDown(KeyCode.V))
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
                    }
                }
                else
                {
                    Debug.Log("检测到layer=13碰撞，但未找到Plant组件");
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
        // Debug.Log($"插入{(success ? "成功" : "失败")} | " +
        //          $"位置：{position} | " );
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
} 