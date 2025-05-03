using UnityEngine;
using System.Collections;
using System.Linq; // 添加LINQ命名空间
using System;
using System.Diagnostics; // 添加用于计时的命名空间

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
    
    [Header("地图设置")]
    public LayerMask mapLayer; // MAP层属性
    
    [Header("光照设置")]
    public float playerLightRange = 5.0f; // 玩家光照范围
    public float playerLightIntensity = 1.0f; // 玩家光照强度
    
    [Header("交互设置")]
    public int interactiveLayer = 6; // 交互对象层级，默认为6
    
    [Header("调试设置")]
    public bool showPathfindingDebug = true; // 是否显示寻路调试信息
    private string pathfindingDebugInfo = ""; // 存储寻路调试信息

    // 添加检测模式标志
    private bool isDetectionModeActive = false;
    
    [Header("高度设置")]
    public float baseHeight = 1.5f; // 基础高度

    // 新增键盘移动设置
    [Header("键盘移动设置")]
    public float keyboardMoveSpeed = 3f; // 键盘移动速度
    public float maxHeightDifference = 0.01f; // 最大可行走高度差

    // 当前玩家所在节点
    private QuadTree.QuadTreeNode currentPlayerNode;
    
    public static PlayerPathfinding Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        playerObject = this.gameObject;
        InsertToQuadTree(); // 初始插入

        stoppingDistance=quadTree.MinNodeSize.x/2-0.05f;
        
        // 初始化着色器参数
        UpdateShaderParameters();
        
        // 订阅检测模式状态改变事件
        PlantInteraction.OnDetectionModeChanged += HandleDetectionModeChanged;
    }
    
    // 在脚本销毁时取消订阅事件
    void OnDestroy()
    {
        PlantInteraction.OnDetectionModeChanged -= HandleDetectionModeChanged;
    }
    
    // 处理检测模式状态改变
    private void HandleDetectionModeChanged(bool isActive, PlantInteraction.DetectionModeType modeType)
    {
        isDetectionModeActive = isActive;
        // 可以根据需要处理modeType参数
    }

    void Start()
    {
        MessageManager.instance.SendMessage("Hello, World!", transform, MessageType.Info, 3f);
    }

    void Update()
    {
        // 更新着色器中的玩家位置
        UpdateShaderParameters();
        
        // 更新当前玩家节点引用
        currentPlayerNode = quadTree.FindLeafNode(transform.position);
        
        // 只有在非检测模式下才处理左键点击
        if (!isDetectionModeActive && Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            HandleLeftClick();
        }
        
        // 如果没有正在执行的寻路协程，才处理WASD输入
        if (moveCoroutine == null)
        {
            // 处理WASD键盘输入
            HandleKeyboardInput();
        }
    }
    
    // 处理键盘WASD输入
    private void HandleKeyboardInput()
    {
        // 获取水平和垂直输入
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // 如果有输入
        if (horizontal != 0 || vertical != 0)
        {
            // 创建基于摄像机方向的移动向量
            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 cameraRight = Camera.main.transform.right;
            
            // 将摄像机方向投影到XZ平面
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            // 计算移动方向
            Vector3 moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;
            
            // 计算目标位置
            Vector3 targetPosition = transform.position + moveDirection * keyboardMoveSpeed * Time.deltaTime;
            
            // 检查目标位置是否可行走
            if (CanMoveToPosition(targetPosition))
            {
                // 如果可移动，则更新位置
                transform.position = targetPosition;
                
                // 更新玩家在四叉树中的位置
                quadTree.Remove(playerObject);
                InsertToQuadTree();
                
                // 更新玩家高度
                UpdatePlayerHeight();
                
                // 如果移动方向不为零，设置旋转
                if (moveDirection != Vector3.zero)
                {
                    // 计算目标旋转
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    
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
            }
        }
    }
    
    // 检查目标位置是否可行走
    private bool CanMoveToPosition(Vector3 targetPosition)
    {
        // 查找目标位置的四叉树节点
        QuadTree.QuadTreeNode targetNode = quadTree.FindLeafNode(targetPosition);
        
        // 如果节点不存在，则不可行走
        if (targetNode == null)
            return false;
        
        // 检查节点是否可行走
        if (!targetNode.IsWalkable)
            return false;
        
        // 检查高度差
        float heightDifference = Mathf.Abs(targetNode.Height - (currentPlayerNode != null ? currentPlayerNode.Height : 0));
        if (heightDifference > maxHeightDifference)
            return false;
        
        return true;
    }
    
    // 处理左键点击
    private void HandleLeftClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        
        // 使用RaycastAll检测所有碰撞体
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
 
        // 检查是否有MAP层的物体被击中
        bool validHit = false;
        RaycastHit mapHit = new RaycastHit();
        
        foreach (RaycastHit hit in hits)
        {
            if (((1 << hit.collider.gameObject.layer) & mapLayer) != 0)
            {
                mapHit = hit;
                validHit = true;
                break;
            }
        }
        if (validHit)
        {
            // 保持玩家当前高度
            Vector3 targetPos = mapHit.point;
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
            
            // 获取四叉树寻路调试信息
            if (showPathfindingDebug)
            {
                pathfindingDebugInfo = quadTree.GetPathfindingDebugInfo();
                UnityEngine.Debug.Log($"寻路信息: {pathfindingDebugInfo}");
            }
            
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
                // 路径为空，记录错误
                if (showPathfindingDebug)
                {
                    UnityEngine.Debug.LogWarning($"寻路失败: 无法找到从 {transform.position} 到 {targetPos} 的路径");
                }
            }
        }
    }
    
    // 更新着色器参数
    void UpdateShaderParameters()
    {
        // 设置全局变量，将玩家世界位置传入着色器
        Shader.SetGlobalVector("_PlayerWorldPos", transform.position);
        
        // 更新光照参数
        Shader.SetGlobalFloat("_PlayerLightRange", playerLightRange);
        Shader.SetGlobalFloat("_PlayerLightIntensity", playerLightIntensity);
    }

    IEnumerator FollowPath()
    {
        while (currentPathIndex < currentPath.Length)
        {
            // 在移动前更新玩家高度
            UpdatePlayerHeight();
            
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
            if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(targetPos.x, targetPos.z)) <= stoppingDistance)
            {
                currentPathIndex++;
            }
            
            yield return null;
        }
        
        // 路径结束后重置协程引用
        moveCoroutine = null;
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
            
            // 在场景视图中显示寻路调试信息
            if (showPathfindingDebug && !string.IsNullOrEmpty(pathfindingDebugInfo))
            {
                UnityEditor.Handles.BeginGUI();
                GUIStyle style = new GUIStyle();
                style.normal.textColor = Color.yellow;
                style.fontSize = 14;
                style.fontStyle = FontStyle.Bold;
                
                // 在玩家上方显示寻路信息
                Vector3 screenPos = Camera.current.WorldToScreenPoint(transform.position + Vector3.up * 2);
                screenPos.y = Camera.current.pixelHeight - screenPos.y;
                
                UnityEditor.Handles.Label(new Vector2(screenPos.x, screenPos.y), pathfindingDebugInfo, style);
                UnityEditor.Handles.EndGUI();
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
        
        // 绘制玩家光源范围
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawSphere(transform.position, playerLightRange);
    }
    
    // 添加一个新的方法用于在游戏视图中显示调试信息
    void OnGUI()
    {
        if (showPathfindingDebug && !string.IsNullOrEmpty(pathfindingDebugInfo))
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.yellow;
            style.fontSize = 16;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.UpperLeft;
            
            GUI.Label(new Rect(10, 10, 300, 200), pathfindingDebugInfo, style);
        }
        
        // 显示玩家当前节点高度信息
        if (currentPlayerNode != null)
        {
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.green;
            style.fontSize = 14;
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.UpperRight;
            
            string nodeInfo = $"节点高度: {currentPlayerNode.Height:F2}\n可行走: {currentPlayerNode.IsWalkable}";
            GUI.Label(new Rect(Screen.width - 200, 10, 190, 100), nodeInfo, style);
        }
    }

    // 更新玩家高度的新方法
    private void UpdatePlayerHeight()
    {
        // 从玩家位置向下发射射线
        Ray ray = new Ray(transform.position + Vector3.up * 10, Vector3.down);
        RaycastHit hit;
        
        // 首先使用layerMask为7进行射线检测
        int terrainLayer = 7;
        int terrainLayerMask = 1 << terrainLayer;
        
        // 尝试与地形层碰撞
        if (Physics.Raycast(ray, out hit, 20f, terrainLayerMask))
        {
            // 将玩家高度设置为碰撞点高度加上基础高度
            Vector3 newPosition = transform.position;
            newPosition.y = hit.point.y + baseHeight;
            transform.position = newPosition;
            
            // 更新四叉树中的位置
            quadTree.Remove(playerObject);
            InsertToQuadTree();
        }
        else
        {
            // 如果没有检测到地形层碰撞，则尝试与mapLayer进行射线检测
            if (Physics.Raycast(ray, out hit, 20f, mapLayer))
            {
                // 将玩家高度设置为碰撞点高度加上基础高度
                Vector3 newPosition = transform.position;
                newPosition.y = hit.point.y + baseHeight;
                transform.position = newPosition;
                
                // 更新四叉树中的位置
                quadTree.Remove(playerObject);
                InsertToQuadTree();
            }
        }
    }
} 