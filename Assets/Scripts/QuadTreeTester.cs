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
    private bool showSeedUI = false;
    private Rect seedUIRect = new Rect(10, 10, 200, 150);

    void Start()
    {
        mainCamera = Camera.main;
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

        // 按Tab键显示/隐藏种子选择UI
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            showSeedUI = !showSeedUI;
        }
    }

    void OnGUI()
    {
        if (showSeedUI)
        {
            seedUIRect = GUI.Window(0, seedUIRect, DrawSeedSelectionWindow, "种子选择");
        }
    }

    void DrawSeedSelectionWindow(int windowID)
    {
        GUILayout.BeginVertical(GUI.skin.box);
        
        GUILayout.Label("选择种子大小:");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(selectedSize == SizeLevel.Small, "小", GUI.skin.button))
            selectedSize = SizeLevel.Small;
        if (GUILayout.Toggle(selectedSize == SizeLevel.Medium, "中", GUI.skin.button))
            selectedSize = SizeLevel.Medium;
        if (GUILayout.Toggle(selectedSize == SizeLevel.Large, "大", GUI.skin.button))
            selectedSize = SizeLevel.Large;
        GUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        GUILayout.Label("选择生长速度:");
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(selectedGrowthRate == GrowthRateLevel.Slow, "慢", GUI.skin.button))
            selectedGrowthRate = GrowthRateLevel.Slow;
        if (GUILayout.Toggle(selectedGrowthRate == GrowthRateLevel.Medium, "中", GUI.skin.button))
            selectedGrowthRate = GrowthRateLevel.Medium;
        if (GUILayout.Toggle(selectedGrowthRate == GrowthRateLevel.Fast, "快", GUI.skin.button))
            selectedGrowthRate = GrowthRateLevel.Fast;
        GUILayout.EndHorizontal();
        
        GUILayout.Space(10);
        
        // 更新种子名称
        seedName = selectedSize.ToString() + selectedGrowthRate.ToString();
        GUILayout.Label($"当前选择: {seedName}");
        
        GUILayout.EndVertical();
        
        // 允许窗口拖动
        GUI.DragWindow();
    }

    public void TestSeed(Vector3 position){
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