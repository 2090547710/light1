Shader "Custom/LightingUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _OutlineTex ("Outline Texture", 2D) = "black" {}
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _MinBrightness ("Min Brightness", Range(0,1)) = 0.2
        _BrightnessMultiplier ("Brightness Multiplier", Range(0.1,3.0)) = 1.0
        // _PlayerLightRange ("玩家光照范围", Range(1.0, 20.0)) = 5.0
        // _PlayerLightIntensity ("玩家光照强度", Range(0.1, 2.0)) = 0.01
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 使用Unity包含文件获取基本功能
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
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _OutlineTex;
            float4 _OutlineTex_ST;
            fixed4 _OutlineColor;
            sampler2D _CompositeMap; // GPU中的RenderTexture
            uniform float4 _HeightmapParams;
            fixed4 _Color;
            half _MinBrightness;
            half _BrightnessMultiplier;
            uniform float3 _PlayerWorldPos; // 玩家世界坐标
            uniform float _PlayerLightRange; // 玩家光照范围
            uniform float _PlayerLightIntensity; // 玩家光照强度

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 计算高度图UV坐标
                float2 heightmapUV = (i.worldPos.xz - _HeightmapParams.xy + _HeightmapParams.zw*0.5) / _HeightmapParams.zw;
                heightmapUV = clamp(heightmapUV, 0, 1);
                
                // 从主纹理获取颜色
                fixed4 mainColor = tex2D(_MainTex, i.uv) * _Color;
                
                // 从边缘描线贴图获取颜色
                fixed4 outlineColor = tex2D(_OutlineTex, i.uv);
                
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
