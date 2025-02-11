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
        objects = new GameObject[10];
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
        if(Input.GetKeyDown(KeyCode.Space)){
            TestNeighborDetection();
        }
    }

    private void TestNeighborDetection()
    {
        // 获取玩家对象（需要场景中有Player标签的对象）
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if(player == null)
        {
            Debug.LogWarning("未找到玩家对象，请创建带有Player标签的对象");
            return;
        }

        // 获取邻近节点
        List<QuadTree.QuadTreeNode> neighbors = tree.GetNeighborLeafNodes(
            player.transform.position, 
            5f // 检测半径
        );

        Debug.Log($"找到 {neighbors.Count} 个邻近节点");
        
        // 在场景视图中可视化
        foreach(var node in neighbors)
        {
            Vector3 center = new Vector3(node.Center.x, 0, node.Center.y);
            Vector3 size = new Vector3(node.Size.x, 0.1f, node.Size.y);
            Debug.DrawLine(player.transform.position, center, Color.green, 2f);
        }
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


    }



    void OnDrawGizmos()
    {        
        // 绘制当前发光节点的邻近节点
        // if (tree != null && tree.GetIlluminatedLeafNodes() != null)
        // {
        //     foreach (var node in tree.GetIlluminatedLeafNodes())
        //     {
        //         // 绘制当前节点
        //         Gizmos.color = Color.yellow;
        //         Vector3 center = new Vector3(node.Center.x, 0, node.Center.y);
        //         Gizmos.DrawWireCube(center, new Vector3(node.Size.x, 0, node.Size.y));
                
        //         // 绘制邻近节点
        //         Gizmos.color = Color.blue;
        //         foreach (var neighbor in tree.GetDirectNeighbors(node))
        //         {
        //             Vector3 neighborCenter = new Vector3(neighbor.Center.x, 0, neighbor.Center.y);
        //             Gizmos.DrawWireCube(neighborCenter, new Vector3(neighbor.Size.x, 0, neighbor.Size.y));
        //         }
        //     }
        // }
    }
}
