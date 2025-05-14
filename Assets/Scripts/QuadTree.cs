using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using System.Diagnostics;

public class QuadTree
{
    #region 四叉树节点类
    public class QuadTreeNode
    {
        // 节点边界（中心点 + 尺寸）
        public Vector2 Center { get; private set; }
        public Vector2 Size { get; private set; }
        
        // 子节点（四个象限）
        public QuadTreeNode[] Children { get; private set; }
        
        // 存储对象的最大容量
        public int Capacity { get; private set; }
        
        // 当前存储的对象列表
        public List<GameObject> Objects = new List<GameObject>();

        // 简化为光照可见状态
        public bool IsIlluminated;

        // 添加父节点引用
        [System.NonSerialized]
        public QuadTreeNode Parent;

        // 新增路径规划属性
        public float GCost = Mathf.Infinity;
        public float HCost;
        public float FCost => GCost + HCost;
        public QuadTreeNode ParentNode;
        public bool IsWalkable => CheckWalkableLayer();

        // 新增高度属性
        public float Height { get; private set; }

        // 新增亮度属性
        public float Brightness;
        public float BrightnessThreshold;

        public QuadTreeNode(Vector2 center, Vector2 size, int capacity)
        {
            Center = center;
            Size = size;
            Capacity = capacity;
            Height = 0;
            IsIlluminated = false;
            Brightness = 0;
            BrightnessThreshold = 0.7f;
            ;
        }

        // 分裂节点为四个子节点
        public void Split()
        {
            Children = new QuadTreeNode[4];
            Vector2 quarterSize = Size * 0.5f;
            Vector2 halfSize = Size * 0.25f;

            // 计算四个子节点的中心点
            Children[0] = new QuadTreeNode(
                new Vector2(Center.x + halfSize.x, Center.y + halfSize.y),
                quarterSize, Capacity);

            Children[1] = new QuadTreeNode(
                new Vector2(Center.x - halfSize.x, Center.y + halfSize.y),
                quarterSize, Capacity);

            Children[2] = new QuadTreeNode(
                new Vector2(Center.x - halfSize.x, Center.y - halfSize.y),
                quarterSize, Capacity);

            Children[3] = new QuadTreeNode(
                new Vector2(Center.x + halfSize.x, Center.y - halfSize.y),
                quarterSize, Capacity);


            foreach (var child in Children)
            {
                child.Parent = this; // 设置父节点引用
            }
        }

    

        // 检查点是否在节点范围内
        public bool Contains(Vector2 point)
        {
            return Mathf.Abs(point.x - Center.x) <= Size.x * 0.5f &&
                   Mathf.Abs(point.y - Center.y) <= Size.y * 0.5f;
        }
       
        // 更新高度方法
        public void SetHeight(float height, bool isObstacle)
        {
            if (isObstacle)
            {
                // 叠加高度
                Height += height;
            }
            else
            {
                Height = 0;
            }
        }

        // 新增射线检测函数，用于检查是否可行走
        private bool CheckWalkableLayer()
        {
            // 此处可行走条件为：1.节点被照亮 2.射线与指定层级发生碰撞
            if (!IsIlluminated) return false;
            
            // 射线起点（节点中心上方）
            Vector3 rayStart = new Vector3(Center.x, 10f, Center.y);
            // 射线方向（向下）
            Vector3 rayDir = Vector3.down;
            // 射线最大距离
            float maxDistance = 20f;
            
            // 创建射线
            RaycastHit hit;
            RaycastHit waterHit;
            // 写死要检测的层级，这里使用第8层和第9层
            int walkableLayerMask = 1 << 8; // 8代表你要检测的层级
            int waterLayerMask = 1 << 9; // 9代表水层级
            
            // 发射射线检测地面
            bool hitGround = Physics.Raycast(rayStart, rayDir, out hit, maxDistance, walkableLayerMask);
            // 发射射线检测水面
            bool hitWater = Physics.Raycast(rayStart, rayDir, out waterHit, maxDistance, waterLayerMask);
            
            // 如果击中水面，并且地面也击中，比较两者的y值
            if (hitGround && hitWater)
            {
                // 如果地面高度小于水面高度，则不可行走
                if (hit.point.y < waterHit.point.y)
                {
                    return false;
                }
            }
            
            // 返回是否与指定层级碰撞
            return hitGround;
        }

    }
    #endregion

    #region 四叉树类相关
    // 根节点和最大深度
    private QuadTreeNode root;
    private int maxDepth;

    // 新增根节点尺寸访问属性
    public Vector2 RootSize { get; private set; }
    public Vector2 RootCenter { get; private set; }
    public int MaxDepth { get; private set; }

    // 添加最小节点尺寸属性
    public Vector2 MinNodeSize { get; private set; }

    // 修改GetNeighbors方法，添加缓存机制
    private Dictionary<QuadTreeNode, List<QuadTreeNode>> neighborCache = new Dictionary<QuadTreeNode, List<QuadTreeNode>>();

    public QuadTree(Vector2 center, Vector2 size, int capacity, int maxDepth = 5, bool preSplit = false)
    {
        RootSize = size;
        RootCenter = center;
        MaxDepth = maxDepth;
        // 计算最小节点尺寸
        MinNodeSize = size / Mathf.Pow(2, maxDepth);
        root = new QuadTreeNode(center, size, capacity);
        this.maxDepth = maxDepth;
        
        // 新增预分裂功能
        if(preSplit)
        {
            PreSplitRecursive(root, 0);
        }
    }

    // 新增预分裂方法
    private void PreSplitRecursive(QuadTreeNode node, int currentDepth)
    {
        if(currentDepth >= maxDepth) return;
        
        node.Split();
        foreach(var child in node.Children)
        {
            PreSplitRecursive(child, currentDepth + 1);
        }
    }

    // 修改后的插入方法
    public bool Insert(GameObject obj, bool adjustPosition = true, int currentDepth = 0)
    {
        // if(adjustPosition)
        // {
        //     Vector2 targetCenter = CalculateFinalNodeCenter(obj.transform.position);
        //     Vector3 newPos = new Vector3(targetCenter.x, obj.transform.position.y, targetCenter.y);
        //     obj.transform.position = newPos;
        // }

        neighborCache.Clear();
        return InsertRecursive(root, new Vector2(obj.transform.position.x, obj.transform.position.z), obj, currentDepth);
    }

    // 新增方法：计算理论最终节点中心
    private Vector2 CalculateFinalNodeCenter(Vector3 worldPosition)
    {
        Vector2 currentCenter = root.Center;
        Vector2 currentSize = root.Size;
        Vector2 position = new Vector2(worldPosition.x, worldPosition.z);
        
        // 遍历到最大深度
        for (int depth = 0; depth < maxDepth; depth++)
        {
            currentSize *= 0.5f; // 每层尺寸减半
            Vector2 offset = position - currentCenter;
            
            // 确定象限
            int quadrant = (offset.x > 0 ? 0 : 1) + (offset.y > 0 ? 0 : 2);
            
            // 更新中心点坐标
            currentCenter += new Vector2(
                (quadrant % 2 == 0 ? 1 : -1) * currentSize.x * 0.5f,
                (quadrant < 2 ? 1 : -1) * currentSize.y * 0.5f
            );
        }
        
        return currentCenter;
    }

    private bool InsertRecursive(QuadTreeNode node, Vector2 position, GameObject obj, int depth)
    {
        if (!node.Contains(position)) return false;

        if (node.Children == null)
        {
            if (node.Objects.Count < node.Capacity || depth >= maxDepth)
            {
                node.Objects.Add(obj);
                return true;
            }

            node.Split();
            RedistributeObjects(node);
        }

        foreach (var child in node.Children)
        {
            if (InsertRecursive(child, position, obj, depth + 1))
            {
                return true;
            }
        }
        return false;
    }

       // 新增移除方法
    public bool Remove(GameObject obj)
    {
        Vector3 pos = obj.transform.position;
        Vector2 position = new Vector2(pos.x, pos.z);
        neighborCache.Clear();
        return RemoveRecursive(root, position, obj);
    }

    private bool RemoveRecursive(QuadTreeNode node, Vector2 position, GameObject obj)
    {
        if (!node.Contains(position)) return false;

        // 尝试在当前节点移除
        if (node.Objects.Remove(obj))
        {
            return true;
        }

        // 如果有子节点则递归查找
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                if (RemoveRecursive(child, position, obj))
                {
                    return true;
                }
            }
        }
        return false;
    }


    // 重新分配对象到子节点
    private void RedistributeObjects(QuadTreeNode node)
    {
        // 过滤掉已销毁的对象
        node.Objects.RemoveAll(obj => obj == null);
        
        List<GameObject> objectsToRedistribute = new List<GameObject>(node.Objects);
        node.Objects.Clear();

        foreach (var obj in objectsToRedistribute)
        {
            // 再次检查确保对象未被销毁
            if (obj == null)
                continue;
            
            Vector3 objPos = obj.transform.position;
            Vector2 position = new Vector2(objPos.x, objPos.z);
            
            bool redistributed = false;

            // 尝试将对象分配到子节点
            foreach (var child in node.Children)
            {
                if (child.Contains(position))
                {
                    child.Objects.Add(obj);
                    redistributed = true;
                    break;
                }
            }

            // 如果无法分配到任何子节点，保留在父节点
            if (!redistributed)
            {
                node.Objects.Add(obj);
            }
        }
    }

    // 查询区域内的对象
    public List<GameObject> QueryArea(Bounds area)
    {
        List<GameObject> results = new List<GameObject>();
        QueryAreaRecursive(root, area, ref results);
        return results;
    }

    private void QueryAreaRecursive(QuadTreeNode node, Bounds area, ref List<GameObject> results)
    {
        // 创建节点对应的AABB边界
        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x * 0.5f,
            node.Center.y - node.Size.y * 0.5f,
            node.Size.x,
            node.Size.y);

        // 创建3D边界框用于检测（XZ平面）
        Bounds nodeBounds = new Bounds(
            new Vector3(node.Center.x, 0, node.Center.y), // 中心点转换
            new Vector3(node.Size.x, 0, node.Size.y));    // 尺寸转换

        // 使用修正后的边界进行检测
        if (!area.Intersects(nodeBounds))
        {
            return;
        }

        // 如果有子节点则递归查询
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                QueryAreaRecursive(child, area, ref results);
            }
        }
        else
        {
            // 添加当前节点内的有效对象
            foreach (var obj in node.Objects)
            {
                Vector3 objPos = obj.transform.position;
                if (area.Contains(new Vector3(objPos.x, 0, objPos.z)))
                {
                    results.Add(obj);
                }
            }
        }
    }
    #endregion

    #region Gizmos绘制
    // 调试绘制
    public void DrawGizmos()
    {
        DrawNodeGizmos(root, 0);
    }

    private void DrawNodeGizmos(QuadTreeNode node, int depth)
    {
        if (node == null) return;

        // 根据深度设置不同颜色
        // Color[] depthColors = { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan };
        // Gizmos.color = depthColors[Mathf.Clamp(depth, 0, depthColors.Length - 1)];

        // 绘制节点边界
        Vector3 center = new Vector3(node.Center.x, 0, node.Center.y);
        Vector3 size = new Vector3(node.Size.x, 0.1f, node.Size.y);
        // Gizmos.DrawWireCube(center, size);

        // 显示对象数量
        // GUIStyle style = new GUIStyle();
        // style.normal.textColor = Gizmos.color;
        // Handles.Label(
        //     new Vector3(node.Center.x, 0,node.Center.y), 
        //     $"{node.Objects.Count}",
        //     style);

        // 根据光照状态改变颜色
        Gizmos.color = node.IsIlluminated ? new Color(1, 0.9f, 0.5f, 1.0f) : new Color(0,0,0,0.1f);
        Gizmos.DrawWireCube(center, size * 1.0f);

        // 修改为数字高度显示
        if (node.Height > 0.01 && node.Size==MinNodeSize)
        {
            // 在节点中心上方显示高度值
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.green;
            style.fontSize = Mathf.RoundToInt(12 * (node.Size.x / MinNodeSize.x)); // 根据节点尺寸自动调整字体大小
            style.alignment = TextAnchor.MiddleCenter;
            style.fontStyle = FontStyle.Bold;
            
            // 显示两位小数的高度值
            Handles.Label(
                center + Vector3.up * 0.2f, // 稍微抬高避免重叠
                node.Height.ToString("F2"), 
                style);
            
            // 保留线框显示（可选）
            Gizmos.color = new Color(0, 0.5f, 0, 0.2f);
            Gizmos.DrawWireCube(center, new Vector3(node.Size.x, 0, node.Size.y));
        }

        // 递归绘制子节点
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                DrawNodeGizmos(child, depth + 1);
            }
        }
    }
    #endregion

    #region 光照标记方法
    // 修改后的光照标记方法,支持旋转
    public float MarkIlluminatedArea(Lighting lighting, bool isAdditive = true, bool useCachedData = false)
    {
        var area = useCachedData ? lighting.GetCachedWorldBounds() : lighting.GetWorldBounds();
        float rotation = useCachedData ? lighting.GetCachedRotation() : lighting.rotation;
        
        // 传递旋转参数到预分裂方法
        PreSplitForLighting(root, area, rotation, 0);
        return FinalizeIlluminationMarking(lighting, isAdditive, useCachedData);
    }

    // 修改预分裂方法，加入旋转参数
    private void PreSplitForLighting(QuadTreeNode node, Bounds area, float rotation, int currentDepth)
    {
        Vector2 rectCenter = new Vector2(area.center.x, area.center.z);
        Vector2 rectSize = new Vector2(area.size.x, area.size.z);

        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x/2,
            node.Center.y - node.Size.y/2,
            node.Size.x,
            node.Size.y);

        // 使用支持旋转的碰撞检测
        bool overlap = RectangleRectOverlap(rectCenter, rectSize, nodeRect, rotation);
        
        if (!overlap) return;

        if (currentDepth < maxDepth)
        {
            if (node.Children == null)
            {
                node.Split();
                RedistributeObjects(node);
            }
            
            foreach (var child in node.Children)
            {
                PreSplitForLighting(child, area, rotation, currentDepth + 1);
            }
        }
    }

    // FinalMarkRecursive方法的UV计算部分需要考虑旋转
    private float FinalizeIlluminationMarking(Lighting lighting, bool isAdditive = true, bool useCachedData = false)
    {
        float totalBrightness = 0f;
        FinalMarkRecursive(root, lighting, ref totalBrightness, isAdditive, useCachedData);
        return totalBrightness;
    }

    private void FinalMarkRecursive(QuadTreeNode node, Lighting lighting, ref float totalBrightness, bool isAdditive = true, bool useCachedData = false)
    {
        var area = useCachedData ? lighting.GetCachedWorldBounds() : lighting.GetWorldBounds();
        var mapData = useCachedData ? lighting.GetCachedAreaMapData() : lighting.GetAreaMapData();
        bool isObstacle = useCachedData ? lighting.GetCachedIsObstacle() : lighting.isObstacle;
        
        Vector2 rectCenter = new Vector2(area.center.x, area.center.z);
        Vector2 rectSize = new Vector2(area.size.x, area.size.z);
        float rotation = useCachedData ? lighting.GetCachedRotation() : lighting.rotation;

        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x/2,
            node.Center.y - node.Size.y/2,
            node.Size.x,
            node.Size.y);

        bool overlap = RectangleRectOverlap(rectCenter, rectSize, nodeRect, rotation);
        
        if (!overlap) return;

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                FinalMarkRecursive(child, lighting, ref totalBrightness, isAdditive, useCachedData);
            }
        }
        else
        { 
            // 计算UV坐标（考虑旋转）
            Vector2 nodePos = new Vector2(node.Center.x, node.Center.y);
            Vector2 localPos = nodePos - rectCenter;
            
            // 应用反向旋转变换以获取正确的UV坐标
            float rotationRad = -rotation * Mathf.Deg2Rad; // 负号是为了反向旋转
            Vector2 rotatedPos = new Vector2(
                localPos.x * Mathf.Cos(rotationRad) - localPos.y * Mathf.Sin(rotationRad),
                localPos.x * Mathf.Sin(rotationRad) + localPos.y * Mathf.Cos(rotationRad)
            );
            
            // 计算UV坐标
            Vector2 uv = new Vector2(
                (rotatedPos.x / (area.size.x * 0.5f)) * 0.5f + 0.5f,
                (rotatedPos.y / (area.size.z * 0.5f)) * 0.5f + 0.5f
            );

            // 边界约束确保UV在0-1范围内
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);

            // 从高度图采样原始值
            float rawHeight = mapData.heightMap != null ? 
                mapData.heightMap.GetPixelBilinear(uv.x, uv.y).r : 0f;

            // 添加容差处理（处理浮点精度）
            rawHeight = Mathf.Clamp01(rawHeight);
            if(rawHeight < 0.003f) rawHeight = 0;

            // 设置节点属性
            if (isObstacle)
            {
                float height = rawHeight;
                // 根据加减法标志决定操作
                if (isAdditive)
                    node.SetHeight(height, true);
                else {
                    // 减法操作，减少高度
                    node.SetHeight(-height, true);
                }
                totalBrightness +=0.01f;
            }
            else
            {
                 float centerHeight = GetNodeHeightAtPosition(new Vector3(area.center.x, 0, area.center.z));             
                // 第一层：高度条件判断 限制在0-1之间
                if (Mathf.Clamp01(area.size.y+centerHeight)>= Mathf.Clamp01(node.Height))
                {
                    // 根据加减法标志决定亮度操作
                    if (isAdditive) {
                        // 累加原始亮度值到总影响
                        totalBrightness += rawHeight;
                        node.Brightness += rawHeight;
                    } else {
                        // 减法操作，减少亮度但不低于0
                        totalBrightness += rawHeight;
                        node.Brightness -= rawHeight;
                    }
                    
                    // 使用亮度阈值判断光照状态
                    node.IsIlluminated = node.Brightness >= node.BrightnessThreshold;
                }
            }
        }
    }
    #endregion
       
    #region 路径规划
    // 新增调试计时器数据结构
    public class PathfindingDebugInfo
    {
        public float TotalTime = 0;
        public float NodeFindingTime = 0;
        public float PathfindingTime = 0;
        public float PathSimplificationTime = 0;
        public int NodesEvaluated = 0;
        public int OpenListMaxCount = 0;
        public int ClosedSetMaxCount = 0;
        public int NeighborEvaluations = 0;
        public int LineOfSightChecks = 0;
        public int FinalPathLength = 0;
        public int OriginalPathLength = 0;
    }

    // 在类中添加最新的调试信息实例
    public PathfindingDebugInfo LastPathfindingInfo { get; private set; } = new PathfindingDebugInfo();

    // 修改寻路方法，添加性能计时
    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        // 添加超时处理
        int maxIterations = 3000; // 最大迭代次数
        int iterations = 0;
        
        // 总体计时器
        Stopwatch totalTimer = new Stopwatch();
        Stopwatch stepTimer = new Stopwatch();
        PathfindingDebugInfo debugInfo = new PathfindingDebugInfo();
        
        totalTimer.Start();
        
        // 节点查找计时
        stepTimer.Start();
        var startNode = FindLeafNode(startPos);
        var targetNode = FindLeafNode(targetPos);
        
        // 修改目标节点处理逻辑
        if (targetNode == null || !targetNode.IsWalkable || targetNode.Size != MinNodeSize)
        {
            // 在目标位置周围3倍节点尺寸范围内寻找最近的可行走节点
            var candidates = GetNeighborLeafNodes(targetPos, MinNodeSize.x * 3f)
                .Where(n => n.IsWalkable && n.Size == MinNodeSize)
                .OrderBy(n => Vector3.Distance(
                    new Vector3(n.Center.x, 0, n.Center.y), 
                    targetPos))
                .ToList();

            if (candidates.Count == 0) return null;
            targetNode = candidates.First();
        }
        
        // 直接返回null如果目标节点不可行走
        if (targetNode == null || !targetNode.IsWalkable || targetNode.Size != MinNodeSize)
        {
            return null;
        }
        
        debugInfo.NodeFindingTime = stepTimer.ElapsedMilliseconds;
        stepTimer.Reset();
        
        // A*寻路计时
        stepTimer.Start();
        
        var openList = new List<QuadTreeNode>();
        var closedSet = new HashSet<QuadTreeNode>();
        // 初始化节点数据
        ResetPathfindingData();
        
        startNode.GCost = 0;
        startNode.HCost = Heuristic(startNode, targetNode);
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            iterations++;
            if (iterations > maxIterations)
            {
                UnityEngine.Debug.Log("寻路超时，返回部分路径");
                // 如果超时，尝试返回到目前为止找到的最佳路径
                var bestNode = openList.OrderBy(n => n.HCost).First();
                List<Vector3> partialPath = RetracePath(startNode, bestNode);
                return partialPath;
            }
            
            // 更新最大列表大小
            debugInfo.OpenListMaxCount = Mathf.Max(debugInfo.OpenListMaxCount, openList.Count);
            debugInfo.ClosedSetMaxCount = Mathf.Max(debugInfo.ClosedSetMaxCount, closedSet.Count);
            
            var currentNode = openList.OrderBy(n => n.FCost).First();
            debugInfo.NodesEvaluated++;
            
            if (currentNode == targetNode){
                List<Vector3> path = RetracePath(startNode, targetNode);
                debugInfo.OriginalPathLength = path.Count;
                
                // 寻路部分用时
                debugInfo.PathfindingTime = stepTimer.ElapsedMilliseconds;
                stepTimer.Reset();
                
                // 路径简化计时
                stepTimer.Start();
                var simplifiedPath = SimplifyPath(path);
                debugInfo.PathSimplificationTime = stepTimer.ElapsedMilliseconds;
                debugInfo.FinalPathLength = simplifiedPath.Count;
                
                // 记录总用时
                totalTimer.Stop();
                debugInfo.TotalTime = totalTimer.ElapsedMilliseconds;
                
                // 保存调试信息
                LastPathfindingInfo = debugInfo;
                
                return simplifiedPath;
            }

            openList.Remove(currentNode);
            closedSet.Add(currentNode);

            foreach (var neighbor in GetNeighbors(currentNode))
            {
                debugInfo.NeighborEvaluations++;
                
                if (!neighbor.IsWalkable || closedSet.Contains(neighbor))
                    continue;

                float tentativeGCost = currentNode.GCost + Heuristic(currentNode, neighbor);
                
                // Theta*核心优化
                if (currentNode.ParentNode != null && 
                    HasLineOfSight(currentNode.ParentNode, neighbor))
                {
                    debugInfo.LineOfSightChecks++;
                    float alternativeCost = currentNode.ParentNode.GCost + 
                                          Heuristic(currentNode.ParentNode, neighbor);
                    if (alternativeCost < tentativeGCost)
                    {
                        tentativeGCost = alternativeCost;
                        neighbor.ParentNode = currentNode.ParentNode;
                    }
                }

                if (tentativeGCost < neighbor.GCost)
                {
                    neighbor.GCost = tentativeGCost;
                    neighbor.HCost = Heuristic(neighbor, targetNode);
                    neighbor.ParentNode = currentNode;

                    if (!openList.Contains(neighbor))
                        openList.Add(neighbor);
                }
            }
        }
        
        // 如果寻路失败
        totalTimer.Stop();
        debugInfo.TotalTime = totalTimer.ElapsedMilliseconds;
        debugInfo.PathfindingTime = stepTimer.ElapsedMilliseconds;
        LastPathfindingInfo = debugInfo;
        
        return null;
    }

    // 新增私有辅助方法
    public QuadTreeNode FindLeafNode(Vector3 position)
    {
        Vector2 pos = new Vector2(position.x, position.z);
        return FindLeafRecursive(root, pos);
    }

    private QuadTreeNode FindLeafRecursive(QuadTreeNode node, Vector2 pos)
    {
        if (!node.Contains(pos)) return null;
        return node.Children == null ? 
            node : 
            node.Children.Select(child => FindLeafRecursive(child, pos))
                         .FirstOrDefault(result => result != null);
    }

    private List<QuadTreeNode> GetNeighbors(QuadTreeNode node)
    {
        if (neighborCache.TryGetValue(node, out var cached))
            return cached;

        var neighbors = new List<QuadTreeNode>(8);
        // 只获取四个主方向邻居，减少对角线邻居
        Vector2[] directions = new Vector2[] {
            new Vector2(node.Size.x, 0),          // 右
            new Vector2(-node.Size.x, 0),         // 左
            new Vector2(0, node.Size.x),          // 上
            new Vector2(0, -node.Size.x),         // 下
        };
        
        foreach (var dir in directions)
        {
            Vector2 neighborPos = node.Center + dir;
            var neighborNode = FindLeafNode(new Vector3(neighborPos.x, 0, neighborPos.y));
            if (neighborNode != null && neighborNode.IsWalkable && 
                IsHeightAccessible(node, neighborNode))
            {
                neighbors.Add(neighborNode);
            }
        }
        
        neighborCache[node] = neighbors;
        return neighbors;
    }

    private bool HasLineOfSight(QuadTreeNode from, QuadTreeNode to)
    {
        // 如果距离很近，直接返回true
        float distance = Vector2.Distance(from.Center, to.Center);
        if (distance < MinNodeSize.x * 3) // 增加直接返回的距离阈值
            return true;
        
        // 使用更大的步长
        float step = Mathf.Max(MinNodeSize.x, distance / 3); // 增加步长
        
        QuadTreeNode prevNode = from;
        
        int checkCount = 0;
        for (float t = 0; t <= 1; t += step / distance)
        {
            checkCount++;
            Vector2 point = Vector2.Lerp(from.Center, to.Center, t);
            var node = FindLeafNode(new Vector3(point.x, 0, point.y));
            
            if (node == null || !node.IsWalkable)
                return false;
            
            // 检查与前一个节点的高度差是否可接受
            if (!IsHeightAccessible(prevNode, node))
                return false;
            
            prevNode = node;
        }
        
        // 增加统计值
        LastPathfindingInfo.LineOfSightChecks += checkCount;
        
        return true;
    }

    private List<Vector3> RetracePath(QuadTreeNode startNode, QuadTreeNode endNode)
    {
        List<Vector3> path = new List<Vector3>();
        var currentNode = endNode;

        while (currentNode != null && currentNode != startNode)
        {
            // 在路径点中包含高度信息
            path.Add(new Vector3(currentNode.Center.x, currentNode.Height, currentNode.Center.y));
            currentNode = currentNode.ParentNode;
        }
        
        // 添加起点（包含高度）
        if (currentNode == startNode)
        {
            path.Add(new Vector3(startNode.Center.x, startNode.Height, startNode.Center.y));
        }
        
        path.Reverse();
        return SimplifyPath(path);
    }

    private List<Vector3> SimplifyPath(List<Vector3> path)
    {
        if (path.Count < 3) return path;
        
        //输出路径信息

        List<Vector3> simplified = new List<Vector3> { path[0] };
        for (int i = 1; i < path.Count - 1; i++)
        {
            // 严格检测中间节点是否真正可跳过
            if (!HasDirectPath(simplified.Last(), path[i + 1]))
                simplified.Add(path[i]);
        }
        simplified.Add(path.Last());
        return simplified;
    }

    private bool HasDirectPath(Vector3 a, Vector3 b)
    {
        Vector2 start = new Vector2(a.x, a.z);
        Vector2 end = new Vector2(b.x, b.z);
        float step = 0.5f;
        float distance = Vector2.Distance(start, end);
        
        QuadTreeNode prevNode = FindLeafNode(a);
        if (prevNode == null) return false;
        
        for (float t = 0; t <= 1; t += step / distance)
        {
            Vector2 point = Vector2.Lerp(start, end, t);
            var node = FindLeafNode(new Vector3(point.x, 0, point.y));
            
            if (node == null || !node.IsWalkable) 
                return false;
            
            // 检查高度可达性
            if (prevNode != null && !IsHeightAccessible(prevNode, node))
                return false;
            
            prevNode = node;
        }
        return true;
    }

    private float Heuristic(QuadTreeNode a, QuadTreeNode b)
    {
        // 基础距离计算
        float baseDistance = Vector2.Distance(a.Center, b.Center);
        
        // 减小高度差异惩罚
        float heightDifference = Mathf.Abs(a.Height - b.Height);
        float heightPenalty = heightDifference * 1.0f; // 从2.0降低到1.0
        
        return baseDistance + heightPenalty;
    }

    private void ResetPathfindingData()
    {
        ResetNodeDataRecursive(root);
    }

    private void ResetNodeDataRecursive(QuadTreeNode node)
    {
        node.GCost = Mathf.Infinity;
        node.HCost = 0;
        node.ParentNode = null;
        
        if (node.Children != null)
        {
            foreach (var child in node.Children)
                ResetNodeDataRecursive(child);
        }
    }

    private QuadTreeNode FindNearestWalkableNode(Vector3 position, float radius)
    {
        var candidates = GetNeighborLeafNodes(position, radius)
            .Where(n => n.IsWalkable)
            .OrderBy(n => Vector3.Distance(
                new Vector3(n.Center.x, 0, n.Center.y), 
                position));
        
        return candidates.FirstOrDefault();
    }

    #endregion

    #region 其他辅助方法
    // 新增方法：获取指定位置最小尺寸节点的高度
    public float GetNodeHeightAtPosition(Vector3 position)
    {
        QuadTreeNode node = FindLeafNode(position);
        return node != null ? node.Height : 0f;
    }

    // 修改遍历方法
    private void ForEachNodeInArea(Bounds area, bool isRect, System.Action<QuadTreeNode> action)
    {
        ForEachNodeInAreaRecursive(root, area, isRect, action);
    }

    private void ForEachNodeInAreaRecursive(QuadTreeNode node, Bounds area, bool isRect, System.Action<QuadTreeNode> action)
    {
        Vector2 center = new Vector2(area.center.x, area.center.z);
        Vector2 size = new Vector2(area.size.x, area.size.z);
        float radius = area.size.x * 0.5f;

        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x/2,
            node.Center.y - node.Size.y/2,
            node.Size.x,
            node.Size.y);

        bool overlap = isRect ?
            RectangleRectOverlap(center, size, nodeRect) :
            CircleRectOverlap(center, radius, nodeRect);

        if (!overlap) return;

        action(node);

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                ForEachNodeInAreaRecursive(child, area, isRect, action);
            }
        }
    }

    // 圆形与矩形碰撞检测
    private bool CircleRectOverlap(Vector2 circlePos, float radius, Rect rect)
    {
        // 先进行快速排除
        float dx = Mathf.Abs(circlePos.x - rect.center.x);
        float dy = Mathf.Abs(circlePos.y - rect.center.y);

        if (dx >= (rect.width/2 + radius)) return false;
        if (dy >= (rect.height/2 + radius)) return false;

        if (dx < (rect.width/2)) return true;
        if (dy < (rect.height/2)) return true;

        float cornerDistSq = Mathf.Pow(dx - rect.width/2, 2) +
                           Mathf.Pow(dy - rect.height/2, 2);

        return cornerDistSq <= (radius * radius);
    }

    // 修改RectangleRectOverlap方法支持旋转
    private bool RectangleRectOverlap(Vector2 rectCenter, Vector2 rectSize, Rect targetRect, float rotation = 0f)
    {
        // 对于简单的情况，如果旋转角度接近0或180度，可以使用AABB快速检测
        if (Mathf.Approximately(rotation % 180f, 0f))
        {
            return RectangleRectOverlapNoRotation(rectCenter, rectSize, targetRect);
        }
        
        // 使用分离轴定理进行旋转矩形碰撞检测
        Vector2 targetCenter = new Vector2(targetRect.center.x, targetRect.center.y);
        Vector2 targetSize = new Vector2(targetRect.width, targetRect.height);
        
        // 获取旋转矩形的四个顶点
        Vector2[] cornersA = GetRotatedRectCorners(rectCenter, rectSize, rotation * Mathf.Deg2Rad);
        Vector2[] cornersB = new Vector2[4] {
            new Vector2(targetRect.xMin, targetRect.yMin),
            new Vector2(targetRect.xMax, targetRect.yMin),
            new Vector2(targetRect.xMax, targetRect.yMax),
            new Vector2(targetRect.xMin, targetRect.yMax)
        };
        
        // 分离轴定理检测
        // 检查A的两个轴
        Vector2 axisA1 = (cornersA[1] - cornersA[0]).normalized;
        Vector2 axisA2 = (cornersA[3] - cornersA[0]).normalized;
        
        if (!OverlapOnAxis(cornersA, cornersB, axisA1)) return false;
        if (!OverlapOnAxis(cornersA, cornersB, axisA2)) return false;
        
        // 检查B的两个轴
        Vector2 axisB1 = Vector2.right;
        Vector2 axisB2 = Vector2.up;
        
        if (!OverlapOnAxis(cornersA, cornersB, axisB1)) return false;
        if (!OverlapOnAxis(cornersA, cornersB, axisB2)) return false;
        
        // 所有轴都有重叠，表示矩形相交
        return true;
    }

    // 原始的非旋转矩形重叠检测（保留用于快速检测）
    private bool RectangleRectOverlapNoRotation(Vector2 rectCenter, Vector2 rectSize, Rect targetRect)
    {
        float halfWidth = rectSize.x * 0.5f;
        float halfHeight = rectSize.y * 0.5f;
        
        float left = rectCenter.x - halfWidth;
        float right = rectCenter.x + halfWidth;
        float bottom = rectCenter.y - halfHeight;
        float top = rectCenter.y + halfHeight;
        
        return !(right < targetRect.xMin || left > targetRect.xMax || 
                 top < targetRect.yMin || bottom > targetRect.yMax);
    }

    // 计算旋转矩形的四个顶点
    private Vector2[] GetRotatedRectCorners(Vector2 center, Vector2 size, float rotation)
    {
        Vector2[] corners = new Vector2[4];
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        
        // 计算旋转后的四个角
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        
        corners[0] = center + new Vector2(cos * -halfWidth - sin * -halfHeight, sin * -halfWidth + cos * -halfHeight);
        corners[1] = center + new Vector2(cos * halfWidth - sin * -halfHeight, sin * halfWidth + cos * -halfHeight);
        corners[2] = center + new Vector2(cos * halfWidth - sin * halfHeight, sin * halfWidth + cos * halfHeight);
        corners[3] = center + new Vector2(cos * -halfWidth - sin * halfHeight, sin * -halfWidth + cos * halfHeight);
        
        return corners;
    }

    // 检查两组顶点在某一轴上是否重叠
    private bool OverlapOnAxis(Vector2[] cornersA, Vector2[] cornersB, Vector2 axis)
    {
        // 计算A投影的最小和最大值
        float minA = float.MaxValue;
        float maxA = float.MinValue;
        
        foreach (Vector2 corner in cornersA)
        {
            float projection = Vector2.Dot(corner, axis);
            minA = Mathf.Min(minA, projection);
            maxA = Mathf.Max(maxA, projection);
        }
        
        // 计算B投影的最小和最大值
        float minB = float.MaxValue;
        float maxB = float.MinValue;
        
        foreach (Vector2 corner in cornersB)
        {
            float projection = Vector2.Dot(corner, axis);
            minB = Mathf.Min(minB, projection);
            maxB = Mathf.Max(maxB, projection);
        }
        
        // 检查投影是否重叠
        return maxA >= minB && maxB >= minA;
    }

    // 重置光照状态
    public void ResetIllumination()
    {
        ResetIlluminationRecursive(root);
    }

    private void ResetIlluminationRecursive(QuadTreeNode node)
    {
        node.SetHeight(0, false);
        node.IsIlluminated = false;
        node.Brightness = 0;
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                ResetIlluminationRecursive(child);
            }
        }
    }


    // 新增方法：获取所有被光照的叶子节点
    public List<QuadTreeNode> GetIlluminatedLeafNodes()
    {
        List<QuadTreeNode> result = new List<QuadTreeNode>();
        GetLeafNodesRecursive(root, result);
        return result;
    }

    private void GetLeafNodesRecursive(QuadTreeNode node, List<QuadTreeNode> result)
    {
        if (node.Children == null)
        {
            if (node.IsIlluminated)
            {
                result.Add(node);
            }
        }
        else
        {
            foreach (var child in node.Children)
            {
                GetLeafNodesRecursive(child, result);
            }
        }
    }

    // 检查指定位置是否被照亮
    public bool IsPositionIlluminated(Vector3 worldPos)
    {
        Vector2 pos = new Vector2(worldPos.x, worldPos.z);
        return CheckIlluminationRecursive(root, pos);
    }

    private bool CheckIlluminationRecursive(QuadTreeNode node, Vector2 pos)
    {
        if (!node.Contains(pos)) return false;
        
        if (node.Children == null)
        {
            return node.IsIlluminated;
        }

        foreach (var child in node.Children)
        {
            if (CheckIlluminationRecursive(child, pos))
            {
                return true;
            }
        }
        return false;
    }

    // 新增方法：获取指定区域内的叶子节点
    public List<QuadTreeNode> GetNeighborLeafNodes(Bounds area)
    {
        Vector2 rectCenter = new Vector2(area.center.x, area.center.z);
        Vector2 rectSize = new Vector2(area.size.x, area.size.z);
        List<QuadTreeNode> result = new List<QuadTreeNode>();
        FindNeighborLeafNodes(root, rectCenter, rectSize, result);
        return result;
    }

    private void FindNeighborLeafNodes(QuadTreeNode node, Vector2 rectCenter, Vector2 rectSize, List<QuadTreeNode> result)
    {
        if (node == null) return;

        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x/2,
            node.Center.y - node.Size.y/2,
            node.Size.x,
            node.Size.y);

        if (!RectangleRectOverlap(rectCenter, rectSize, nodeRect)) return;

        if (node.Children == null)
        {
            result.Add(node);
        }
        else
        {
            foreach (var child in node.Children)
            {
                FindNeighborLeafNodes(child, rectCenter, rectSize, result);
            }
        }
    }

    // 保留原有方法作为重载，以兼容现有代码
    public List<QuadTreeNode> GetNeighborLeafNodes(Vector3 position, float radius)
    {
        Vector2 pos = new Vector2(position.x, position.z);
        List<QuadTreeNode> result = new List<QuadTreeNode>();
        FindNeighborLeafNodesCircle(root, pos, radius, result);
        return result;
    }

    private void FindNeighborLeafNodesCircle(QuadTreeNode node, Vector2 position, float radius, List<QuadTreeNode> result)
    {
        if (node == null) return;

        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x/2,
            node.Center.y - node.Size.y/2,
            node.Size.x,
            node.Size.y);

        if (!CircleRectOverlap(position, radius, nodeRect)) return;

        if (node.Children == null)
        {
            result.Add(node);
        }
        else
        {
            foreach (var child in node.Children)
            {
                FindNeighborLeafNodesCircle(child, position, radius, result);
            }
        }
    }

    // 新增方法：判断两个节点间的高度是否可达
    private bool IsHeightAccessible(QuadTreeNode from, QuadTreeNode to)
    {
        // 定义最大可攀爬高度差
        float maxClimbableHeight = 0.35f; // 可以根据需要调整
        
        // 计算高度差
        float heightDifference = Mathf.Abs(from.Height - to.Height);
        
        // 如果高度差超过最大可攀爬高度，则不可达
        return heightDifference <= maxClimbableHeight;
    }

    // 新增方法：确保指定区域内的节点完全分裂到最小尺寸
    public void PreSplitArea(Bounds area)
    {
        Vector2 rectCenter = new Vector2(area.center.x, area.center.z);
        Vector2 rectSize = new Vector2(area.size.x, area.size.z);
        PreSplitAreaRecursive(root, rectCenter, rectSize, 0);
    }

    private void PreSplitAreaRecursive(QuadTreeNode node, Vector2 rectCenter, Vector2 rectSize, int currentDepth)
    {
        if (node == null) return;

        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x/2,
            node.Center.y - node.Size.y/2,
            node.Size.x,
            node.Size.y);

        if (!RectangleRectOverlap(rectCenter, rectSize, nodeRect)) return;

        // 如果当前深度小于最大深度且节点没有子节点，则分裂
        if (currentDepth < maxDepth && node.Children == null)
        {
            node.Split();
            RedistributeObjects(node);
        }

        // 如果有子节点，继续递归分裂
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                PreSplitAreaRecursive(child, rectCenter, rectSize, currentDepth + 1);
            }
        }
    }

    // 新增方法：获取指定旋转区域内的叶子节点
    public List<QuadTreeNode> GetNeighborLeafNodesWithRotation(Bounds area, float rotation)
    {
        Vector2 rectCenter = new Vector2(area.center.x, area.center.z);
        Vector2 rectSize = new Vector2(area.size.x, area.size.z);
        List<QuadTreeNode> result = new List<QuadTreeNode>();
        FindNeighborLeafNodesWithRotation(root, rectCenter, rectSize, rotation, result);
        return result;
    }

    private void FindNeighborLeafNodesWithRotation(QuadTreeNode node, Vector2 rectCenter, Vector2 rectSize, float rotation, List<QuadTreeNode> result)
    {
        if (node == null) return;

        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x/2,
            node.Center.y - node.Size.y/2,
            node.Size.x,
            node.Size.y);

        // 使用考虑旋转的碰撞检测
        if (!RectangleRectOverlap(rectCenter, rectSize, nodeRect, rotation)) return;

        if (node.Children == null)
        {
            result.Add(node);
        }
        else
        {
            foreach (var child in node.Children)
            {
                FindNeighborLeafNodesWithRotation(child, rectCenter, rectSize, rotation, result);
            }
        }
    }
    #endregion

    public List<Vector4> GetIlluminatedAreaBoundarySegments()
    {
        // 使用HashSet存储边界线段，可以避免重复
        HashSet<Vector4> boundarySegments = new HashSet<Vector4>();
        
        // 获取所有被照亮的叶子节点
        List<QuadTreeNode> illuminatedNodes = GetIlluminatedLeafNodes();
        
        // 遍历所有被照亮的节点
        foreach (var node in illuminatedNodes)
        {
            // 查找节点的四个边界，检查是否为边界线段
            Vector2 halfSize = node.Size * 0.5f;
            
            // 节点的四个顶点
            Vector2[] corners = new Vector2[4] {
                new Vector2(node.Center.x + halfSize.x, node.Center.y + halfSize.y), // 右上
                new Vector2(node.Center.x - halfSize.x, node.Center.y + halfSize.y), // 左上
                new Vector2(node.Center.x - halfSize.x, node.Center.y - halfSize.y), // 左下
                new Vector2(node.Center.x + halfSize.x, node.Center.y - halfSize.y)  // 右下
            };
            
            // 检查四条边是否为边界（通过检查相邻位置是否被照亮）
            // 上边界
            if (!IsPositionIlluminated(new Vector3(node.Center.x, 0, node.Center.y + node.Size.y)))
            {
                // 上边界是边缘，添加线段(右上 -> 左上)
                AddSegmentToSet(corners[0], corners[1], boundarySegments);
            }
            
            // 左边界
            if (!IsPositionIlluminated(new Vector3(node.Center.x - node.Size.x, 0, node.Center.y)))
            {
                // 左边界是边缘，添加线段(左上 -> 左下)
                AddSegmentToSet(corners[1], corners[2], boundarySegments);
            }
            
            // 下边界
            if (!IsPositionIlluminated(new Vector3(node.Center.x, 0, node.Center.y - node.Size.y)))
            {
                // 下边界是边缘，添加线段(左下 -> 右下)
                AddSegmentToSet(corners[2], corners[3], boundarySegments);
            }
            
            // 右边界
            if (!IsPositionIlluminated(new Vector3(node.Center.x + node.Size.x, 0, node.Center.y)))
            {
                // 右边界是边缘，添加线段(右下 -> 右上)
                AddSegmentToSet(corners[3], corners[0], boundarySegments);
            }
        }
        
        // 将HashSet转换为List返回
        return new List<Vector4>(boundarySegments);
    }

    // 辅助方法：按规范化顺序添加线段到集合
    private void AddSegmentToSet(Vector2 start, Vector2 end, HashSet<Vector4> segments)
    {
        // 确保线段表示的标准化（起点坐标小于终点坐标）
        if (start.x < end.x || (start.x == end.x && start.y < end.y))
        {
            segments.Add(new Vector4(start.x, start.y, end.x, end.y));
        }
        else
        {
            segments.Add(new Vector4(end.x, end.y, start.x, start.y));
        }
    }

    public List<Vector4> GetSimplifiedBoundarySegments(float targetSegmentLength = 1.0f)
    {
        // 第一步：获取原始边界线段
        List<Vector4> originalSegments = GetIlluminatedAreaBoundarySegments();
        if (originalSegments.Count == 0) return new List<Vector4>();
        
        // 第二步：构建连接的轮廓
        List<List<Vector2>> contours = BuildContours(originalSegments);
        
        // 第三步：对每个轮廓进行等长细分
        List<Vector4> simplifiedSegments = new List<Vector4>();
        foreach (var contour in contours)
        {
            if (contour.Count < 2) continue;
            
            // 计算轮廓总长度
            float totalLength = 0;
            for (int i = 0; i < contour.Count - 1; i++)
            {
                totalLength += Vector2.Distance(contour[i], contour[i + 1]);
            }
            // 闭合轮廓的最后一段
            totalLength += Vector2.Distance(contour[contour.Count - 1], contour[0]);
            
            // 计算需要的线段数量
            int segmentCount = Mathf.Max(3, Mathf.RoundToInt(totalLength / targetSegmentLength));
            
            // 创建等长线段
            List<Vector2> simplifiedPoints = new List<Vector2>();
            float distancePerSegment = totalLength / segmentCount;
            
            // 沿轮廓等距离采样点
            float accumulatedDistance = 0;
            int currentIndex = 0;
            simplifiedPoints.Add(contour[0]); // 添加起始点
            
            for (int i = 1; i <= segmentCount; i++)
            {
                float targetDistance = i * distancePerSegment;
                
                // 沿轮廓前进直到达到目标距离
                while (accumulatedDistance < targetDistance)
                {
                    int nextIndex = (currentIndex + 1) % contour.Count;
                    float segmentLength = Vector2.Distance(contour[currentIndex], contour[nextIndex]);
                    
                    if (accumulatedDistance + segmentLength >= targetDistance)
                    {
                        // 在当前线段上插值获取点
                        float t = (targetDistance - accumulatedDistance) / segmentLength;
                        Vector2 point = Vector2.Lerp(contour[currentIndex], contour[nextIndex], t);
                        simplifiedPoints.Add(point);
                        break;
                    }
                    
                    accumulatedDistance += segmentLength;
                    currentIndex = nextIndex;
                }
            }
            
            // 转换为线段
            for (int i = 0; i < simplifiedPoints.Count - 1; i++)
            {
                simplifiedSegments.Add(new Vector4(
                    simplifiedPoints[i].x, simplifiedPoints[i].y,
                    simplifiedPoints[i + 1].x, simplifiedPoints[i + 1].y));
            }
            
            // 闭合轮廓
            if (simplifiedPoints.Count > 1)
            {
                simplifiedSegments.Add(new Vector4(
                    simplifiedPoints[simplifiedPoints.Count - 1].x, simplifiedPoints[simplifiedPoints.Count - 1].y,
                    simplifiedPoints[0].x, simplifiedPoints[0].y));
            }
        }
        
        return simplifiedSegments;
    }

    // 构建连续的轮廓
    private List<List<Vector2>> BuildContours(List<Vector4> segments)
    {
        if (segments.Count == 0) return new List<List<Vector2>>();
        
        // 创建端点字典用于快速查找
        Dictionary<Vector2, List<Vector2>> connections = new Dictionary<Vector2, List<Vector2>>(new Vector2EqualityComparer());
        
        // 添加所有线段到连接字典
        foreach (var segment in segments)
        {
            Vector2 start = new Vector2(segment.x, segment.y);
            Vector2 end = new Vector2(segment.z, segment.w);
            
            if (!connections.ContainsKey(start))
                connections[start] = new List<Vector2>();
            if (!connections.ContainsKey(end))
                connections[end] = new List<Vector2>();
            
            // 避免重复添加相同的连接
            if (!connections[start].Any(p => Vector2.Distance(p, end) < 0.001f))
                connections[start].Add(end);
            if (!connections[end].Any(p => Vector2.Distance(p, start) < 0.001f))
                connections[end].Add(start);
        }
        
        // 查找并构建轮廓
        List<List<Vector2>> contours = new List<List<Vector2>>();
        
        // 使用边的访问状态而不是点的访问状态
        HashSet<string> visitedEdges = new HashSet<string>();
        
        // 首先处理度数为1的点（端点）或度数为2的点
        var startPoints = connections.Where(kvp => kvp.Value.Count <= 2)
                                  .Select(kvp => kvp.Key).ToList();
        
        // 如果没有度数<=2的点，则选择任意点开始
        if (startPoints.Count == 0)
            startPoints = connections.Keys.ToList();
        
        foreach (var startPoint in startPoints)
        {
            foreach (var initialNext in connections[startPoint])
            {
                string edgeKey = GetEdgeKey(startPoint, initialNext);
                if (visitedEdges.Contains(edgeKey)) continue;
                
                List<Vector2> currentContour = new List<Vector2>();
                currentContour.Add(startPoint);
                
                Vector2 current = startPoint;
                Vector2 next = initialNext;
                
                while (true)
                {
                    // 标记当前边为已访问
                    visitedEdges.Add(GetEdgeKey(current, next));
                    
                    current = next;
                    currentContour.Add(current);
                    
                    // 找到下一个未访问的边
                    bool foundNextEdge = false;
                    foreach (var neighbor in connections[current])
                    {
                        string nextEdgeKey = GetEdgeKey(current, neighbor);
                        if (!visitedEdges.Contains(nextEdgeKey))
                        {
                            next = neighbor;
                            foundNextEdge = true;
                            break;
                        }
                    }
                    
                    if (!foundNextEdge || next.Equals(startPoint))
                        break;
                }
                
                if (currentContour.Count > 2)
                {
                    contours.Add(currentContour);
                }
            }
        }
        
        return contours;
    }

    // 创建边的唯一标识符
    private string GetEdgeKey(Vector2 a, Vector2 b)
    {
        // 确保边的方向一致性（小坐标点在前）
        if (a.x < b.x || (a.x == b.x && a.y < b.y))
            return $"{a.x:F3},{a.y:F3}_{b.x:F3},{b.y:F3}";
        else
            return $"{b.x:F3},{b.y:F3}_{a.x:F3},{a.y:F3}";
    }

    // Vector2比较器
    private class Vector2EqualityComparer : IEqualityComparer<Vector2>
    {
        private const float Epsilon = 0.001f;
        
        public bool Equals(Vector2 a, Vector2 b)
        {
            return Vector2.Distance(a, b) < Epsilon;
        }
        
        public int GetHashCode(Vector2 v)
        {
            return Mathf.RoundToInt(v.x * 100) ^ Mathf.RoundToInt(v.y * 100);
        }
    }
    
    // 计算给定光源列表的光照比例
    public float CalculateLightingRatio(List<Lighting> lightSources)
    {
        // 跳过障碍物光源
        List<Lighting> nonObstacleLights = lightSources.Where(l => !l.isObstacle).ToList();
        
        if (nonObstacleLights.Count == 0)
        {
            return 0f;
        }
        
        // 创建哈希集合存储叶子节点，防止重复
        HashSet<QuadTreeNode> leafNodesSet = new HashSet<QuadTreeNode>();
        
        // 对每个光源获取其范围内的叶子节点并添加到集合中
        foreach (var light in nonObstacleLights)
        {
            Bounds lightBounds = light.GetWorldBounds();
            float rotation = light.rotation;
            // 使用考虑旋转的方法获取叶子节点
            var nodesInRange = GetNeighborLeafNodesWithRotation(lightBounds, rotation);
            foreach (var node in nodesInRange)
            {
                leafNodesSet.Add(node);
            }
        }
        
        // 将集合转换为列表
        List<QuadTreeNode> allNodes = leafNodesSet.ToList();
        if (allNodes.Count == 0)
        {
            return 0f;
        }
        
        // 创建实际接收光照的叶子节点集合
        HashSet<QuadTreeNode> illuminatedNodes = new HashSet<QuadTreeNode>();
        float totalBrightness = 0f;
        
        // 处理每个叶子节点
        foreach (var node in allNodes)
        {
            // 检查节点中心点是否被任何光源照亮
            Vector3 nodeCenter = new Vector3(node.Center.x, 0, node.Center.y);
            bool isInLightEffectiveRange = false;
            
            foreach (var light in nonObstacleLights)
            {
                if (light.heightMap == null) continue;
                
                // 检查节点是否在光源范围内
                if (IsNodeInLightRange(node, light))
                {
                    // 计算节点在光源中的UV坐标
                    Vector2 uv = CalculateUVForNodeInLight(node, light);
                    
                    // 从高度图采样原始值
                    float rawBrightness = light.heightMap.GetPixelBilinear(uv.x, uv.y).r;
                    
                    // 添加容差处理
                    rawBrightness = Mathf.Clamp01(rawBrightness);
                    if (rawBrightness > 0.01f)
                    {
                        isInLightEffectiveRange = true;

                    }
                }
            }
            
            // 如果节点在有效光照范围内，添加到集合并累加亮度
            if (isInLightEffectiveRange)
            {
                illuminatedNodes.Add(node);
                totalBrightness += Mathf.Min(node.Brightness, 1f); // 限制每个节点的亮度最大为1
            }
        }
        
        // 计算光照比例：亮度值/实际接收光照的叶子节点数
        if (allNodes.Count > 0)
        {
            return Mathf.Clamp01(totalBrightness / illuminatedNodes.Count);
        }
        
        return 0f;
    }

    // 检查节点是否在光源范围内（考虑旋转）
    private bool IsNodeInLightRange(QuadTreeNode node, Lighting light)
    {
        Vector2 nodeCenter = node.Center;     
        // 检查节点中心点是否在旋转后的光源范围内
        return light.IsPointInRotatedBounds(new Vector3(nodeCenter.x, 0, nodeCenter.y));
    }

    // 计算节点在光源中的UV坐标
    private Vector2 CalculateUVForNodeInLight(QuadTreeNode node, Lighting light)
    {
        Vector2 nodePos = node.Center;
        Bounds lightBounds = light.GetWorldBounds();
        Vector2 lightCenter = new Vector2(lightBounds.center.x, lightBounds.center.z);
        float rotation = light.rotation;
        
        // 计算相对于光源中心的位置
        Vector2 localPos = nodePos - lightCenter;
        
        // 应用反向旋转变换
        float rotationRad = -rotation * Mathf.Deg2Rad;
        Vector2 rotatedPos = new Vector2(
            localPos.x * Mathf.Cos(rotationRad) - localPos.y * Mathf.Sin(rotationRad),
            localPos.x * Mathf.Sin(rotationRad) + localPos.y * Mathf.Cos(rotationRad)
        );
        
        // 计算UV坐标
        Vector2 uv = new Vector2(
            (rotatedPos.x / (lightBounds.size.x * 0.5f)) * 0.5f + 0.5f,
            (rotatedPos.y / (lightBounds.size.z * 0.5f)) * 0.5f + 0.5f
        );
        
        // 边界约束确保UV在0-1范围内
        return new Vector2(Mathf.Clamp01(uv.x), Mathf.Clamp01(uv.y));
    }

    // 获取火光在指定位置的高度值
    public float GetFireLightHeightAtPosition(Vector3 position, List<Lighting> fireLights)
    {
        // 如果没有火光源，返回0
        if (fireLights == null || fireLights.Count == 0)
        {
            return 0f;
        }
        
        // 找到对应的叶子节点
        QuadTreeNode node = FindLeafNode(position);
        if (node == null)
        {
            return 0f;
        }
        
        // 查找最大高度值
        float maxHeight = 0f;
        
        foreach (var light in fireLights)
        {
            if (light.heightMap == null) continue;
            
            // 检查节点是否在光源范围内
            if (IsNodeInLightRange(node, light))
            {
                // 计算节点在光源中的UV坐标
                Vector2 uv = CalculateUVForNodeInLight(node, light);
                
                // 从高度图采样原始值
                float rawBrightness = light.heightMap.GetPixelBilinear(uv.x, uv.y).r;
                
                // 规范化亮度值
                rawBrightness = Mathf.Clamp01(rawBrightness);
                
                // 保留最大高度值
                if (rawBrightness > maxHeight)
                {
                    maxHeight = rawBrightness;
                }
            }
        }
        
        return maxHeight;
    }

    // 添加获取性能数据的方法
    public string GetPathfindingDebugInfo()
    {
        if (LastPathfindingInfo == null)
            return "尚无寻路数据";
            
        return $"寻路总耗时: {LastPathfindingInfo.TotalTime}ms\n" +
               $"节点查找: {LastPathfindingInfo.NodeFindingTime}ms\n" +
               $"寻路计算: {LastPathfindingInfo.PathfindingTime}ms\n" +
               $"路径简化: {LastPathfindingInfo.PathSimplificationTime}ms\n" +
               $"评估节点数: {LastPathfindingInfo.NodesEvaluated}\n" +
               $"最大开放列表: {LastPathfindingInfo.OpenListMaxCount}\n" +
               $"最大关闭列表: {LastPathfindingInfo.ClosedSetMaxCount}\n" +
               $"邻居评估次数: {LastPathfindingInfo.NeighborEvaluations}\n" +
               $"视线检查次数: {LastPathfindingInfo.LineOfSightChecks}\n" +
               $"原始路径长度: {LastPathfindingInfo.OriginalPathLength}\n" +
               $"最终路径长度: {LastPathfindingInfo.FinalPathLength}";
    }

    // 在 QuadTree 类中添加优先队列辅助类
    private class PriorityQueue<T>
    {
        private List<T> data;
        private readonly IComparer<T> comparer;

        public PriorityQueue(IComparer<T> comparer)
        {
            this.data = new List<T>();
            this.comparer = comparer;
        }

        public int Count => data.Count;

        public void Enqueue(T item)
        {
            data.Add(item);
            int childIndex = data.Count - 1;
            while (childIndex > 0)
            {
                int parentIndex = (childIndex - 1) / 2;
                if (comparer.Compare(data[childIndex], data[parentIndex]) >= 0)
                    break;
                T tmp = data[childIndex];
                data[childIndex] = data[parentIndex];
                data[parentIndex] = tmp;
                childIndex = parentIndex;
            }
        }

        public T Dequeue()
        {
            T frontItem = data[0];
            int lastIndex = data.Count - 1;
            data[0] = data[lastIndex];
            data.RemoveAt(lastIndex);

            if (lastIndex > 0)
            {
                int parentIndex = 0;
                while (true)
                {
                    int leftChildIndex = parentIndex * 2 + 1;
                    if (leftChildIndex >= data.Count)
                        break;

                    int rightChildIndex = leftChildIndex + 1;
                    int bestChildIndex = (rightChildIndex < data.Count && comparer.Compare(data[rightChildIndex], data[leftChildIndex]) < 0) ? 
                        rightChildIndex : leftChildIndex;

                    if (comparer.Compare(data[parentIndex], data[bestChildIndex]) <= 0)
                        break;

                    T tmp = data[parentIndex];
                    data[parentIndex] = data[bestChildIndex];
                    data[bestChildIndex] = tmp;
                    parentIndex = bestChildIndex;
                }
            }
            return frontItem;
        }

        public bool Contains(T item)
        {
            return data.Contains(item);
        }

        public void Clear()
        {
            data.Clear();
        }
    }
}

