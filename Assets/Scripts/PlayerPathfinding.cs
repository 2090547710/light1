using UnityEngine;
using System.Collections;
using System.Linq; // 添加LINQ命名空间

public class PlayerPathfinding : MonoBehaviour
{
    public static QuadTree quadTree; 
    public float moveSpeed = 5f;
    public float stoppingDistance = 0.2f;
    
    private Vector3[] currentPath;
    private int currentPathIndex;
    private Coroutine moveCoroutine;
    
    [Header("Marker Settings")]
    public GameObject markerPrefab;  // 拖入预制体
    public float markerScale = 0.2f; // 标记缩放比例
    public float markerDuration = 1.0f; // 标记存在时间
    
    // 新增玩家对象引用
    private GameObject playerObject;
    
    [Header("旋转设置")]
    public float rotationSpeed = 10f; // 旋转速度
    public bool smoothRotation = true; // 是否使用平滑旋转
    
    void Start()
    {
        playerObject = this.gameObject;
        InsertToQuadTree(); // 初始插入
        stoppingDistance=quadTree.MinNodeSize.x/2-0.05f;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // 左键点击
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                // 保持玩家当前高度
                Vector3 targetPos = hit.point;
                targetPos.y = transform.position.y;
                
                // 生成并配置标记
                if(markerPrefab)
                {
                    GameObject marker = Instantiate(markerPrefab, targetPos+new Vector3(0,0.5f,0), Quaternion.identity);
                    marker.transform.localScale = Vector3.one * markerScale;
                    Destroy(marker, markerDuration);
                }
                
                // 请求路径
                var path = quadTree.FindPath(transform.position, targetPos);
                if (path != null && path.Count > 0)
                {
                    // 转换路径点为世界坐标（保持高度）
                    currentPath = path.Select(p => new Vector3(p.x, transform.position.y, p.z)).ToArray();
                    currentPathIndex = 0;
                    
                    // 停止之前的移动协程
                    if (moveCoroutine != null)
                    {
                        StopCoroutine(moveCoroutine);
                    }
                    
                    // 更新玩家在四叉树中的位置
                    quadTree.Remove(playerObject);
                    InsertToQuadTree();
                    
                    moveCoroutine = StartCoroutine(FollowPath());
                }
                else
                {
                    // Debug.LogWarning("无法找到到目标点的可行路径！");
                }
            }
        }
    }

    IEnumerator FollowPath()
    {
        while (currentPathIndex < currentPath.Length)
        {
            Vector3 targetPos = currentPath[currentPathIndex];
            // 添加中断检查点
            
            // 移除了距离检查循环，改为每帧移动一次
            float step = moveSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(
                transform.position, 
                targetPos, 
                step);
            
            // 添加转向逻辑
            Vector3 direction = targetPos - transform.position;
            if (direction != Vector3.zero)
            {
                // 计算目标旋转
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                
                // 根据设置决定是否使用平滑旋转
                if (smoothRotation)
                {
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, 
                        targetRotation, 
                        rotationSpeed * Time.deltaTime);
                }
                else
                {
                    transform.rotation = targetRotation;
                }
            }

            quadTree.Remove(playerObject);
            InsertToQuadTree();

            
            // 检查是否已足够接近目标点
            if (Vector3.Distance(transform.position, targetPos) <= stoppingDistance)
            {
                currentPathIndex++;
            }
            
            yield return null;
        }
    }

    // 将InsertToQuadTree方法改为公共方法，以便其他类可以调用
    public void InsertToQuadTree()
    {
        if (quadTree != null)
        {
            // 使用新的插入方法（不调整位置）
            quadTree.Insert(playerObject, adjustPosition: false);
        }
    }

    // 调试绘制路径
    void OnDrawGizmos()
    {
        if (currentPath != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < currentPath.Length; i++)
            {
                Gizmos.DrawSphere(currentPath[i], 0.2f);
                if (i > 0)
                    Gizmos.DrawLine(currentPath[i-1], currentPath[i]);
            }
        }

        // 新增玩家所在节点绘制
        if (quadTree != null && playerObject != null)
        {
            var playerNode = quadTree.FindLeafNode(transform.position);
            if (playerNode != null)
            {
                Vector3 center = new Vector3(
                    playerNode.Center.x, 
                    transform.position.y,  // 保持与玩家相同高度
                    playerNode.Center.y);
                
                Vector3 size = new Vector3(
                    playerNode.Size.x, 
                    1f,  // 保持薄片高度
                    playerNode.Size.y);
                
                Gizmos.color = new Color(1, 0, 0, 1f); // 半透明红色
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
} 