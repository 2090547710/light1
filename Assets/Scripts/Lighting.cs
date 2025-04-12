using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

#region 数据结构定义
// 高度图数据结构
[System.Serializable]
public struct AreaMapData
{
    public Texture2D heightMap;
    public float rotation; // 新增旋转属性，以度为单位

    public AreaMapData(Texture2D map = null, float rotation = 0f)
    {
        heightMap = map;
        this.rotation = rotation;
    }
}

[System.Serializable]
public struct LightingData
{
    [Range(0, 128)] public float size;
    public bool isObstacle;
    public bool isSeed;
    [Range(0, 1)] public float lightHeight;
    public Texture2D heightMap;
    public float rotation; // 替换tiling和offset为rotation

    public LightingData(float size = 0, bool isObstacle = false, bool isSeed = false, float lightHeight = 0.5f, 
                       Texture2D heightMap = null, float rotation = 0f)
    {
        this.size = Mathf.Clamp(size, 0, 100);
        this.isObstacle = isObstacle;
        this.isSeed = isSeed;
        this.lightHeight = Mathf.Clamp01(lightHeight);
        this.heightMap = heightMap;
        this.rotation = rotation;
    }
}
#endregion

public class Lighting : MonoBehaviour
{
    #region 字段和属性
    [Header("区域设置")]
    [Range(0,100)]public float size;
    public bool isObstacle;
    public bool isSeed;
    public Texture2D heightMap;
    public float rotation; // 替换tiling和offset为rotation
    [Range(0, 1)] public float lightHeight;

    [Header("节点影响")]
    public float TotalBrightnessImpact;

    // 添加缓存字段
    [Header("调试信息")]
    [SerializeField] private float cachedSize;
    [SerializeField] private bool cachedIsObstacle;
    [SerializeField] private bool cachedIsSeed;
    [SerializeField] private Texture2D cachedHeightMap;
    [SerializeField] private float cachedRotation; // 替换cachedTiling和cachedOffset
    [SerializeField] private float cachedLightHeight;

    // 添加脏标记系统
    [SerializeField] private bool isDirty = true; // 默认为脏，确保首次应用
    public bool IsDirty => isDirty;

    // 新增哈希表记录重叠的光源
    [Header("重叠光源信息")]
    [SerializeField] private Dictionary<int, Lighting> overlappingLights = new Dictionary<int, Lighting>();
    
    // 新增公共属性用于获取重叠光源
    public IReadOnlyDictionary<int, Lighting> OverlappingLights => overlappingLights;
    #endregion

    #region Unity生命周期方法
    private void OnEnable() 
    {
        LightingManager.RegisterLight(this);
    }
    
    private void OnDisable() 
    {
        LightingManager.UnregisterLight(this);
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // 根据光源类型设置不同颜色
        if (isObstacle)
            Gizmos.color = Color.red;
        else if (isSeed)
            Gizmos.color = Color.green;
        else
            Gizmos.color = Color.yellow;
        
        // 保存当前矩阵
        Matrix4x4 originalMatrix = Gizmos.matrix;
        
        // 创建旋转矩阵
        Vector3 position = transform.position;
        Quaternion rotationQuat = Quaternion.Euler(0, rotation, 0);
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(
            position,
            rotationQuat,
            Vector3.one
        );
        
        // 应用旋转矩阵
        Gizmos.matrix = rotationMatrix;
        
        // 绘制旋转后的线框立方体，注意中心点需要是本地坐标原点
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size, 0, size));
        
        // 恢复原始矩阵
        Gizmos.matrix = originalMatrix;
        
        // 可选：绘制重叠光源关系线
        if (overlappingLights != null && overlappingLights.Count > 0)
        {
            Gizmos.color = Color.magenta;
            foreach (var light in overlappingLights.Values)
            {
                if (light != null)
                {
                    Gizmos.DrawLine(transform.position, light.transform.position);
                }
            }
        }
    }

    public void OnValidate()
    {
        // 确保isSeed和isObstacle不能同时为true
        if(isSeed && isObstacle)
        {
            isObstacle = false;
        }
        
        // 检查每个参数是否发生变化
        if (cachedSize != size || 
            cachedIsObstacle != isObstacle ||
            cachedIsSeed != isSeed ||
            cachedHeightMap != heightMap ||
            cachedRotation != rotation ||  // 替换tiling和offset检查
            cachedLightHeight != lightHeight)
        {
           MarkDirty(); // 设置为脏
        }

        if(TotalBrightnessImpact > 0.01f)
        {
            ValidateHeightmap();
        }

        // 如果有参数变化且应用在编辑器运行时
        if (isDirty && Application.isPlaying)
        {
            LightingManager.UpdateDirtyLights(); // 使用新方法更新脏光源
        }

        // 更新缓存值
        cachedSize = size;
        cachedIsObstacle = isObstacle;
        cachedIsSeed = isSeed;
        cachedHeightMap = heightMap;
        cachedRotation = rotation;  // 替换tiling和offset更新
        cachedLightHeight = lightHeight;
    }
    #endif
    #endregion

    #region 光照操作方法
    //光照标记
    public int ApplyLighting(bool isAdditive = true, bool useCachedData = false)
    { 
        if (useCachedData)
        {
            // 使用缓存数据
            TotalBrightnessImpact = LightingManager.tree.MarkIlluminatedArea(this, isAdditive, true);
        }
        else
        {
            // 使用当前数据
            TotalBrightnessImpact = LightingManager.tree.MarkIlluminatedArea(this, isAdditive, false);
        }
        
        return Mathf.RoundToInt(TotalBrightnessImpact * 100f);
    }

    public void RemoveLighting()
    {
        // 通过设置光源大小为0，来移除光源
        size = 0;
        MarkDirty();
        LightingManager.UpdateDirtyLights();
        //从activeLights中移除
        LightingManager.UnregisterLight(this);
    }
    
    // 添加重置脏标记方法
    public void ResetDirtyFlag()
    {
        isDirty = false;
    }

    // 强制设置脏标记
    public void MarkDirty()
    {
        isDirty = true;
    }
    #endregion

    #region 重叠光源管理
    // 更新当前光源与其他光源的重叠关系
    public void UpdateOverlappingLights(bool useCachedData = false)
    {
        // 先清除现有关系
        ClearOverlappingRelationships();
        
        // 获取当前光源的影响范围（根据是否使用缓存数据）
        Bounds myBounds = useCachedData ? GetCachedWorldBounds() : GetWorldBounds();
        
        // 检查与所有其他活跃光源的重叠
        
        foreach (var otherLight in LightingManager.activeLights)
        {
            // 跳过自身和障碍物光源
            if (otherLight == this || otherLight.isObstacle)
                continue;
                
            // 获取其他光源的影响范围
            Bounds otherBounds = otherLight.GetWorldBounds();
            // 判断两个矩形在xz平面是否重叠
            if (IsOverlappingOnXZPlane(myBounds, otherBounds))
            {
                // 添加到重叠光源表中
                overlappingLights.Add(otherLight.GetInstanceID(), otherLight);
                
                // 同时更新对方的重叠光源表（如果当前光源不是障碍物）
                if (!isObstacle && !otherLight.overlappingLights.ContainsKey(this.GetInstanceID()))
                {
                    otherLight.overlappingLights.Add(this.GetInstanceID(), this);
                }
            }
        }
    }

    // 清除与其他光源的重叠关系
    private void ClearOverlappingRelationships()
    {
        // 从其他光源的重叠列表中移除自己
        foreach (var otherLight in overlappingLights.Values)
        {
            if (otherLight != null)
            {
                otherLight.overlappingLights.Remove(this.GetInstanceID());
            }
        }
        
        // 清空自己的重叠列表
        overlappingLights.Clear();
    }
    
    // 修改IsOverlappingOnXZPlane方法，支持旋转碰撞检测
    private bool IsOverlappingOnXZPlane(Bounds a, Bounds b)
    {
        // 获取旋转角度（弧度）
        float rotationRadA = this.rotation * Mathf.Deg2Rad;
        
        // 假设b是另一个Lighting组件的边界
        float rotationRadB = 0f;
        // 尝试获取另一个光源的旋转角度
        foreach (var light in LightingManager.activeLights)
        {
            if (light.GetWorldBounds() == b)
            {
                rotationRadB = light.rotation * Mathf.Deg2Rad;
                break;
            }
        }
        
        // 通过分离轴定理检测旋转矩形碰撞
        return AreRotatedRectsOverlapping(
            new Vector2(a.center.x, a.center.z), new Vector2(a.size.x, a.size.z), rotationRadA,
            new Vector2(b.center.x, b.center.z), new Vector2(b.size.x, b.size.z), rotationRadB
        );
    }

    // 新增用于检测两个旋转矩形碰撞的辅助方法（使用分离轴定理）
    private bool AreRotatedRectsOverlapping(Vector2 centerA, Vector2 sizeA, float rotationA, 
                                            Vector2 centerB, Vector2 sizeB, float rotationB)
    {
        // 计算两个矩形的四个顶点
        Vector2[] cornersA = GetRotatedRectCorners(centerA, sizeA, rotationA);
        Vector2[] cornersB = GetRotatedRectCorners(centerB, sizeB, rotationB);
        
        // 分离轴定理检测
        // 检查A的两个轴
        Vector2 axisA1 = (cornersA[1] - cornersA[0]).normalized;
        Vector2 axisA2 = (cornersA[3] - cornersA[0]).normalized;
        
        if (!OverlapOnAxis(cornersA, cornersB, axisA1)) return false;
        if (!OverlapOnAxis(cornersA, cornersB, axisA2)) return false;
        
        // 检查B的两个轴
        Vector2 axisB1 = (cornersB[1] - cornersB[0]).normalized;
        Vector2 axisB2 = (cornersB[3] - cornersB[0]).normalized;
        
        if (!OverlapOnAxis(cornersA, cornersB, axisB1)) return false;
        if (!OverlapOnAxis(cornersA, cornersB, axisB2)) return false;
        
        // 所有轴都有重叠，表示矩形相交
        return true;
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
    #endregion

    #region 光照数据获取与验证
    public AreaMapData GetAreaMapData()
    {
        return new AreaMapData(
            heightMap,
            rotation
        );
    }

    public Bounds GetWorldBounds()
    {
        Vector3 center = transform.position;
        Vector3 size = new Vector3(this.size, lightHeight, this.size);
        return new Bounds(center, size);
    }

    public bool ValidateHeightmap()
    {
        if (heightMap == null)
        {
            Debug.LogWarning($"光源 {name} 未分配高度图", this);
            return false;
        }

        bool isValid = true;
        return isValid;
    }
    #endregion

    #region 缓存数据访问
    // 新增获取缓存的heightMap方法
    public Texture2D GetCachedHeightMap()
    {
        return cachedHeightMap;
    }
    
    // 新增获取缓存的WorldBounds方法
    public Bounds GetCachedWorldBounds()
    {
        Vector3 center = transform.position;
        Vector3 size = new Vector3(cachedSize, cachedLightHeight, cachedSize);
        return new Bounds(center, size);
    }
    
    // 新增获取缓存的AreaMapData方法
    public AreaMapData GetCachedAreaMapData()
    {
        return new AreaMapData(
            cachedHeightMap,
            cachedRotation
        );
    }
    
    // 新增获取缓存的isObstacle方法
    public bool GetCachedIsObstacle()
    {
        return cachedIsObstacle;
    }
    
    // 新增获取缓存的lightHeight方法
    public float GetCachedLightHeight()
    {
        return cachedLightHeight;
    }

    // 新增获取缓存的isSeed方法
    public bool GetCachedIsSeed()
    {
        return cachedIsSeed;
    }
    
    // 新增获取缓存的rotation方法
    public float GetCachedRotation()
    {
        return cachedRotation;
    }
    #endregion

    #region 光照组件初始化方法
    //通过LightingData初始化光照组件
    public void InitializeFromData(LightingData data)
    {
        // 设置主要字段
        size = data.size;
        isObstacle = data.isObstacle;
        isSeed = data.isSeed;
        lightHeight = data.lightHeight;
        heightMap = data.heightMap;
        rotation = data.rotation;  // 替换tiling和offset赋值
        
        // 同时初始化缓存字段
        cachedSize = data.size;
        cachedIsObstacle = data.isObstacle;
        cachedIsSeed = data.isSeed;
        cachedLightHeight = data.lightHeight;
        cachedHeightMap = data.heightMap;
        cachedRotation = data.rotation;  // 替换cachedTiling和cachedOffset赋值
        
        // 标记为脏，确保应用更改
        MarkDirty();
        LightingManager.UpdateDirtyLights();
    }
    #endregion

    // 新增检查点是否在旋转矩形内的方法
    public bool IsPointInRotatedBounds(Vector3 point, bool useCachedData = false)
    {
        Vector2 center;
        Vector2 size;
        float rot;
        
        if (useCachedData)
        {
            center = new Vector2(transform.position.x, transform.position.z);
            size = new Vector2(cachedSize, cachedSize);
            rot = cachedRotation * Mathf.Deg2Rad;
        }
        else
        {
            center = new Vector2(transform.position.x, transform.position.z);
            size = new Vector2(this.size, this.size);
            rot = rotation * Mathf.Deg2Rad;
        }
        
        Vector2 pointXZ = new Vector2(point.x, point.z);
        Vector2 localPoint = RotatePoint(pointXZ - center, -rot) + center;
        
        float halfWidth = size.x * 0.5f;
        float halfHeight = size.y * 0.5f;
        
        return localPoint.x >= center.x - halfWidth &&
               localPoint.x <= center.x + halfWidth &&
               localPoint.y >= center.y - halfHeight &&
               localPoint.y <= center.y + halfHeight;
    }

    // 旋转一个点
    private Vector2 RotatePoint(Vector2 point, float angle)
    {
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        return new Vector2(
            point.x * cos - point.y * sin,
            point.x * sin + point.y * cos
        );
    }
}

