using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

public class QuadTree
{
    ////////////////////////////////////////////////////////////////
    // 四叉树节点类
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
        public bool IsWalkable => IsIlluminated; // 复用光照状态

        public QuadTreeNode(Vector2 center, Vector2 size, int capacity)
        {
            Center = center;
            Size = size;
            Capacity = capacity;
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
       
    }

    ////////////////////////////////////////////////////////////////

    // 根节点和最大深度
    private QuadTreeNode root;
    private int maxDepth;

    // 新增根节点尺寸访问属性
    public Vector2 RootSize { get; private set; }
    public int MaxDepth { get; private set; }

    public QuadTree(Vector2 center, Vector2 size, int capacity, int maxDepth = 5, bool preSplit = false)
    {
        RootSize = size; // 记录根节点尺寸
        MaxDepth = maxDepth; // 记录最大深度
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

    // 插入对象到四叉树
    public bool Insert(GameObject obj, int currentDepth = 0)
    {
        Vector3 objPosition = obj.transform.position;
        Vector2 position = new Vector2(objPosition.x, objPosition.z);
        return InsertRecursive(root, position, obj, currentDepth);
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
         // 半透明填充（alpha值0.2）
        Gizmos.color = node.IsIlluminated ? new Color(1, 0.9f, 0.5f, 1.0f) : new Color(0,0,0,0.1f);
        Gizmos.DrawWireCube(center, size * 1.0f);

        // 递归绘制子节点
        if (node.Children != null)
        {
            foreach (var child in node.Children)
            {
                DrawNodeGizmos(child, depth + 1);
            }
        }
    }

    // 新增移除方法
    public bool Remove(GameObject obj)
    {
        Vector3 pos = obj.transform.position;
        Vector2 position = new Vector2(pos.x, pos.z);
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

    // 修改后的光照标记方法（返回影响节点数）
    public int MarkIlluminatedArea(Vector2 center, float radius)
    {
        bool isSubtractive = radius < 0;
        bool isSquare = isSubtractive;
        return MarkIlluminatedRecursive(root, center, Mathf.Abs(radius), isSubtractive, isSquare, 0);
    }

    private int MarkIlluminatedRecursive(QuadTreeNode node, Vector2 center, float size, bool isSubtractive, bool isSquare, int currentDepth = 0)
    {
        Rect nodeRect = new Rect(
            node.Center.x - node.Size.x/2,
            node.Center.y - node.Size.y/2,
            node.Size.x,
            node.Size.y);
        
        // 根据检测类型选择碰撞判断方法
        bool overlap = isSquare ? 
            SquareRectOverlap(center, size, nodeRect) : 
            CircleRectOverlap(center, size, nodeRect);
        
        if (!overlap) return 0;

        int count = 0;

        // 强制分裂直到达到最大深度
        if (currentDepth < maxDepth)
        {
            // 如果还没有子节点则分裂
            if (node.Children == null)
            {
                node.Split();
                RedistributeObjects(node);
            }
            
            // 继续递归子节点
            foreach (var child in node.Children)
            {
                count += MarkIlluminatedRecursive(child, center, size, isSubtractive, isSquare, currentDepth + 1);
            }
        }
        else
        {
            // 仅当状态改变时计数
            bool newState = isSubtractive ? false : true;
            if(node.IsIlluminated != newState)
            {
                node.IsIlluminated = newState;
                count = 1;
            }
        }
        return count;
    }

    // 圆形与矩形碰撞检测
    private bool CircleRectOverlap(Vector2 circlePos, float radius, Rect rect)
    {
        // 先进行快速排除
        float dx = Mathf.Abs(circlePos.x - rect.center.x);
        float dy = Mathf.Abs(circlePos.y - rect.center.y);

        if (dx > (rect.width/2 + radius)) return false;
        if (dy > (rect.height/2 + radius)) return false;

        if (dx <= (rect.width/2)) return true;
        if (dy <= (rect.height/2)) return true;

        float cornerDistSq = Mathf.Pow(dx - rect.width/2, 2) +
                           Mathf.Pow(dy - rect.height/2, 2);

        return cornerDistSq <= (radius * radius);
    }

    // 新增正方形与矩形碰撞检测
    private bool SquareRectOverlap(Vector2 squareCenter, float squareSize, Rect rect)
    {
        // 计算正方形边界（边长为两倍squareSize）
        float halfSize = squareSize; // 因为总边长是2*squareSize，半长就是squareSize
        Rect squareRect = new Rect(
            squareCenter.x - halfSize,
            squareCenter.y - halfSize,
            squareSize * 2,  // 实际边长为两倍传入值
            squareSize * 2);
        
        // 矩形相交检测
        return rect.Overlaps(squareRect);
    }

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

    // 新增路径规划方法
    public List<Vector3> FindPath(Vector3 startPos, Vector3 targetPos, float searchRadius = 5f)
    {
        var startNode = FindLeafNode(startPos);
        var targetNode = FindLeafNode(targetPos);
        
        // 如果目标节点不可行走，直接返回null
        if (targetNode == null || !targetNode.IsWalkable)
        {
            // 在目标位置周围寻找最近的可行走节点
            targetNode = FindNearestWalkableNode(targetPos, searchRadius);
            if (targetNode == null) return null;
        }
        
        var openList = new List<QuadTreeNode>();
        var closedSet = new HashSet<QuadTreeNode>();

        // 初始化节点数据
        ResetPathfindingData();
        
        startNode.GCost = 0;
        startNode.HCost = Heuristic(startNode, targetNode);
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

                float tentativeGCost = currentNode.GCost + Heuristic(currentNode, neighbor);
                
                // Theta*核心优化
                if (currentNode.ParentNode != null && 
                    HasLineOfSight(currentNode.ParentNode, neighbor))
                {
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
        Vector3 center = new Vector3(node.Center.x, 0, node.Center.y);
        float radius = Mathf.Max(node.Size.x, node.Size.y) * 1.5f;
        return GetNeighborLeafNodes(center, radius)
            .Where(n => n.IsWalkable).ToList();
    }

    private bool HasLineOfSight(QuadTreeNode from, QuadTreeNode to)
    {
        Vector2 start = from.Center;
        Vector2 end = to.Center;
        float step = Mathf.Min(from.Size.x, from.Size.y) * 0.5f;
        float distance = Vector2.Distance(start, end);
        
        for (float t = 0; t <= 1; t += step / distance)
        {
            Vector2 point = Vector2.Lerp(start, end, t);
            var node = FindLeafNode(new Vector3(point.x, 0, point.y));
            if (node == null || !node.IsWalkable) return false;
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

    private float Heuristic(QuadTreeNode a, QuadTreeNode b)
    {
        return Vector2.Distance(a.Center, b.Center);
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
}

