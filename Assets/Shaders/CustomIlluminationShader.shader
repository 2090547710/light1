Shader "Custom/CustomIlluminationShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        
        [Header(Brightness Settings)]
        _MinBrightness ("Minimum Brightness", Range(0,1)) = 0.2
        _MaxBrightness ("Maximum Brightness", Range(0,2)) = 1.0
        
        [Header(Light Falloff Settings)]
        [Space(10)]
        _LightRectFalloff ("Light Rect Falloff", Range(0.0,1)) = 0.0
        _LightCircleFalloff ("Light Circle Falloff", Range(0.1,1)) = 0.8
        
        [Header(Darkness Settings)]
        _DarknessFactor ("Darkness Strength", Range(0,1)) = 0.0
        [Space(10)]
        _DarknessRectFalloff ("Darkness Rect Falloff", Range(0.1,1)) = 0.5
        _DarknessCircleFalloff ("Darkness Circle Falloff", Range(0.1,2)) = 0.5

        _LightFalloff ("Light Falloff", Range(0.1,1)) = 0.8
        _DarknessFalloff ("Darkness Falloff", Range(0.1,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        float _LightCount;
        float4 _LightPositions[1000];
        float4 _LightSizes[1000];

        half _MinBrightness;
        half _MaxBrightness;
        half _LightFalloff;
        half _DarknessFactor;
        half _DarknessFalloff;
        half _LightRectFalloff;
        half _LightCircleFalloff;
        half _DarknessRectFalloff;
        half _DarknessCircleFalloff;
        

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        // 在surf函数之前添加光照判断函数
        //光照过滤（按光源插入顺序计算）
        
        //第二层：光源高度低于当前区域高度光源
        float IsIlluminated(float3 worldPos)
        {
            float illumination = 0.0;
            float currentHeight = -2;
            bool isInDark = false;    

            for (int i = 0; i < _LightCount; i++)
            {
                float4 lightPos = _LightPositions[i];
                float4 lightSize = _LightSizes[i];
                
                // 解析参数
                bool isRect = lightPos.w > 0.5;    // 形状标识
                bool isDark = lightSize.z > 0.5;   // 区域类型
                float lightHeight = lightPos.y;    // 光照高度
                float areaHeight = lightSize.w;    // 区域高度
       
                
                //第一层：区域外光源
                // 区域判断逻辑，如果不在区域内，则跳过
                if (isRect)
                {
                    // 使用完整尺寸计算半宽高
                    float halfWidth = lightSize.x * 0.5; // 现在lightSize.x存储的是完整宽度
                    float halfHeight = lightSize.y * 0.5; // lightSize.y存储的是完整长度
                    
                    if(abs(worldPos.x - lightPos.x) > halfWidth || 
                       abs(worldPos.z - lightPos.z) > halfHeight) 
                    {
                        continue;
                    }
                }
                else
                {
                    // 圆形区域判断（使用lightSize.x作为直径）
                    float distanceToCenter = length(worldPos.xz - lightPos.xz);
                    if (distanceToCenter > lightSize.x*0.5f)
                    {
                        continue;
                    }
                }
                //比较区域高度并更新
                if (areaHeight > currentHeight) 
                {
                    currentHeight = areaHeight;
                }
                // 高度过滤，过滤来自低高度光源的光照
                if(currentHeight > lightHeight) continue;
                // 根据光源类型处理亮度
                if (!isDark) 
                {
                    isInDark=true;
                    float falloff = _LightFalloff;
                    
                    // 圆形光源：径向渐变
                    if (!isRect)
                    {
                        float lightRadius = lightSize.x * 0.5;
                        float distanceToCenter = length(worldPos.xz - lightPos.xz);
                        falloff = 1.0 - smoothstep(lightRadius * _LightCircleFalloff, lightRadius, distanceToCenter);
                    }
                    // 矩形光源：双轴向渐变
                    else  
                    {
                        float halfWidth = lightSize.x * 0.5;
                        float halfHeight = lightSize.y * 0.5;
                        
                        // 使用矩形专用衰减参数
                        float xDist = saturate((halfWidth - abs(worldPos.x - lightPos.x)) / (halfWidth * _LightRectFalloff));
                        float zDist = saturate((halfHeight - abs(worldPos.z - lightPos.z)) / (halfHeight * _LightRectFalloff));
                        
                        // 使用相乘实现平滑的角落渐变
                        falloff = xDist * zDist;
                    }
                    
                    illumination = max(illumination, falloff * _MaxBrightness);
                }
                else
                {
                    // 黑暗区域处理（反向渐变）
                    float darknessFactor = _DarknessFactor;
                    
                    // 黑暗圆形处理
                    if (!isRect)
                    {
                        float lightRadius = lightSize.x * 0.5;
                        float distanceToCenter = length(worldPos.xz - lightPos.xz);
                        darknessFactor =smoothstep(0, lightRadius * _DarknessCircleFalloff, distanceToCenter);
                    }
                    // 黑暗矩形处理
                    else  
                    {
                        float halfWidth = lightSize.x * 0.5;
                        float halfHeight = lightSize.y * 0.5;
                        
                        // 计算基础距离
                        float xDist = (abs(worldPos.x - lightPos.x) - halfWidth * (1 - _DarknessRectFalloff)) / (halfWidth * _DarknessRectFalloff);
                        float zDist = (abs(worldPos.z - lightPos.z) - halfHeight * (1 - _DarknessRectFalloff)) / (halfHeight * _DarknessRectFalloff);
                        
                        // 添加范围限制：只在0.8-1.0范围内渐变
                        darknessFactor = smoothstep(0.8, 1.0, saturate(max(xDist, zDist)));
                    }
                    
                    // 应用黑暗因子（越靠近中心，darknessFactor越小，最终亮度越低）
                    illumination = lerp(illumination * _DarknessFactor, illumination, darknessFactor);
                }
            } 
            return saturate(illumination);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            
            // 计算光照影响（使用正确的世界坐标）
            float lit = IsIlluminated(IN.worldPos);
            
            // 混合明暗颜色（示例使用0.2作为基础亮度）
            o.Albedo = lerp(c.rgb * _MinBrightness, c.rgb * _MaxBrightness, lit);
            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
