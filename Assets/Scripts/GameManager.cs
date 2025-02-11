using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    #region
    public static GameManager Instance;
    public QuadTree tree;  // 提前声明
    public int maxDepth=5;
    public int capacity=1;
    public bool preSplit=true;
    public Vector2 size=new Vector2(100,100);  
    public Vector2 center=Vector2.zero;
    public GameObject prefab; 
    
    void Awake()
    {
        Instance = this;
        // 将四叉树初始化移到Awake
        tree = new QuadTree(
            center: center,
            size: size,
            capacity: capacity,
            maxDepth: maxDepth,
            preSplit: preSplit  // 启用预分裂
        );

        // 将四叉树实例赋给光照系统
        LightingManager.tree = tree;
    }
    #endregion
    public GameObject[] objects;
    public Bounds queryBounds;  


    // Start is called before the first frame update
    void Start()
    {
        // 生成100个实例
        objects = new GameObject[100];
        for (int i = 0; i < objects.Length; i++)
        {
            Vector3 position = new Vector3(
                Random.Range(-50f, 50f),
                1f,
                Random.Range(-50f, 50f)
            );
            objects[i] = Instantiate(prefab, position, Quaternion.identity, transform);
        }

        // 重置光照状态
        tree.ResetIllumination();

        // 插入对象
        foreach (var obj in objects)
        {
            tree.Insert(obj);
        }

        // 初始化查询区域（注意使用XZ平面坐标）
        Vector3 center = new Vector3(0, 0, 0); // y坐标可设为任意值（不影响查询）
        Vector3 size = new Vector3(10, 0, 10); // 在XZ平面创建10x10的方形区域
        queryBounds = new Bounds(center, size);
    }

    // Update is called once per frame
    void Update()
    {
        // if(Input.GetKeyDown(KeyCode.Space)){
        //     check();
        // }
    }


    // 查询区域（示例区域）
   
    public void check(){

        // 执行查询
        List<GameObject> foundObjects = tree.QueryArea(queryBounds);

        if(foundObjects.Count == 0){
            Debug.Log("没有找到对象");
        }


        // 处理结果（示例：输出对象信息）
        foreach (var obj in foundObjects)
        {
            Vector3 pos = obj.transform.position;
            Debug.Log($"找到对象：{obj.name} 位置：X={pos.x}, Z={pos.z}");
        }

        //删除对象
        // foreach (var obj in foundObjects)
        // {
        //     tree.Remove(obj);
        //     //隐藏对象
        //     obj.SetActive(false);
        //     //删除信息
        //     Vector3 pos = obj.transform.position;
        //     Debug.Log($"删除对象：{obj.name} 位置：X={pos.x}, Z={pos.z}");

        // }

    }



    void OnDrawGizmos()
    {        
        
        // if (queryBounds != null)
        // {
        //     Gizmos.color = Color.magenta;
        //     Gizmos.DrawWireCube(queryBounds.center, queryBounds.size);
        // }
  
    }
}
