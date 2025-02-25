using UnityEngine;

public class HeightMapSampler : MonoBehaviour
{
    public Texture2D heightMap; // 关联的高度图纹理
    public float heightScale = 10f; // 高度缩放系数

    // 通过世界坐标获取高度
    public float SampleHeight(Vector3 worldPosition)
    {
        // 将世界坐标转换为UV坐标（0-1范围）
        Vector3 localPos = transform.InverseTransformPoint(worldPosition);
        Vector2 uv = new Vector2(
            (localPos.x + 0.5f),  // 假设物体中心在原点，尺寸为1x1
            (localPos.z + 0.5f)
        );

        // 采样高度图（使用Bilinear过滤）
        float height = heightMap.GetPixelBilinear(uv.x, uv.y).grayscale;
        return height * heightScale;
    }

    // 直接通过UV坐标获取高度
    public float SampleHeightByUV(Vector2 uv)
    {
        // ... existing code ...
        return heightMap.GetPixelBilinear(uv.x, uv.y).grayscale * heightScale;
    }
} 