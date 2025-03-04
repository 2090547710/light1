using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// 高度图数据结构
[System.Serializable]
public struct AreaMapData
{
    public Texture2D heightMap;
    public Vector2 tiling;
    public Vector2 offset;

    public AreaMapData(Texture2D map = null, Vector2 tiling = default, 
                      Vector2 offset = default)
    {
        heightMap = map;
        this.tiling = tiling == default ? Vector2.one : tiling;
        this.offset = offset;
    }
}

public class Lighting : MonoBehaviour
{
    [Header("区域设置")]
    [Range(0,100)]public float size;
    public bool isObstacle;
    public Texture2D heightMap;
    public Vector2 tiling;
    public Vector2 offset;
    [Range(0, 1)] public float lightHeight;

    [Header("节点影响")]
    public float TotalBrightnessImpact;

    // 添加缓存字段
    [Header("调试信息")]
    [SerializeField] private float cachedSize;
    [SerializeField] private bool cachedIsObstacle;
    [SerializeField] private Texture2D cachedHeightMap;
    [SerializeField] private Vector2 cachedTiling;
    [SerializeField] private Vector2 cachedOffset;
    [SerializeField] private float cachedLightHeight;

    // 添加脏标记系统
    [SerializeField] private bool isDirty = true; // 默认为脏，确保首次应用
    public bool IsDirty => isDirty;

    // 新增哈希表记录重叠的光源
    [Header("重叠光源信息")]
    [SerializeField] private Dictionary<int, Lighting> overlappingLights = new Dictionary<int, Lighting>();
    
    // 新增公共属性用于获取重叠光源
    public IReadOnlyDictionary<int, Lighting> OverlappingLights => overlappingLights;

    private void OnEnable() 
    {
        LightingManager.RegisterLight(this);
    }
    
    private void OnDisable() 
    {

        LightingManager.UnregisterLight(this);
    }
    
    // 更新当前光源与其他光源的重叠关系
    public void UpdateOverlappingLights(bool useCachedData = false)
    {
        // 先清除现有关系
        overlappingLights.Clear();
        
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
        // 清除重叠关系
        ClearOverlappingRelationships();
        
        // 通知光照管理器更新光照
        LightingManager.UpdateLighting(); 
         LightingManager.UnregisterLight(this);
    }

    public AreaMapData GetAreaMapData()
    {
        return new AreaMapData(
            heightMap,
            tiling == default ? Vector2.one : tiling,
            offset
        );
    }

    public Bounds GetWorldBounds()
    {
        Vector3 center = transform.position;
        Vector3 size = new Vector3(this.size, lightHeight, this.size);
        return new Bounds(center, size);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = isObstacle ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(size, 0, size));
        
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
        bool needsUpdate = false;
        
        // 检查每个参数是否发生变化
        if (cachedSize != size || 
            cachedIsObstacle != isObstacle ||
            cachedHeightMap != heightMap ||
            cachedTiling != tiling ||
            cachedOffset != offset ||
            cachedLightHeight != lightHeight)
        {
            needsUpdate = true;
            isDirty = true; // 设置为脏
        }

        ValidateHeightmap();

        // 如果有参数变化且应用在编辑器运行时
        if (needsUpdate && Application.isPlaying)
        {
            LightingManager.UpdateDirtyLights(); // 使用新方法更新脏光源
        }

        // 更新缓存值
        cachedSize = size;
        cachedIsObstacle = isObstacle;
        cachedHeightMap = heightMap;
        cachedTiling = tiling;
        cachedOffset = offset;
        cachedLightHeight = lightHeight;
    }
#endif

    public bool ValidateHeightmap()
    {
        if (heightMap == null)
        {
            Debug.LogWarning($"光源 {name} 未分配高度图", this);
            return false;
        }

        bool isValid = true;
        
        // 验证格式
        bool formatValid = false;
#if UNITY_EDITOR
        // 编辑器模式下检查原始格式
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(heightMap)) as TextureImporter;
        if (importer != null)
        {
            var platformSettings = importer.GetDefaultPlatformTextureSettings();
            formatValid = platformSettings.format == TextureImporterFormat.R8;
        }
#else
        // 运行时检查实际格式
        formatValid = heightMap.format == TextureFormat.R8;
#endif

        if (!formatValid)
        {
            Debug.LogError($"高度图格式必须为R8，当前格式：{heightMap.format} ({name})", this);
            isValid = false;
        }

        return isValid;
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
            cachedTiling == default ? Vector2.one : cachedTiling,
            cachedOffset
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

    // 添加新方法：检测两个边界在xz平面上是否重叠
    private bool IsOverlappingOnXZPlane(Bounds a, Bounds b)
    {
        // 只检查x和z轴方向的重叠，忽略y轴
        bool overlapX = Mathf.Abs(a.center.x - b.center.x) <= (a.size.x + b.size.x) * 0.5f;
        bool overlapZ = Mathf.Abs(a.center.z - b.center.z) <= (a.size.z + b.size.z) * 0.5f;
        
        return overlapX && overlapZ;
    }
}

