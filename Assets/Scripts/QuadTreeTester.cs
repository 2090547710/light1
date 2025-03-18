using System.Collections.Generic;
using UnityEngine;

public class QuadTreeTester : MonoBehaviour
{
    [Header("测试设置")]
    public List<GameObject> prefabToSpawn; // 改为预制体列表
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float seedCooldown = 0.5f; // 新增种子冷却时间
    [SerializeField] private float darkCooldown = 0.5f; // 新增障碍物冷却时间

    public static QuadTree quadTree;
    private Camera mainCamera;

    private List<GameObject> objects = new List<GameObject>();
    private float seedCooldownTimer; // 种子冷却计时器
    private float darkCooldownTimer; // 障碍物冷却计时器

    // 添加种子选择UI相关变量
    [Header("种子选择")]
    [SerializeField] private SizeLevel selectedSize = SizeLevel.Medium;
    [SerializeField] private GrowthRateLevel selectedGrowthRate = GrowthRateLevel.Medium;
    private string seedName = "MediumMedium"; // 默认种子名称

    void Start()
    {
        mainCamera = Camera.main;
        // 初始化种子名称
        UpdateSeedName();
    }

    void Update()
    {
        // 更新冷却计时器
        seedCooldownTimer -= Time.deltaTime;
        darkCooldownTimer -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Z))
        {
            if(objects.Count > 0){
                int i = objects.Count - 1;
                // 获取所有 Plant 组件
                Plant[] plants = objects[i].GetComponents<Plant>();
                foreach(Plant plant in plants){
                    plant.Wither(); // 对每个Plant组件调用 Wither 方法
                }
                Destroy(objects[i]);
                objects.RemoveAt(i);
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

        // 按C键清除所有对象
        if (Input.GetKeyDown(KeyCode.C))
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
                    Destroy(objects[i]);
                }
                objects.Clear();
            }
        }
    }

    // 添加新方法来更新种子名称
    private void UpdateSeedName()
    {
        seedName = selectedSize.ToString() + selectedGrowthRate.ToString();
    }

    public void TestSeed(Vector3 position){
        // 在放置种子前更新种子名称，确保使用最新的Inspector设置
        UpdateSeedName();
        
        // 随机选择一个预制体
        GameObject prefab = prefabToSpawn[0];
        GameObject newObj = Instantiate(
            prefab,
            position,
            Quaternion.identity
        );
        objects.Add(newObj);
        
        // 获取Plant组件
        Plant plant = newObj.GetComponent<Plant>();
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
} 