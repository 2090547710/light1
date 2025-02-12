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
        PlayerPathfinding.quadTree = tree;
        QuadTreeTester.quadTree = tree;
    }
    #endregion
    public GameObject[] objects;
    public Bounds queryBounds;  


    // Start is called before the first frame update
    void Start()
    {
        // 生成N个实例
        objects = new GameObject[50];
        for (int i = 0; i < objects.Length; i++)
        {
            Vector3 position = new Vector3(
                Random.Range(-50f, 50f),
                1f,
                Random.Range(-50f, 50f)
            );
            objects[i] = Instantiate(prefab, position, Quaternion.identity, transform);
            objects[i].GetComponent<Lighting>().Radius=Random.Range(0,5);
        }

        // 重置光照状态
        tree.ResetIllumination();

        // 插入对象
        foreach (var obj in objects)
        {
            tree.Insert(obj);
        }

    }

    // Update is called once per frame
    void Update()
    {
       
    }



    void OnDrawGizmos()
    {        
      
    }
}
