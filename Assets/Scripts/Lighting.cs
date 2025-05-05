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
    public Texture2D edgeHeightMap; // 新增边缘高度图属性
    [Range(0, 360)] public float rotation; 

    public LightingData(float size = 0, bool isObstacle = false, bool isSeed = false, float lightHeight = 0.5f, 
                       Texture2D heightMap = null, Texture2D edgeHeightMap = null, float rotation = 0f)
    {
        this.size = Mathf.Clamp(size, 0, 100);
        this.isObstacle = isObstacle;
        this.isSeed = isSeed;
        this.lightHeight = Mathf.Clamp01(lightHeight);
        this.heightMap = heightMap;
        this.edgeHeightMap = edgeHeightMap;
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
    public Texture2D edgeHeightMap; // 新增边缘高度图属性
    [Range(0, 360)] public float rotation;
    [Range(0, 1)] public float lightHeight;

    [Header("节点影响")]
    public float TotalBrightnessImpact;

    // 添加缓存字段
    [Header("调试信息")]
    [SerializeField] private float cachedSize;
    [SerializeField] private bool cachedIsObstacle;
    [SerializeField] private bool cachedIsSeed;
    [SerializeField] private Texture2D cachedHeightMap;
    [SerializeField] private Texture2D cachedEdgeHeightMap; // 新增边缘高度图缓存
    [SerializeField] private float cachedRotation;
    [SerializeField] private float cachedLightHeight;
    [SerializeField] private Vector3 cachedPosition; // 新增position缓存字段
    [SerializeField] private Quaternion cachedRotationQuaternion; // 缓存transform的旋转

    // 添加脏标记系统
    [SerializeField] private bool isDirty = true; // 默认为脏，确保首次应用
    public bool IsDirty => isDirty;

    // 新增哈希表记录重叠的光源
    [Header("重叠光源信息")]
    [SerializeField] private Dictionary<int, Lighting> overlappingLights = new Dictionary<int, Lighting>();
    
    // 新增公共属性用于获取重叠光源
    public IReadOnlyDictionary<int, Lighting> OverlappingLights => overlappingLights;

    [Header("边缘高度图Quad")]
    [SerializeField] private GameObject edgeQuad; // 存储创建的quad

    // 在 Lighting.cs 中添加一个私有标志
    private bool _pendingEdgeQuadUpdate = false;
    #endregion

    #region Unity生命周期方法
    private void OnEnable() 
    {
        LightingManager.RegisterLight(this);
        cachedRotationQuaternion = transform.rotation; // 初始化旋转缓存
    }
    
    private void OnDisable() 
    {
        LightingManager.UnregisterLight(this);
    }

    private void Update()
    {
        // 检查position是否发生变化
        if (transform.position != cachedPosition && Application.isPlaying)
        {
            MarkDirty();
            if(TotalBrightnessImpact > 0.01f)
            {
                ValidateHeightmap();
            }
            LightingManager.UpdateDirtyLights();
            // 更新位置缓存
            cachedPosition = transform.position;
        }
        
        // 检查rotation是否发生变化
        if (transform.rotation != cachedRotationQuaternion && Application.isPlaying)
        {
            // 计算Y轴旋转角度差并增加到rotation属性
            float currentYRotation = transform.eulerAngles.y;
            float previousYRotation = cachedRotationQuaternion.eulerAngles.y;
            float rotationDelta = Mathf.DeltaAngle(previousYRotation, currentYRotation);
            
            // 将角度差值累加到rotation属性
            rotation = (rotation -rotationDelta + 360) % 360;
            
            MarkDirty();
            if(TotalBrightnessImpact > 0.01f)
            {
                ValidateHeightmap();
            }
            LightingManager.UpdateDirtyLights();
            // 更新旋转缓存
            cachedRotation = rotation;
            cachedRotationQuaternion = transform.rotation;
        }

        // 如果edgeHeightMap变化，更新quad
        if (edgeHeightMap != cachedEdgeHeightMap)
        {
            if (edgeHeightMap != null)
            {
                CreateEdgeQuad();
            }
            else
            {
                DestroyEdgeQuad();
            }
            cachedEdgeHeightMap = edgeHeightMap;
        }
        
        // 如果有quad存在，同步transform
        if (edgeQuad != null)
        {
            // 更新旋转以匹配光源rotation
            edgeQuad.transform.localRotation = Quaternion.Euler(90, -rotation, 0);
            
            // 检查size是否变化
            if (edgeQuad.transform.childCount > 0)
            {
                edgeQuad.transform.GetChild(0).localScale = new Vector3(size, size, 1);
                
                cachedSize = size;
            }
        }
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
        Quaternion rotationQuat = Quaternion.Euler(0, -rotation, 0);
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
        LightingManager.isValidating = true;
        
        // 确保isSeed和isObstacle不能同时为true
        if(isSeed && isObstacle)
        {
            isObstacle = false;
        }
        
        // 检查每个参数是否发生变化，包括position和rotation
        if (cachedSize != size || 
            cachedIsObstacle != isObstacle ||
            cachedIsSeed != isSeed ||
            cachedHeightMap != heightMap ||
            cachedEdgeHeightMap != edgeHeightMap || // 新增边缘高度图检查
            cachedRotation != rotation ||
            cachedLightHeight != lightHeight ||
            cachedPosition != transform.position ||
            cachedRotationQuaternion != transform.rotation)
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

        // 更新缓存值，包括position和rotation
        cachedSize = size;
        cachedIsObstacle = isObstacle;
        cachedIsSeed = isSeed;
        cachedHeightMap = heightMap;
        
        // 修改：检查edgeHeightMap是否变化，但不直接调用CreateEdgeQuad或DestroyEdgeQuad
        if (cachedEdgeHeightMap != edgeHeightMap)
        {
            // 标记需要更新edgeQuad，但不立即执行
            _pendingEdgeQuadUpdate = true;
            
            // 如果在编辑器中运行，使用延迟调用
            if (Application.isPlaying)
            {
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () => 
                {
                    // 在isValidating = false 后执行
                    if (_pendingEdgeQuadUpdate)
                    {
                        if (edgeHeightMap != null)
                        {
                            CreateEdgeQuad();
                        }
                        else
                        {
                            DestroyEdgeQuad();
                        }
                        _pendingEdgeQuadUpdate = false;
                    }
                };
                #endif
            }
            
            cachedEdgeHeightMap = edgeHeightMap;
        }
        
        cachedRotation = rotation;
        cachedLightHeight = lightHeight;
        cachedPosition = transform.position;
        cachedRotationQuaternion = transform.rotation; // 新增rotation更新
        
        LightingManager.isValidating = false;
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
        // 销毁范围高度图
        DestroyEdgeQuad();
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
        Vector3 center = cachedPosition; // 使用缓存的position
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

    // 新增获取缓存的position方法
    public Vector3 GetCachedPosition()
    {
        return cachedPosition;
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
        edgeHeightMap = data.edgeHeightMap; // 新增边缘高度图设置
        rotation = data.rotation;
        
        // 同时初始化缓存字段
        cachedSize = data.size;
        cachedIsObstacle = data.isObstacle;
        cachedIsSeed = data.isSeed;
        cachedLightHeight = data.lightHeight;
        cachedHeightMap = data.heightMap;
        cachedEdgeHeightMap = data.edgeHeightMap; // 新增边缘高度图缓存
        cachedRotation = data.rotation;
        cachedPosition = transform.position;
        cachedRotationQuaternion = transform.rotation;
        
        // 创建quad
        if (edgeHeightMap != null)
        {
            CreateEdgeQuad();
        }
        
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
            center = new Vector2(cachedPosition.x, cachedPosition.z); // 使用缓存的position
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

    // 创建quad的方法
    public void CreateEdgeQuad()
    {
        // 如果已经有quad，先删除
        DestroyEdgeQuad();
        
        // 修改：优先使用专门的边缘高度图材质，如果没有则使用通用材质
        // 如果没有edgeHeightMap或者材质都为null，则不创建
        if (edgeHeightMap == null || 
           (LightingManager.instance.edgeHeightMapMaterial == null && 
            LightingManager.instance.imageDisplayMaterial == null))
        {
            return;
        }
        
        // 创建一个新的游戏对象作为quad容器
        edgeQuad = new GameObject($"EdgeQuad_{gameObject.name}");
        edgeQuad.transform.SetParent(transform);
        
        // 创建一个Quad作为图片显示
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.layer = 13;
        quad.transform.SetParent(edgeQuad.transform);
        
        // 修改：考虑rotation属性
        float yOffset = 0.01f * (GetInstanceID() % 1000) / 1000f; // 基于实例ID创建微小偏移
        edgeQuad.transform.localPosition = new Vector3(0, 0.05f+yOffset, 0);
        // 水平放置quad，但要考虑rotation属性
        edgeQuad.transform.localRotation = Quaternion.Euler(90, -rotation, 0);
        
        // 设置quad的缩放以匹配size
        quad.transform.localScale = new Vector3(size, size, 1);
        quad.transform.localPosition = Vector3.zero;
        
        // 使用预制材质创建新材质
        Material material;
        
        // 修改：优先使用专门的边缘高度图材质
        if (LightingManager.instance.edgeHeightMapMaterial != null)
        {
            material = new Material(LightingManager.instance.edgeHeightMapMaterial);
        }
        else
        {
            material = new Material(LightingManager.instance.imageDisplayMaterial);
        }
        
        // 设置纹理
        material.mainTexture = edgeHeightMap;
        
        // 应用材质
        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.material = material;
        
        // 根据LightingManager中的显示状态设置可见性
        renderer.enabled = LightingManager.showLightingQuads;
        
        // 添加到管理器的列表中
        LightingManager.lightingQuads.Add(edgeQuad);
    }

    // 删除quad的方法
    public void DestroyEdgeQuad()
    {
        if (edgeQuad != null)
        {
            // 从管理器的列表中移除
            LightingManager.lightingQuads.Remove(edgeQuad);
            
            // 销毁游戏对象
            if (Application.isPlaying)
            {
                Destroy(edgeQuad);
            }
            else
            {
                #if UNITY_EDITOR
                DestroyImmediate(edgeQuad);
                #endif
            }
            edgeQuad = null;
        }
    }
        
}



