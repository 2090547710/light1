using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

// 新增枚举类型
public enum AreaShape { Circle, Rectangle }
public enum AreaType
{        
    Seed,
    Dark,
    Light,     
    Obstacle, 
}

// 新增光照数据结构体
[System.Serializable]
public struct LightingData
{
    public float areaSizeX;
    public float areaSizeZ;
    [Range(-1,1)] public float areaHeight;
    public AreaShape areaShape;
    public AreaType areaType;
    [Range(0,1)] public float lightHeight;

    public LightingData(
        float x = 1f, 
        float z = 1f,
        float height = 0.01f,
        AreaShape shape = AreaShape.Rectangle,
        AreaType type = AreaType.Dark,
        float lHeight = 0.01f)
    {
        areaSizeX = x;
        areaSizeZ = z;
        areaHeight = height;
        areaShape = shape;
        areaType = type;
        lightHeight = lHeight;
    }
}

// 光照组件
public class Lighting : MonoBehaviour
{
    [Header("区域设置")]
    [Tooltip("圆形会以area.size.x作为直径")]
    [Range(0,100)]public float areaSizeX = 0f;
    [Range(0,100)]public float areaSizeZ = 0f;
    [Range(-1,1)]public float areaHeight = 0f;
    public AreaShape areaShape = AreaShape.Rectangle;
     public AreaType areaType = AreaType.Light;
    [Tooltip("光照影响高度（不能小于区域高度）")]
    [Range(0,1)]public float lightHeight = 0f; 

    [Header("节点影响")]
    public int LightNodesAffected;
    public int DarkNodesAffected;

    private Bounds area;

    // 新增线宽控制参数（在类开头添加）
    public static float GizmoLineWidth = 2.0f;

    // 添加属性变更检测字段
    private float cachedAreaSizeX;
    private float cachedAreaSizeZ;
    private float cachedAreaHeight;
    private AreaType cachedAreaType;
    private AreaShape cachedAreaShape;
    private float cachedLightHeight;

    void OnEnable()
    {
        LightingManager.RegisterLight(this);
        LightingManager.UpdateLighting();
    }

    void OnDisable()
    {
        LightingManager.UnregisterLight(this);
        LightingManager.UpdateLighting();
    }

    void Start()
    {
        CacheCurrentProperties();

    }

    void Update()
    {
      
        // 仅当关键属性变化时更新
        if (HasPropertyChanged())
        {
            CacheCurrentProperties();
            LightingManager.UpdateLighting();
        }
    }

    // 新增属性变更检测方法
    private bool HasPropertyChanged()
    {
        return areaSizeX != cachedAreaSizeX ||
               areaSizeZ != cachedAreaSizeZ ||
               areaHeight != cachedAreaHeight ||
               areaType != cachedAreaType ||
               areaShape != cachedAreaShape ||
               lightHeight != cachedLightHeight;
    }

    private void CacheCurrentProperties()
    {
        cachedAreaSizeX = areaSizeX;
        cachedAreaSizeZ = areaSizeZ;
        cachedAreaHeight = areaHeight;
        cachedAreaType = areaType;
        cachedAreaShape = areaShape;
        cachedLightHeight = lightHeight;
    }

    // 添加编辑器即时更新支持
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            if (HasPropertyChanged())
            {
                CacheCurrentProperties();
                LightingManager.UpdateLighting();
            }
        }
    }
#endif

    // 应用光照
    public int ApplyLighting()
    {
        
        Debug.Log("ApplyLighting: " + areaType);
        ResetCounters();
        bool isRect = (areaShape == AreaShape.Rectangle);
        bool isDark = (areaType != AreaType.Light );
        
        // 根据区域类型设置参数限制
        switch (areaType)
        {
            case AreaType.Seed:
                areaHeight = 0f;
                lightHeight = 0f;
                break;
            case AreaType.Light:
                lightHeight = Mathf.Clamp(lightHeight, 0.11f, 1f);
                areaHeight = Mathf.Clamp(areaHeight, -1f, -0.01f);
                break;
            case AreaType.Dark:
                areaHeight = Mathf.Clamp(areaHeight, 0.01f, 0.1f); 
                lightHeight = areaHeight; // 强制同步lightHeight
                break;
            case AreaType.Obstacle:
                areaHeight = Mathf.Clamp(areaHeight, 0.11f, 1f); // 0.1f以上为障碍物
                lightHeight = areaHeight; // 强制同步lightHeight
                break;
        }

        // 仅在area未初始化时设置默认值
        Vector3 size = new Vector3(areaSizeX, areaHeight, areaSizeZ);
        if (size == Vector3.zero)
        {
         // 初始化设置
            area.size = Vector3.one;
        }else{
            area.size = size;
        }
        area.center = new Vector3(transform.position.x, 0, transform.position.z);


        int count = LightingManager.tree.MarkIlluminatedArea(
            area,
            isRect,
            isDark,
            lightHeight
        );

        switch (areaType)
        {
            case AreaType.Light:
                LightNodesAffected += count;
                break;
            case AreaType.Seed:
                DarkNodesAffected += count;
                break;
            case AreaType.Dark:
                DarkNodesAffected += count;
                break;
            case AreaType.Obstacle:
                break;
        }

        
        return count;
    }

    public void ResetCounters()
    {
        LightNodesAffected = 0;
        DarkNodesAffected = 0;
    }

    // 移除光照
    public void RemoveLighting()
    {
        

        ResetCounters();
        bool isRect = (areaShape == AreaShape.Rectangle);
        LightingManager.tree.RemoveIlluminationEffect(area, isRect, areaType);
        // 并调用LightingManager.UnregisterLight(this)
        // 同时重置LightNodesAffected和DarkNodesAffected的计数
        LightingManager.UnregisterLight(this);
    }
    
    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // 根据状态设置颜色（同步到Handles）
        Color stateColor = (areaType != AreaType.Light) ? 
            new Color(0, 0, 0) : new Color(0, 1, 0);
        Gizmos.color = stateColor;
        Handles.color = stateColor;

        Vector3 center = area.center;
        
        if (areaShape == AreaShape.Rectangle)
        {
            // 修改矩形绘制方式支持线宽
            float halfX = area.size.x * 0.5f;
            float halfZ = area.size.z * 0.5f;
            Vector3[] points = new Vector3[4]
            {
                center + new Vector3(-halfX, 0, -halfZ),
                center + new Vector3(halfX, 0, -halfZ),
                center + new Vector3(halfX, 0, halfZ),
                center + new Vector3(-halfX, 0, halfZ)
            };
            
            // 使用Handles绘制带线宽的线段
            Handles.DrawLine(points[0], points[1], GizmoLineWidth);
            Handles.DrawLine(points[1], points[2], GizmoLineWidth);
            Handles.DrawLine(points[2], points[3], GizmoLineWidth);
            Handles.DrawLine(points[3], points[0], GizmoLineWidth);
        }
        else
        {
            // 修改圆形绘制方式支持线宽
            float radius = area.size.x * 0.5f;
            int segments = 36;
            float theta = 0;
            
            for(int i = 0; i < segments; i++){
                Vector3 pos1 = center + new Vector3(
                    Mathf.Cos(theta) * radius,
                    0,
                    Mathf.Sin(theta) * radius
                );
                theta += (2f * Mathf.PI) / segments;
                Vector3 pos2 = center + new Vector3(
                    Mathf.Cos(theta) * radius,
                    0,
                    Mathf.Sin(theta) * radius
                );
                Handles.DrawLine(pos1, pos2, GizmoLineWidth);
            }
        }
#endif
    }

    // 新增数据初始化方法
    public void InitializeFromData(LightingData data)
    {
        areaSizeX = data.areaSizeX;
        areaSizeZ = data.areaSizeZ;
        areaHeight = data.areaHeight;
        areaShape = data.areaShape;
        areaType = data.areaType;
        lightHeight = data.lightHeight;
        
        // 更新缓存并立即应用修改
        CacheCurrentProperties();
        LightingManager.UpdateLighting();
    }

}