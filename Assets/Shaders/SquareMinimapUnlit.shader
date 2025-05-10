Shader "Custom/SquareMinimapUnlit"
{
    Properties
    {
        _MapColor ("地图颜色", Color) = (1,1,1,1)
        _PlayerColor ("玩家颜色", Color) = (1,0,0,1)
        _PlayerOutlineColor ("玩家描边颜色", Color) = (0,0,0,1)
        _BeginPointColor ("起点颜色", Color) = (0,1,0,1)
        _EndPointColor ("终点颜色", Color) = (0,0,1,1)
        _PointOutlineColor ("标记点描边颜色", Color) = (0,0,0,1)
        _PlantColor ("植物颜色", Color) = (0.5,0.8,0.2,1)
        _PlantOutlineColor ("植物描边颜色", Color) = (0.2,0.3,0.1,1)
        _PlantSize ("植物图标大小", Range(0.1, 1.0)) = 0.3
        _PointSize ("标记点大小", Range(0.1, 2.0)) = 0.4
        _PointOutlineSize ("标记点描边大小", Range(0.01, 0.3)) = 0.15
        _PlayerDotSize ("玩家标记大小", Range(0.1, 2.0)) = 0.5
        _PlayerOutlineSize ("玩家描边大小", Range(0.01, 0.3)) = 0.15
        _MapSize ("地图显示尺寸", Range(10, 100)) = 30
        _MapScale ("地图缩放", Range(0.1, 2.0)) = 1.0
        _MapBorderColor ("地图边框颜色", Color) = (0,0,0,1)
        _MapBorderSize ("地图边框大小", Range(0.001, 0.05)) = 0.01
        _FireColor ("火植物颜色", Color) = (1,0.3,0,1)
        _FireOutlineColor ("火植物描边颜色", Color) = (0.5,0.1,0,1)
        _FireSize ("火植物图标大小", Range(0.1, 1.0)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _CompositeMap;
            uniform float4 _HeightmapParams;
            uniform float3 _PlayerWorldPos;
            uniform float3 _BeginPointPos;
            uniform float3 _EndPointPositions[64];
            uniform int _EndPointCount;
            uniform float _ShowBeginPoint;
            uniform float _ShowEndPoint;
            
            // 添加摄像机相关参数
            uniform float3 _CameraWorldPos;  // 摄像机位置
            uniform float _CameraRotationY;  // 摄像机Y轴旋转角度
            uniform float _CameraZoom;       // 摄像机缩放值
            
            // 添加植物位置数组(最多支持64个植物)
            uniform float3 _PlantPositions[64];
            uniform int _PlantCount;
            
            uniform float4 _FirePositions[64];
            uniform int _FireCount;
            
            fixed4 _MapColor;
            fixed4 _PlayerColor;
            fixed4 _PlayerOutlineColor;
            fixed4 _BeginPointColor;
            fixed4 _EndPointColor;
            fixed4 _PointOutlineColor;
            fixed4 _PlantColor;
            fixed4 _PlantOutlineColor;
            float _PlantSize;
            float _PointSize;
            float _PointOutlineSize;
            float _PlayerDotSize;
            float _PlayerOutlineSize;
            float _MapSize;
            float _MapScale;
            fixed4 _MapBorderColor;
            float _MapBorderSize;
            fixed4 _FireColor;
            fixed4 _FireOutlineColor;
            float _FireSize;

            // 添加旋转点函数
            float2 RotatePoint(float2 pos, float2 center, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                
                // 将点移到原点
                float2 translatedPoint = pos - center;
                
                // 旋转点
                float2 rotatedPoint = float2(
                    translatedPoint.x * c - translatedPoint.y * s,
                    translatedPoint.x * s + translatedPoint.y * c
                );
                
                // 移回原来的位置
                return rotatedPoint + center;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            // 辅助函数：绘制方块
            bool IsInsideSquare(float2 pos, float2 center, float size)
            {
                float2 dist = abs(pos - center);
                return max(dist.x, dist.y) < size;
            }
            
            // 辅助函数：绘制方块边框
            bool IsInsideSquareOutline(float2 pos, float2 center, float innerSize, float outerSize)
            {
                float2 dist = abs(pos - center);
                float maxDist = max(dist.x, dist.y);
                return maxDist < outerSize && maxDist >= innerSize;
            }
            
            // 辅助函数：判断点是否在三角形内部
            bool IsInsideTriangle(float2 p, float2 center, float size)
            {
                // 定义三角形的三个顶点，朝上的等边三角形
                float2 p1 = center + float2(0, -size);           // 底部中心点
                float2 p2 = center + float2(-size*0.866, size*0.5); // 左上角
                float2 p3 = center + float2(size*0.866, size*0.5);  // 右上角
                
                // 计算重心坐标
                float alpha = ((p2.y - p3.y)*(p.x - p3.x) + (p3.x - p2.x)*(p.y - p3.y)) /
                             ((p2.y - p3.y)*(p1.x - p3.x) + (p3.x - p2.x)*(p1.y - p3.y));
                
                float beta = ((p3.y - p1.y)*(p.x - p3.x) + (p1.x - p3.x)*(p.y - p3.y)) /
                            ((p2.y - p3.y)*(p1.x - p3.x) + (p3.x - p2.x)*(p1.y - p3.y));
                
                float gamma = 1.0 - alpha - beta;
                
                // 如果三个系数都为正，点在三角形内部
                return alpha > 0 && beta > 0 && gamma > 0;
            }
            
            // 辅助函数：绘制三角形边框
            bool IsInsideTriangleOutline(float2 p, float2 center, float innerSize, float outerSize)
            {
                return IsInsideTriangle(p, center, outerSize) && !IsInsideTriangle(p, center, innerSize);
            }

            // 辅助函数：判断点是否在倒三角形内部
            bool IsInsideInvertedTriangle(float2 p, float2 center, float size)
            {
                // 定义倒三角形的三个顶点
                float2 p1 = center + float2(0, size);            // 顶部中心点
                float2 p2 = center + float2(-size*0.866, -size*0.5); // 左下角
                float2 p3 = center + float2(size*0.866, -size*0.5);  // 右下角
                
                // 计算重心坐标
                float alpha = ((p2.y - p3.y)*(p.x - p3.x) + (p3.x - p2.x)*(p.y - p3.y)) /
                             ((p2.y - p3.y)*(p1.x - p3.x) + (p3.x - p2.x)*(p1.y - p3.y));
                
                float beta = ((p3.y - p1.y)*(p.x - p3.x) + (p1.x - p3.x)*(p.y - p3.y)) /
                            ((p2.y - p3.y)*(p1.x - p3.x) + (p3.x - p2.x)*(p1.y - p3.y));
                
                float gamma = 1.0 - alpha - beta;
                
                // 如果三个系数都为正，点在三角形内部
                return alpha > 0 && beta > 0 && gamma > 0;
            }
            
            // 辅助函数：绘制倒三角形边框
            bool IsInsideInvertedTriangleOutline(float2 p, float2 center, float innerSize, float outerSize)
            {
                return IsInsideInvertedTriangle(p, center, outerSize) && !IsInsideInvertedTriangle(p, center, innerSize);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 方形地图边框
                float2 border = abs(i.uv - 0.5) * 2.0;
                float maxBorder = max(border.x, border.y);
                
                // 计算边框
                if (maxBorder > (1.0 - _MapBorderSize * 2.0) && maxBorder < 1.0)
                {
                    return _MapBorderColor;
                }
                
                // 根据摄像机缩放调整地图尺寸
                float adjustedMapSize = _MapSize * (1.0 + (_CameraZoom - 10) / 40.0);  // 10是初始缩放值，40是最大缩放范围
                
                // 将UV坐标转换为以玩家为中心的世界坐标偏移
                float2 offset = (i.uv - 0.5) * adjustedMapSize * _MapScale;
                
                // 应用相机旋转（弧度制），但旋转方向相反
                float rotationRad = _CameraRotationY * 3.14159265359 / 180.0;
                // 使用相反的旋转角度，这样当相机向左时，地图会向右移动
                float2 rotatedOffset = RotatePoint(offset, float2(0, 0), -rotationRad);
                
                // 计算实际的世界坐标点
                float2 worldPos = float2(_PlayerWorldPos.x, _PlayerWorldPos.z) + rotatedOffset;
                
                // 将世界坐标转换为_CompositeMap的UV坐标
                float2 heightmapUV = (worldPos - _HeightmapParams.xy + _HeightmapParams.zw*0.5) / _HeightmapParams.zw;
                heightmapUV = clamp(heightmapUV, 0, 1);
                
                // 从CompositeMap获取光照数据
                float4 lightData = tex2D(_CompositeMap, heightmapUV);
                float lightIntensity = lightData.r; // 使用红色通道存储的光照数据
                
                // 使用亮度创建地图颜色
                fixed4 mapColor = _MapColor * lightIntensity;
                
                // 绘制植物位置(三角形)
                for (int p = 0; p < _PlantCount; p++) {
                    // 计算植物相对于玩家的偏移
                    float2 plantPos = float2(_PlantPositions[p].x, _PlantPositions[p].z);
                    float2 playerPos = float2(_PlayerWorldPos.x, _PlayerWorldPos.z);
                    float2 plantOffset = plantPos - playerPos;
                    
                    // 应用旋转
                    plantOffset = RotatePoint(plantOffset, float2(0, 0), rotationRad);
                    
                    // 转换为UV坐标偏移
                    plantOffset = plantOffset / adjustedMapSize / _MapScale;
                    // 将植物图标稍微上移（Y轴负方向为上）
                    float2 plantUV = float2(0.5, 0.5) + plantOffset + float2(0, 0.03);
                    
                    // 检查植物是否在地图范围内
                    if (abs(plantUV.x - 0.5) < 0.5 && abs(plantUV.y - 0.5) < 0.5) {
                        // 绘制植物三角形(带描边)
                        if (IsInsideTriangleOutline(i.uv, plantUV, _PlantSize/30.0, (_PlantSize*1.1)/30.0)) {
                            return _PlantOutlineColor;
                        }
                        if (IsInsideTriangle(i.uv, plantUV, _PlantSize/30.0)) {
                            return _PlantColor;
                        }
                    }
                }
                
                // 绘制火植物位置(倒三角形)
                for (int f = 0; f < _FireCount; f++) {
                    // 计算火植物相对于玩家的偏移
                    float2 firePos = float2(_FirePositions[f].x, _FirePositions[f].z);
                    float2 playerPos = float2(_PlayerWorldPos.x, _PlayerWorldPos.z);
                    float2 fireOffset = firePos - playerPos;
                    
                    // 应用旋转
                    fireOffset = RotatePoint(fireOffset, float2(0, 0), rotationRad);
                    
                    // 转换为UV坐标偏移
                    fireOffset = fireOffset / adjustedMapSize / _MapScale;
                    float2 fireUV = float2(0.5, 0.5) + fireOffset;
                    
                    // 检查火植物是否在地图范围内
                    if (abs(fireUV.x - 0.5) < 0.5 && abs(fireUV.y - 0.5) < 0.5) {
                        // 绘制火植物倒三角形(带描边)
                        if (IsInsideInvertedTriangleOutline(i.uv, fireUV, _FireSize/30.0, (_FireSize*1.1)/30.0)) {
                            return _FireOutlineColor;
                        }
                        if (IsInsideInvertedTriangle(i.uv, fireUV, _FireSize/30.0)) {
                            return _FireColor;
                        }
                    }
                }
                
                // 计算起点在地图上的位置（如果激活）
                if (_ShowBeginPoint > 0.5) {
                    // 计算起点相对于玩家的偏移
                    float2 beginPos = float2(_BeginPointPos.x, _BeginPointPos.z);
                    float2 playerPos = float2(_PlayerWorldPos.x, _PlayerWorldPos.z);
                    float2 beginPointOffset = beginPos - playerPos;
                    
                    // 应用旋转
                    beginPointOffset = RotatePoint(beginPointOffset, float2(0, 0), rotationRad);
                    
                    // 转换为UV坐标偏移
                    beginPointOffset = beginPointOffset / adjustedMapSize / _MapScale;
                    float2 beginPointUV = float2(0.5, 0.5) + beginPointOffset;
                    
                    // 检查起点是否在地图范围内
                    if (abs(beginPointUV.x - 0.5) < 0.5 && abs(beginPointUV.y - 0.5) < 0.5) {
                        // 绘制起点方块（带描边）
                        if (IsInsideSquareOutline(i.uv, beginPointUV, _PointSize/25.0, (_PointSize+_PointOutlineSize)/25.0)) {
                            return _PointOutlineColor;
                        }
                        if (IsInsideSquare(i.uv, beginPointUV, _PointSize/25.0)) {
                            return _BeginPointColor;
                        }
                    }
                }
                
                // 计算终点在地图上的位置（如果激活）
                if (_ShowEndPoint > 0.5) {
                    // 遍历所有终点
                    for (int e = 0; e < _EndPointCount; e++) {
                        // 计算终点相对于玩家的偏移
                        float2 endPos = float2(_EndPointPositions[e].x, _EndPointPositions[e].z);
                        float2 playerPos = float2(_PlayerWorldPos.x, _PlayerWorldPos.z);
                        float2 endPointOffset = endPos - playerPos;
                        
                        // 应用旋转
                        endPointOffset = RotatePoint(endPointOffset, float2(0, 0), rotationRad);
                        
                        // 转换为UV坐标偏移
                        endPointOffset = endPointOffset / adjustedMapSize / _MapScale;
                        float2 endPointUV = float2(0.5, 0.5) + endPointOffset;
                        
                        // 检查终点是否在地图范围内
                        if (abs(endPointUV.x - 0.5) < 0.5 && abs(endPointUV.y - 0.5) < 0.5) {
                            // 绘制终点方块（带描边）
                            if (IsInsideSquareOutline(i.uv, endPointUV, _PointSize/25.0, (_PointSize+_PointOutlineSize)/25.0)) {
                                return _PointOutlineColor;
                            }
                            if (IsInsideSquare(i.uv, endPointUV, _PointSize/25.0)) {
                                return _EndPointColor;
                            }
                        }
                    }
                }
                
                // 绘制玩家位置（圆点带描边）
                float playerDist = distance(i.uv, float2(0.5, 0.5)) * 25.0;
                float innerRadius = _PlayerDotSize;
                float outerRadius = innerRadius + _PlayerOutlineSize;
                
                if (playerDist < outerRadius) // 外圆
                {
                    if (playerDist < innerRadius) // 内圆
                    {
                        return _PlayerColor; // 玩家颜色
                    }
                    else
                    {
                        return _PlayerOutlineColor; // 描边颜色
                    }
                }
                
                return mapColor;
            }
            ENDCG
        }
    }
}