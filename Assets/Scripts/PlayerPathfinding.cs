using UnityEngine;
using System.Collections;
using System.Linq; // 添加LINQ命名空间

public class PlayerPathfinding : MonoBehaviour
{
    public static QuadTree quadTree; // 在Inspector中分配
    public float moveSpeed = 5f;
    public float stoppingDistance = 0.5f;
    
    private Vector3[] currentPath;
    private int currentPathIndex;
    private Coroutine moveCoroutine;

    [Header("Marker Settings")]
    public GameObject markerPrefab;  // 拖入预制体
    public float markerScale = 0.2f; // 标记缩放比例
    public float markerDuration = 1.0f; // 标记存在时间

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
                var path = quadTree.FindPath(transform.position, targetPos, 0.1f);
                // Debug.Log(path.Count);
                if (path != null && path.Count > 0)
                {
                    // 转换路径点为世界坐标（保持高度）
                    currentPath = path.Select(p => new Vector3(p.x, transform.position.y, p.z)).ToArray();
                    currentPathIndex = 0;
                    
                    // 停止之前的移动协程
                    if (moveCoroutine != null) 
                        StopCoroutine(moveCoroutine);
                    
                    moveCoroutine = StartCoroutine(FollowPath());
                }
            }
        }
    }

    IEnumerator FollowPath()
    {
        while (currentPathIndex < currentPath.Length)
        {
            Vector3 targetPos = currentPath[currentPathIndex];
            
            // 保持当前高度移动
            Vector3 moveDirection = new Vector3(targetPos.x - transform.position.x, 0, targetPos.z - transform.position.z);
            transform.Translate(moveDirection.normalized * moveSpeed * Time.deltaTime, Space.World);
            
            // 到达路径点时转向下一个点
            if (Vector3.Distance(transform.position, targetPos) < stoppingDistance)
            {
                currentPathIndex++;
            }
            
            yield return null;
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
    }
} 