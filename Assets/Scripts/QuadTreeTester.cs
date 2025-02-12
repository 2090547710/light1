using System.Collections.Generic;
using UnityEngine;

public class QuadTreeTester : MonoBehaviour
{
    [Header("测试设置")]
    public GameObject prefabToSpawn; // 需要插入的预制体
    public LayerMask groundLayer;    // 地面层

    public static QuadTree quadTree;
    private Camera mainCamera;

    private List<GameObject> objects = new List<GameObject>();

    void Start()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        // 鼠标中键插入
        if (Input.GetMouseButtonDown(2))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                TestSeed(hit.point);
            }
        }
    }

    public void TestSeed(Vector3 position){
        // 实例化预制体
                GameObject newObj = Instantiate(
                    prefabToSpawn,
                    position,
                    Quaternion.identity
                );
                objects.Add(newObj);
                // 插入四叉树
                CustomCollider collider = newObj.GetComponent<CustomCollider>();
                bool success = quadTree.Insert(newObj);
                Debug.Log($"插入{(success ? "成功" : "失败")} | " +
                         $"位置：{position} | " +
                         $"尺寸：{collider.Bounds.size}");
    } 


   
} 