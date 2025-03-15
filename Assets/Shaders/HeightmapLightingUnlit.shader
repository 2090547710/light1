Shader "Custom/HeightmapLightingUnlit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _MinBrightness ("Min Brightness", Range(0,1)) = 0.2
        _BrightnessMultiplier ("Brightness Multiplier", Range(0.1,3.0)) = 1.0
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
            sampler2D _CompositeMap; // GPU中的RenderTexture
            uniform float4 _HeightmapParams;
            fixed4 _Color;
            half _MinBrightness;
            half _BrightnessMultiplier;

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
                
                // 从CompositeMap获取光照数据
                float4 lightData = tex2D(_CompositeMap, heightmapUV);
                float lightIntensity = lightData.r; // 使用红色通道存储的光照数据
                lightIntensity = saturate(lightIntensity); // 限制在0-1范围
                
                // 应用亮度调整
                float adjustedIntensity = lerp(_MinBrightness, 1.0, lightIntensity) * _BrightnessMultiplier;
                
                // 计算最终颜色 - 不受Unity光照系统影响
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                col.rgb *= adjustedIntensity;
                
                return col;
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
