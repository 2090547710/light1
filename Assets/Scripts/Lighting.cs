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
    [Range(0, 1)] public float lightHeight;
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
        area = new Bounds(transform.position, new Vector3(size, lightHeight, size));

        WriteToComposite();
        Vector2 counts = LightingManager.tree.MarkIlluminatedArea(
            area,
            isObstacle,
            areaMapData
        );
        LightAffectedNodesCount = (int)counts.x;
        DarkAffectedNodesCount = (int)counts.y;
        return LightAffectedNodesCount + DarkAffectedNodesCount;
    }

    private void WriteToComposite()
    {
        if (heightMap == null) return;

        // 计算根节点范围
        var rootSize = LightingManager.tree.RootSize;
        var rootCenter = LightingManager.tree.RootCenter;
        Bounds rootBounds = new Bounds(
            new Vector3(rootCenter.x, 0, rootCenter.y), 
            new Vector3(rootSize.x, 0, rootSize.y)
        );

        // 计算当前光源的UV范围
        Vector3 min = area.center - area.extents;
        Vector3 max = area.center + area.extents;
        
        float uvMinX = Mathf.InverseLerp(rootBounds.min.x, rootBounds.max.x, min.x);
        float uvMaxX = Mathf.InverseLerp(rootBounds.min.x, rootBounds.max.x, max.x);
        float uvMinY = Mathf.InverseLerp(rootBounds.min.z, rootBounds.max.z, min.z);
        float uvMaxY = Mathf.InverseLerp(rootBounds.min.z, rootBounds.max.z, max.z);

        // 遍历光源高度图的每个像素
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                // 计算合成图坐标
                float targetX = Mathf.Lerp(uvMinX, uvMaxX, x / 255f);
                float targetY = Mathf.Lerp(uvMinY, uvMaxY, y / 255f);
                
                int pixelX = Mathf.FloorToInt(targetX * (LightingManager.compositeSize - 1));
                int pixelY = Mathf.FloorToInt(targetY * (LightingManager.compositeSize - 1));
                
                // 写入红色通道
                int index = pixelY * LightingManager.compositeSize + pixelX;
                float height = heightMap.GetPixel(x, y).r;

                if (isObstacle)
                {
                    // 障碍物写入绿色通道
                    LightingManager.compositePixels[index].g = height;
                }
                else 
                {
                    // 获取当前障碍物高度
                    float obstacleHeight = LightingManager.compositePixels[index].g;
                    // 比较光照高度与障碍物高度，原高度>0.01视为有效光照
                    if (area.size.y >= obstacleHeight && height > 0.01f)
                    {
                        LightingManager.compositePixels[index].r = height;
                    }
                }
            }
        }
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
        Vector3 size = new Vector3(this.size, lightHeight, this.size);
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

