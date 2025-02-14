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
                LightingManager.UpdateLighting();
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            LightingManager.UpdateLighting();
        }
        
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