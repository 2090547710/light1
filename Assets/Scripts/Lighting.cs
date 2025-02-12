using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 光照组件
[RequireComponent(typeof(CustomCollider))]
public class Lighting : MonoBehaviour
{
    [Range(-10, 30)] public float Radius = 5f;
    public int LightNodesAffected;
    public int DarkNodesAffected;
    [Range(0, 30)] public float LightHeight = 1.0f;
    public bool isDark = false;
    public CustomCollider customCollider;

    void OnEnable()
    {
        LightingManager.RegisterLight(this);
    }


    void OnDisable()
    {
        LightingManager.UnregisterLight(this);
    }

    void Start()
    {
        customCollider = GetComponent<CustomCollider>(); 
        LightHeight = customCollider.UnitHeightY;
    }

    public int ApplyLighting()
    {
        if(!isDark || Radius <= 0){
            // 保存原始尺寸
            Vector2 originalSize = customCollider.UnitSizeXZ;
            
            // 临时修改碰撞体尺寸用于计算边界
            customCollider.UnitSizeXZ = new Vector2(Mathf.Abs(Radius)*2, Mathf.Abs(Radius)*2);
            customCollider.UnitHeightY = LightHeight;
            
            Bounds bounds= customCollider.Bounds;
            
            // 恢复原始尺寸
            customCollider.UnitSizeXZ = originalSize;
            
            ResetCounters();
            int count = LightingManager.tree.MarkIlluminatedArea(bounds,isDark);
            if (isDark)
            {
                DarkNodesAffected += count;
            }
            else
            {
                LightNodesAffected += count;
            }
            
            return count;
        }else
        {
            LightingManager.tree.MarkIlluminatedArea(customCollider.Bounds,isDark);
            return 0;
        }
                 
    }

    public void ResetCounters()
    {
        LightNodesAffected = 0;
        DarkNodesAffected = 0;
    }

    private void OnDrawGizmosSelected()
    {
        // 根据光照类型设置颜色
        Gizmos.color = isDark ? 
           new Color(0, 0, 0) : new Color(0, 1, 0);//表示黑暗矩形，绿色表示光明区域

        // 确保在编辑器模式下也能获取collider
        if (!Application.isPlaying)
        {
            customCollider = GetComponent<CustomCollider>();
        }

        Vector3 center = transform.position;
        float theta = 0;
        int segments = 36;
        
        if (isDark)
        {
            float size = Radius > 0 ? 
                customCollider.UnitSizeXZ.x * customCollider.MinNodeSize.x : 
                Mathf.Abs(Radius) * customCollider.MinNodeSize.x * 2;

            // 绘制矩形（当Radius>0）或正方形（当Radius<=0）
            float halfSize = size / 2;
            Vector3[] points = new Vector3[4]
            {
                center + new Vector3(-halfSize, 0, -halfSize),
                center + new Vector3(halfSize, 0, -halfSize),
                center + new Vector3(halfSize, 0, halfSize),
                center + new Vector3(-halfSize, 0, halfSize)
            };
            
            Gizmos.DrawLine(points[0], points[1]);
            Gizmos.DrawLine(points[1], points[2]);
            Gizmos.DrawLine(points[2], points[3]);
            Gizmos.DrawLine(points[3], points[0]);
        }
        else
        {
            // 光明区域保持圆形，使用绝对半径值
            float drawRadius = Mathf.Abs(Radius) * customCollider.MinNodeSize.x;
            for(int i = 0; i < segments; i++){
                Vector3 pos1 = center + new Vector3(
                    Mathf.Cos(theta) * drawRadius,
                    0,
                    Mathf.Sin(theta) * drawRadius
                );
                theta += (2f * Mathf.PI) / segments;
                Vector3 pos2 = center + new Vector3(
                    Mathf.Cos(theta) * drawRadius,
                    0,
                    Mathf.Sin(theta) * drawRadius
                );
                Gizmos.DrawLine(pos1, pos2);
            }
        }
    }
}