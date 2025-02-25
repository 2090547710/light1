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
    [Range(0, 10)] public float heightScale;

    public AreaMapData(Texture2D map = null, Vector2 tiling = default, 
                      Vector2 offset = default, float scale = 1f)
    {
        heightMap = map;
        this.tiling = tiling == default ? Vector2.one : tiling;
        this.offset = offset;
        heightScale = scale;
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
    [Range(0, 10)] public float heightScale;
    private AreaMapData areaMapData;
    private Bounds area;

    [Header("节点影响")]
    public int LightAffectedNodesCount;
    public int DarkAffectedNodesCount;

    private void OnEnable() => LightingManager.RegisterLight(this);
    private void OnDisable() => LightingManager.UnregisterLight(this);

    public int ApplyLighting()
    {
        // 新增高度图验证
        if (heightMap != null && (heightMap.width != 256 || heightMap.height != 256))
        {
            Debug.LogError($"高度图尺寸必须为256x256，当前为{heightMap.width}x{heightMap.height}");
            return 0;
        }
        if (heightMap == null || heightMap.format != TextureFormat.R8)
        {
            Debug.LogError("需要有效的单通道高度图（R8格式）");
            return 0;
        }
        // 初始化区域数据
        areaMapData = new AreaMapData(
            heightMap,
            tiling == default ? Vector2.one : tiling,  // 使用默认值保护
            offset,
            heightScale
        );
        // 初始化区域范围
        area = new Bounds(transform.position, new Vector3(size, 0, size));

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
        LightingManager.tree.RemoveIlluminationEffect(area);
        LightingManager.UnregisterLight(this);
    }

    public AreaMapData GetAreaMapData()
    {
        return new AreaMapData(
            heightMap,
            tiling == default ? Vector2.one : tiling,
            offset,
            heightScale
        );
    }

    public Bounds GetWorldBounds()
    {
        Vector3 center = transform.position;
        Vector3 size = new Vector3(this.size, 0, this.size);
        return new Bounds(center, size);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = isObstacle ? Color.red : Color.yellow;
        Gizmos.DrawWireCube(area.center, area.size);
    }
#endif
}

