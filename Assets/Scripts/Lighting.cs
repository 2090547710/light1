using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 光照组件
public class Lighting : MonoBehaviour
{
    [Range(-10, 30)] public float Radius = 5f;

    
    
    void OnEnable()
    {
        LightingManager.RegisterLight(this);
    }


    void OnDisable()
    {
        LightingManager.UnregisterLight(this);
    }


    public void ApplyLighting()
    {
        Vector2 position = new Vector2(transform.position.x, transform.position.z);
        LightingManager.tree.MarkIlluminatedArea(position, Radius);
    }


    private void OnDrawGizmosSelected()
    {
        // 根据半径正负设置不同颜色
        Gizmos.color = Radius < 0 ? 
            new Color(0, 0, 0) :  // 黑色表示负半径
            new Color(0, 1, 0);   // 绿色表示正半径

        Vector3 center = transform.position;
        float theta = 0;
        int segments = 36;
        
        // 使用绝对值确保绘制正确
        float drawRadius = Mathf.Abs(Radius);

        if (Radius < 0)
        {
            // 绘制正方形（边长为两倍drawRadius）
            float halfSize = drawRadius; // 因为总边长是2*drawRadius
            Vector3[] squarePoints = new Vector3[4]
            {
                center + new Vector3(-halfSize, 0, -halfSize),
                center + new Vector3(halfSize, 0, -halfSize),
                center + new Vector3(halfSize, 0, halfSize),
                center + new Vector3(-halfSize, 0, halfSize)
            };
            
            // 连接四个边
            Gizmos.DrawLine(squarePoints[0], squarePoints[1]);
            Gizmos.DrawLine(squarePoints[1], squarePoints[2]);
            Gizmos.DrawLine(squarePoints[2], squarePoints[3]);
            Gizmos.DrawLine(squarePoints[3], squarePoints[0]);
        }
        else
        {
            // 保持原有圆形绘制逻辑
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