Shader "Custom/LightingUnlitTransparent"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _OutlineTex ("Outline Texture", 2D) = "black" {}
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _MinBrightness ("Min Brightness", Range(0,1)) = 0.2
        _BrightnessMultiplier ("Brightness Multiplier", Range(0.1,3.0)) = 1.0
        
        // 摆动相关参数
        _SwayFrequency ("摆动频率", Range(0.1, 10.0)) = 1.0
        _SwayAmplitude ("摆动幅度", Range(0.0, 0.5)) = 0.02
        _SwaySpeed ("摆动速度", Range(0.1, 10.0)) = 1.0
        [Toggle] _HorizontalPlant ("横向植物", Float) = 0
        _SwayMask ("摆动遮罩 (顶部摆动多)", 2D) = "white" {}
        
        // 添加边缘采样控制
        [Toggle] _AvoidEdgeSampling ("避免边缘采样", Float) = 1
        
        // 玩家光照参数
        _PlayerLightRange ("玩家光照范围", Range(1.0, 20.0)) = 5.0
        _PlayerLightIntensity ("玩家光照强度", Range(0.1, 2.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        // 第一个Pass：只写入深度，不渲染颜色
        Pass
        {
            ZWrite On     // 开启深度写入
            ColorMask 0   // 不写入任何颜色通道
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 使用Unity包含文件获取基本功能
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _OutlineTex;
            float4 _OutlineTex_ST;
            sampler2D _SwayMask;
            fixed4 _OutlineColor;
            sampler2D _CompositeMap; // GPU中的RenderTexture
            uniform float4 _HeightmapParams;
            fixed4 _Color;
            half _MinBrightness;
            half _BrightnessMultiplier;
            
            // 摆动参数
            float _SwayFrequency;
            float _SwayAmplitude;
            float _SwaySpeed;
            float _HorizontalPlant;
            
            // 添加边缘采样控制变量
            float _AvoidEdgeSampling;

            v2f vert (appdata v)
            {
                v2f o;
                
                // 获取遮罩值，用于控制摆动强度（通常根据高度）
                float mask = tex2Dlod(_SwayMask, float4(v.uv, 0, 0)).r;
                
                // 计算时间相关的偏移
                float timeOffset = _Time.y * _SwaySpeed;
                
                // 计算摆动值（基于物体世界坐标的正弦波）
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float swayFactor = sin(worldPos.x * _SwayFrequency + timeOffset) * _SwayAmplitude * mask;
                
                // 根据植物方向应用摆动
                if (_HorizontalPlant > 0.5) {
                    // 横向植物（摆动垂直方向）
                    v.vertex.y += swayFactor;
                } else {
                    // 竖向植物（摆动水平方向）
                    v.vertex.x += swayFactor;
                }
                
                // 正常变换处理
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 根据开关决定是否避免采样边缘像素
                float2 safeUV = _AvoidEdgeSampling > 0.5 ? clamp(i.uv, 0.02, 0.98) : i.uv;
                
                // 采样主纹理获取alpha值
                fixed4 mainTex = tex2D(_MainTex, safeUV) * _Color;
                
                // 丢弃透明部分
                if(mainTex.a < 0.4)
                    discard;
                    
                // 简单返回白色（但由于ColorMask 0，颜色不会被写入）
                return fixed4(1,1,1,1);
            }
            ENDCG
        }
        
        // 第二个Pass：正常的透明渲染通道
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 使用Unity包含文件获取基本功能
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _OutlineTex;
            float4 _OutlineTex_ST;
            sampler2D _SwayMask;
            fixed4 _OutlineColor;
            sampler2D _CompositeMap; // GPU中的RenderTexture
            uniform float4 _HeightmapParams;
            fixed4 _Color;
            half _MinBrightness;
            half _BrightnessMultiplier;
            
            // 摆动参数
            float _SwayFrequency;
            float _SwayAmplitude;
            float _SwaySpeed;
            float _HorizontalPlant;
            
            // 添加边缘采样控制变量
            float _AvoidEdgeSampling;
            
            // 玩家光照参数
            uniform float3 _PlayerWorldPos; // 玩家世界坐标
            float _PlayerLightRange; // 玩家光照范围
            float _PlayerLightIntensity; // 玩家光照强度

            v2f vert (appdata v)
            {
                v2f o;
                
                // 获取遮罩值，用于控制摆动强度（通常根据高度）
                float mask = tex2Dlod(_SwayMask, float4(v.uv, 0, 0)).r;

                // 计算时间相关的偏移
                float timeOffset = _Time.y * _SwaySpeed;
                
                // 计算摆动值（基于物体世界坐标的正弦波）
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float swayFactor = sin(worldPos.x * _SwayFrequency + timeOffset) * _SwayAmplitude * mask;
                


                // 根据植物方向应用摆动
                if (_HorizontalPlant > 0.5) {
                    // 横向植物（摆动垂直方向）
                    v.vertex.y += swayFactor;
                } else {
                    // 竖向植物（摆动水平方向）
                    v.vertex.x += swayFactor;
                }
                
                // 正常变换处理
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 根据开关决定是否避免采样边缘像素
                float2 safeUV = _AvoidEdgeSampling > 0.5 ? clamp(i.uv, 0.02, 0.98) : i.uv;
                
                // 计算高度图UV坐标 (同样根据开关控制)
                float2 heightmapUV = (i.worldPos.xz - _HeightmapParams.xy + _HeightmapParams.zw*0.5) / _HeightmapParams.zw;
                heightmapUV = _AvoidEdgeSampling > 0.5 ? clamp(heightmapUV, 0.02, 0.98) : heightmapUV;
                
                // 从主纹理获取颜色
                fixed4 mainColor = tex2D(_MainTex, safeUV) * _Color;
                
                // 从边缘描线贴图获取颜色
                fixed4 outlineColor = tex2D(_OutlineTex, safeUV);
                
                // 从CompositeMap获取光照数据
                float4 lightData = tex2D(_CompositeMap, heightmapUV);
                float lightIntensity = lightData.r; // 使用红色通道存储的光照数据
                lightIntensity = saturate(lightIntensity); // 限制在0-1范围
                
                // 计算玩家光源对当前片元的影响
                float distToPlayer = distance(i.worldPos, _PlayerWorldPos);
                float playerLight = max(0, 1.0 - (distToPlayer / _PlayerLightRange)); 
                playerLight = pow(playerLight, 2.0) * _PlayerLightIntensity; // 平方衰减，更真实的点光源效果
                
                // 结合原有光照和玩家光源
                float combinedLight = saturate(lightIntensity + playerLight);
                
                // 应用亮度调整
                float adjustedIntensity = lerp(_MinBrightness, 1.0, combinedLight) * _BrightnessMultiplier;
                
                // 处理主贴图的光照
                fixed4 finalColor = mainColor;
                finalColor.rgb *= adjustedIntensity;
                
                // 应用边缘描线效果 (只有当不满足丢弃条件时)
                // 规则1: 透明度==0不要
                // 规则2: r>0.99f不要
                if(outlineColor.a > 0.001 && outlineColor.r <= 0.99)
                {
                    // 混合描线颜色
                    finalColor = lerp(finalColor, _OutlineColor, outlineColor.a);
                }
                
                return finalColor;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
