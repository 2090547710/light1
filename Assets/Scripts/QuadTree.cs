using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

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
        public bool IsIlluminated = false;

        // 添加父节点引用
        [System.NonSerialized]
        public QuadTreeNode Parent;

        // 新增路径规划属性
        public float GCost = Mathf.Infinity;
        public float HCost;
        public float FCost => GCost + HCost;
        public QuadTreeNode ParentNode;
        public bool IsWalkable => AreaType != AreaType.Dark;

        // 新增高度属性
        public float Height { get; private set; }
        public AreaType AreaType => GetAreaType(Height);

        public QuadTreeNode(Vector2 center, Vector2 size, int capacity)
        {
            Center = center;
            Size = size;
            Capacity = capacity;
            Height = 0.01f;
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
       
        // 修改后的高度更新方法
        public void UpdateHeight(float newHeight)
        {
            var newType = GetAreaType(newHeight);
            var currentTypePriority = (int)AreaType;
            var newTypePriority = (int)newType;

           
            if ( AreaType == AreaType.Obstacle && newType != AreaType.Obstacle)
            {
                // 障碍物会被其他类型累加高度
                Height += newHeight;
                return;
            }
                 // 优先级判断规则：Obstacle>Light>seed>Dark
            if (newTypePriority > currentTypePriority || 
               (newType == AreaType))
            {
                Height = newHeight;
            }
        }

        // 根据高度值获取区域类型
        private AreaType GetAreaType(float height)
        {
            if (height >= 0.11f && height <= 1f) 
                return AreaType.Obstacle;
            if (height >= -1f && height <= -0.01f) 
                return AreaType.Light;
            if (height >= 0.01f && height <= 0.1f) 
                return AreaType.Dark;
            if (Mathf.Approximately(height, 0f)) 
                return AreaType.Seed;
            
            return AreaType.Dark; // 默认处理
        }

        // 新增设置方法
        public void SetHeight(float newHeight)
        {
            Height = newHeight;
        }

    }
    #endregion

    #region 四叉树类相关
    // 根节点和最大深度
    private QuadTreeNode root;
    private int maxDepth;

    // 新增根节点尺寸访问属性
    public Vector2 RootSize { get; private set; }
    public int MaxDepth { get; private set; }

    // 添加最小节点尺寸属性
    public Vector2 MinNodeSize { get; private set; }

    // 修改GetNeighbors方法，添加缓存机制
    private Dictionary<QuadTreeNode, List<QuadTreeNode>> neighborCache = new Dictionary<QuadTreeNode, List<QuadTreeNode>>();

    public QuadTree(Vector2 center, Vector2 size, int capacity, int maxDepth = 5, bool preSplit = false)
    {
        RootSize = size;
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
    public bool Insert(GameObject obj, int currentDepth = 0)
    {
        // 计算理论最终节点中心
        Vector2 targetCenter = CalculateFinalNodeCenter(obj.transform.position);
        
        // 调整对象坐标到节点中心
        Vector3 newPos = new Vector3(
            targetCenter.x, 
            obj.transform.position.y, 
            targetCenter.y
        );
        obj.transform.position = newPos;

        // 使用调整后的坐标进行插入
        neighborCache.Clear();
        return InsertRecursive(root, targetCenter, obj, currentDepth);
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
        List<GameObject> objectsToRedistribute = new List<GameObject>(node.Objects);
        node.Objects.Clear();

        foreach (var obj in objectsToRedistribute)
        {
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
        if (node.Height > 0 && node.Size==MinNodeSize)
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
    // 修改后的光照标记方法 先预分裂，
    public int MarkIlluminatedArea(Bounds area, bool isRect, bool isDark, float lightHeight)
    {
        PreSplitForLighting(root, area, isRect, 0);
        return FinalizeIlluminationMarking(area, isRect, isDark, lightHeight);
    }

    // 修改后的预分裂方法（移除高度更新）
    private void PreSplitForLighting(QuadTreeNode node, Bounds area, bool isRect, int currentDepth)
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

        if (currentDepth < maxDepth)
        {
            if (node.Children == null)
            {
                node.Split();
                RedistributeObjects(node);
            }
            
            foreach (var child in node.Children)
            {
                PreSplitForLighting(child, area, isRect, currentDepth + 1);
            }
        }
    }

    // 修改获取区域对象方法
    private List<GameObject> GetAllObjectsInArea(Bounds area, bool isRect)
    {
        List<GameObject> results = new List<GameObject>();
        QueryAreaRecursive(root, area, isRect, ref results);
        return results;
    }

    // 修改查询方法
    private void QueryAreaRecursive(QuadTreeNode node, Bounds area, bool isRect, ref List<GameObject> results)
    {
        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x/2,
            node.Center.y - node.Size.y/2,
            node.Size.x,
            node.Size.y);

        Vector2 center = new Vector2(area.center.x, area.center.z);
        bool overlap = isRect ?
            RectangleRectOverlap(center, new Vector2(area.size.x, area.size.z), nodeRect) :
            CircleRectOverlap(center, area.size.x * 0.5f, nodeRect);

        if (!overlap) return;

        // 如果有子节点则递归查询
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                QueryAreaRecursive(child, area, isRect, ref results);
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

    // 修改后的最终标记方法（添加高度更新）
    private int FinalizeIlluminationMarking(Bounds area, bool isRect, bool isDark, float lightHeight)
    {
        int count = 0;
        FinalMarkRecursive(root, area, isRect, isDark, lightHeight, 0, ref count);
        return count;
    }

    private void FinalMarkRecursive(QuadTreeNode node, Bounds area, bool isRect, bool isDark, 
                                  float lightHeight, int depth, ref int count)
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

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                FinalMarkRecursive(child, area, isRect, isDark, lightHeight, depth + 1, ref count);
            }
        }
        else
        { 
            if (node.Height <= lightHeight)
            {
                // 添加高度更新逻辑
                node.UpdateHeight(area.size.y);
                
                bool originalState = node.IsIlluminated;
                node.IsIlluminated = !isDark;
                if (originalState != node.IsIlluminated) count++;
            }
        }
    }
    #endregion
   
    #region 光照移除方法
    public void RemoveIlluminationEffect(Bounds area, bool isRect, AreaType areaType)
    {
        if (areaType == AreaType.Obstacle) return;
        
        RemoveIlluminationRecursive(root, area, isRect);
    }
   
    private void RemoveIlluminationRecursive(QuadTreeNode node, Bounds area, bool isRect)
    {
        // 区域重叠检测（复用现有逻辑）
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

        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                RemoveIlluminationRecursive(child, area, isRect);
            }
        }
        else if (node.Size == MinNodeSize) // 仅处理最小节点
        {
            node.SetHeight(0.01f);
            node.IsIlluminated = false;
        }
    }
    #endregion
    
    #region 路径规划
    // 新增路径规划方法
    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos, float maxStepHeight = 0.2f)
    {
        float maxStep = maxStepHeight;
        
        var startNode = FindLeafNode(startPos);
        var targetNode = FindLeafNode(targetPos);
        
        // 直接返回null如果目标节点不可行走
        if (targetNode == null || !targetNode.IsWalkable || targetNode.Size != MinNodeSize)
        {
            return null;
        }
        
        var openList = new List<QuadTreeNode>();
        var closedSet = new HashSet<QuadTreeNode>();

        // 初始化节点数据
        ResetPathfindingData();
        
        startNode.GCost = 0;
        startNode.HCost = Heuristic(startNode, targetNode, maxStep);
        openList.Add(startNode);

        while (openList.Count > 0)
        {
            var currentNode = openList.OrderBy(n => n.FCost).First();
            
            if (currentNode == targetNode)
                return RetracePath(startNode, targetNode);

            openList.Remove(currentNode);
            closedSet.Add(currentNode);

            foreach (var neighbor in GetNeighbors(currentNode))
            {
                if (!neighbor.IsWalkable || closedSet.Contains(neighbor))
                    continue;

                float tentativeGCost = currentNode.GCost + Heuristic(currentNode, neighbor, maxStep);
                
                // Theta*核心优化
                if (currentNode.ParentNode != null && 
                    HasLineOfSight(currentNode.ParentNode, neighbor, maxStep))
                {
                    float alternativeCost = currentNode.ParentNode.GCost + 
                                          Heuristic(currentNode.ParentNode, neighbor, maxStep);
                    if (alternativeCost < tentativeGCost)
                    {
                        tentativeGCost = alternativeCost;
                        neighbor.ParentNode = currentNode.ParentNode;
                    }
                }

                if (tentativeGCost < neighbor.GCost)
                {
                    neighbor.GCost = tentativeGCost;
                    neighbor.HCost = Heuristic(neighbor, targetNode, maxStep);
                    neighbor.ParentNode = currentNode;

                    if (!openList.Contains(neighbor))
                        openList.Add(neighbor);
                }
            }
        }
        return null;
    }

    // 新增私有辅助方法
    private QuadTreeNode FindLeafNode(Vector3 position)
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

        Vector3 center = new Vector3(node.Center.x, 0, node.Center.y);
        float radius = Mathf.Max(node.Size.x, node.Size.y) * 1.5f;
        var neighbors = GetNeighborLeafNodes(center, radius)
            .Where(n => n.IsWalkable).ToList();
        
        neighborCache[node] = neighbors;
        return neighbors;
    }

    private bool HasLineOfSight(QuadTreeNode from, QuadTreeNode to, float maxStepHeight = 0.2f)
    {
        Vector2 start = from.Center;
        Vector2 end = to.Center;
        float step = Mathf.Min(from.Size.x, from.Size.y) * 0.3f;
        float distance = Vector2.Distance(start, end);
        
        QuadTreeNode prevNode = from;
        
        for (float t = 0; t <= 1; t += step / distance)
        {
            Vector2 point = Vector2.Lerp(start, end, t);
            var node = FindLeafNode(new Vector3(point.x, 0, point.y));
            if (node == null || !node.IsWalkable) return false;
            
            // 添加连续高度差检测
            if (Mathf.Abs(prevNode.Height - node.Height) > maxStepHeight)
                return false;
            
            prevNode = node;
        }
        return true;
    }

    private List<Vector3> RetracePath(QuadTreeNode startNode, QuadTreeNode endNode)
    {
        List<Vector3> path = new List<Vector3>();
        var currentNode = endNode;

        while (currentNode != null && currentNode != startNode)
        {
            path.Add(new Vector3(currentNode.Center.x, 0, currentNode.Center.y));
            currentNode = currentNode.ParentNode;
        }
        path.Reverse();
        return SimplifyPath(path);
    }

    private List<Vector3> SimplifyPath(List<Vector3> path)
    {
        if (path.Count < 3) return path;
        
        List<Vector3> simplified = new List<Vector3> { path[0] };
        for (int i = 1; i < path.Count - 1; i++)
        {
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
        
        for (float t = 0; t <= 1; t += step / distance)
        {
            Vector2 point = Vector2.Lerp(start, end, t);
            var node = FindLeafNode(new Vector3(point.x, 0, point.y));
            if (node == null || !node.IsWalkable) return false;
        }
        return true;
    }

    private float Heuristic(QuadTreeNode a, QuadTreeNode b, float maxStepHeight = 0.2f)
    {
        // 将负高度视为0
        float aHeight = Mathf.Max(a.Height, 0);
        float bHeight = Mathf.Max(b.Height, 0);
        
        float heightDifference = Mathf.Abs(aHeight - bHeight);
        if (heightDifference > maxStepHeight)
        {
            return Mathf.Infinity;
        }
        return Vector2.Distance(a.Center, b.Center) + heightDifference * 0.2f;
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

    private QuadTreeNode FindNearestWalkableNode(Vector3 position, float radius, float maxStepHeight = 0.5f)
    {
        // 获取当前位置所在节点的高度作为参考
        var referenceNode = FindLeafNode(position);
        float referenceHeight = referenceNode?.Height ?? 0;

        var candidates = GetNeighborLeafNodes(position, radius)
            .Where(n => n.IsWalkable && 
                   Mathf.Abs(n.Height - referenceHeight) <= maxStepHeight) // 添加高度差过滤
            .OrderBy(n => Vector3.Distance(
                new Vector3(n.Center.x, 0, n.Center.y), 
                position))
            .ThenBy(n => Mathf.Abs(n.Height - referenceHeight)); // 添加高度差排序

        return candidates.FirstOrDefault();
    }

    #endregion

    #region 其他辅助方法
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

    #region 碰撞检测
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

    // 矩形检测
    private bool RectangleRectOverlap(Vector2 rectCenter, Vector2 rectSize, Rect targetRect)
    {
        // 构造源矩形
        Rect sourceRect = new Rect(
            rectCenter.x - rectSize.x/2,
            rectCenter.y - rectSize.y/2,
            rectSize.x,
            rectSize.y);
        
        // 修改为严格重叠检测（排除边界接触）
        return sourceRect.xMin < targetRect.xMax && 
               sourceRect.xMax > targetRect.xMin && 
               sourceRect.yMin < targetRect.yMax && 
               sourceRect.yMax > targetRect.yMin;
    }

#endregion


    // 重置光照状态
    public void ResetIllumination()
    {
        ResetIlluminationRecursive(root);
    }

    private void ResetIlluminationRecursive(QuadTreeNode node)
    {
        node.IsIlluminated = false;
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

    // 获取指定位置周围邻近的叶子节点
    public List<QuadTreeNode> GetNeighborLeafNodes(Vector3 position, float radius)
    {
        Vector2 pos = new Vector2(position.x, position.z);
        List<QuadTreeNode> result = new List<QuadTreeNode>();
        FindNeighborLeafNodes(root, pos, radius, result);
        return result;
    }

    private void FindNeighborLeafNodes(QuadTreeNode node, Vector2 position, float radius, List<QuadTreeNode> result)
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
                FindNeighborLeafNodes(child, position, radius, result);
            }
        }
    }
    #endregion

  
}

