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

    void Start()
    {
        mainCamera = Camera.main;
        LightingManager.UpdateLighting();
    }

    void Update()
    {
        // 更新冷却计时器
        seedCooldownTimer -= Time.deltaTime;
        darkCooldownTimer -= Time.deltaTime;

        if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) 
        && Input.GetKeyDown(KeyCode.Z))
        {
            if(objects.Count > 0){
                int i = objects.Count - 1;
                Lighting lighting = objects[i].GetComponent<Lighting>();
                if(lighting != null){
                    lighting.RemoveLighting();
                }
                quadTree.Remove(objects[i]);
                Destroy(objects[i]);
                objects.RemoveAt(i);
                LightingManager.UpdateLighting();
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
            }
        }

        // 按C键清除所有对象
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (objects.Count >= 1)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    Lighting lighting = objects[i].GetComponent<Lighting>();
                    if(lighting != null){
                        lighting.RemoveLighting();
                    }
                    quadTree.Remove(objects[i]);
                    Destroy(objects[i]);
                }
                objects.Clear();
                LightingManager.UpdateLighting();
            }
        }
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
        // 插入四叉树
        bool success = quadTree.Insert(newObj);
        // Debug.Log($"插入{(success ? "成功" : "失败")} | " +
        //          $"位置：{position} | " );

        Lighting lighting = newObj.GetComponent<Lighting>();
        if(lighting != null){
            LightingManager.UpdateLighting();
        }
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

        Lighting lighting = newObj.GetComponent<Lighting>();
        if(lighting != null){
            LightingManager.UpdateLighting();
        }
        // 如果障碍物，则立马移除光照
        if(lighting.areaType == AreaType.Obstacle){
            lighting.RemoveLighting();
            quadTree.Remove(newObj);
            Destroy(newObj);
            LightingManager.UpdateLighting();
        }
    } 
  
} 