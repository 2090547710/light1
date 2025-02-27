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
    public float size;
    public bool isObstacle;
    public Texture2D heightMap;
    public Vector2 tiling;
    public Vector2 offset;
    [Range(0, 1)] public float lightHeight;
    private AreaMapData areaMapData;
    private Bounds area;

    [Header("节点影响")]
    public int LightAffectedNodesCount;
    public int DarkAffectedNodesCount;

    // 添加缓存字段
    [Header("调试信息")]
    [SerializeField] private float cachedSize;
    [SerializeField] private bool cachedIsObstacle;
    [SerializeField] private Texture2D cachedHeightMap;
    [SerializeField] private Vector2 cachedTiling;
    [SerializeField] private Vector2 cachedOffset;
    [SerializeField] private float cachedLightHeight;

    private void OnEnable() => LightingManager.RegisterLight(this);
    private void OnDisable() => LightingManager.UnregisterLight(this);

    public int ApplyLighting()
    {
        // 初始化区域数据
        areaMapData = new AreaMapData(
            heightMap,
            tiling == default ? Vector2.one : tiling,  // 使用默认值保护
            offset
        );
        // 初始化区域范围
        area = new Bounds(transform.position, new Vector3(size, lightHeight, size));

        Vector2 counts = LightingManager.tree.MarkIlluminatedArea(
            area,
            isObstacle,
            areaMapData
        );
        LightAffectedNodesCount = (int)counts.x;
        DarkAffectedNodesCount = (int)counts.y;
        return LightAffectedNodesCount + DarkAffectedNodesCount;
    }

    public void RemoveLighting()
    {
        //在光照管理器在通知光照管理器更新光照
        LightingManager.UnregisterLight(this);
        LightingManager.UpdateLighting(); 
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
        Gizmos.DrawWireCube(area.center, area.size);
    }

    private void OnValidate()
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
        }

        ValidateHeightmap();

        // 如果有参数变化且应用在编辑器运行时
        if (needsUpdate && Application.isPlaying)
        {
            LightingManager.UpdateLighting();
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
        
        // 验证分辨率
        if (heightMap.width != 256 || heightMap.height != 256)
        {
            Debug.LogError($"高度图分辨率必须为256x256，当前：{heightMap.width}x{heightMap.height} ({name})", this);
            isValid = false;
        }

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
}

