Shader "Custom/CustomIlluminationShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _MinBrightness ("Minimum Brightness", Range(0,1)) = 0.2
        _MaxBrightness ("Maximum Brightness", Range(0,2)) = 1.0
        _LightFalloff ("Light Falloff", Range(0.1,1)) = 0.8
        _DarknessFactor ("Darkness Factor", Range(0,1)) = 0.0
        
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
        float2 _LightSizes[1000];

        half _MinBrightness;
        half _MaxBrightness;
        half _LightFalloff;
        half _DarknessFactor;
        

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        // 在surf函数之前添加光照判断函数
        float IsIlluminated(float3 worldPos)
        {
            float illumination = 0.0;
            bool isInDark = false;

            for (int i = 0; i < _LightCount; i++)
            {
                // 处理矩形黑暗区域（y != 0）
                if (_LightSizes[i].y != 0) 
                {
            
                    float2 lightXZ = _LightPositions[i].xz;
                    float halfWidth = _LightSizes[i].x * 0.5;
                    float halfHeight = _LightSizes[i].y * 0.5;
                    
                    if (abs(worldPos.x - lightXZ.x) <= halfWidth && 
                        abs(worldPos.z - lightXZ.y) <= halfHeight) 
                    {
                        isInDark = true; // 标记在黑暗区域
                    }
                    continue;
                }

                // 处理圆形光明区域（y == 0）
                float3 lightPos = _LightPositions[i].xyz;
                float lightHeight = lightPos.y;
                float distanceToLight = length(worldPos.xz - lightPos.xz);
                float radius = _LightSizes[i].x;
                
                if(worldPos.y > lightHeight && isInDark) continue;
                
                float falloff = 1.0 - smoothstep(radius * _LightFalloff, radius, distanceToLight);
                if(falloff > 0) 
                {
                    isInDark = false; // 新增：当处于光明区域时强制取消黑暗标记
                    illumination = max(illumination, falloff * _MaxBrightness);
                }
            }
            
            // 最终亮度处理：如果在黑暗区域则衰减亮度
            if(isInDark) {
                illumination *= _DarknessFactor; // 使用0-1的系数控制黑暗区域亮度
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
