using System.Collections.Generic;
using UnityEngine;

public class QuadTreeTester : MonoBehaviour
{
    [Header("测试设置")]
    public List<GameObject> prefabToSpawn; // 改为预制体列表
    [SerializeField] private LayerMask groundLayer;

    public static QuadTree quadTree;
    private Camera mainCamera;

    private List<GameObject> objects = new List<GameObject>();

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 鼠标右键插入障碍物
        if (Input.GetMouseButtonDown(1)) // 1 表示鼠标右键
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                TestSeed(hit.point);
            }
        }

        if (Input.GetMouseButtonDown(2)) // 1 表示鼠标右键
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                TestDark(hit.point);
            }
        }

        // 按C键清除
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (objects.Count > 1)
            {
                // 从后往前删除避免索引问题
                for (int i = objects.Count - 1; i >= 1; i--)
                {
                    quadTree.Remove(objects[i]);
                    Destroy(objects[i]);
                }
                // 保留第一个元素
                var first = objects[0];
                objects.Clear();
                objects.Add(first);
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
        Debug.Log($"插入{(success ? "成功" : "失败")} | " +
                 $"位置：{position} | " );
    } 

     public void TestDark(Vector3 position){
        // 随机选择一个预制体
        GameObject prefab = prefabToSpawn[1];
        GameObject newObj = Instantiate(
            prefab,
            position,
            Quaternion.identity
        );
        objects.Add(newObj);
        // 插入四叉树
        bool success = quadTree.Insert(newObj);
        Debug.Log($"插入{(success ? "成功" : "失败")} | " +
                 $"位置：{position} | " );
    } 


   
} 