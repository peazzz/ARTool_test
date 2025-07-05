Shader "Custom/DoubleSidedTransparentDepthOcclusionLit"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color   ("Color Tint",    Color) = (1,1,1,1)
        _PaintTexture ("Paint Texture", 2D) = "clear" {}
        _PaintOpacity ("Paint Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "AutoLight.cginc"

        sampler2D _MainTex;
        sampler2D _PaintTexture;
        fixed4   _Color;
        half     _PaintOpacity;

        struct appdata {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 uv     : TEXCOORD0;
        };
        
        struct v2f {
            float4 pos    : SV_POSITION;
            float3 wnorm  : TEXCOORD0;
            float2 uv     : TEXCOORD1;
            float3 worldPos : TEXCOORD2;
            UNITY_SHADOW_COORDS(3)
        };

        v2f vert(appdata v)
        {
            v2f o;
            o.pos   = UnityObjectToClipPos(v.vertex);
            o.uv    = v.uv;
            o.wnorm = UnityObjectToWorldNormal(v.normal);
            o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
            UNITY_TRANSFER_SHADOW(o, v.uv)
            return o;
        }

        fixed4 frag(v2f i) : SV_Target
        {
            // 完全模擬Legacy Diffuse的行為
            fixed4 col = tex2D(_MainTex, i.uv) * _Color;
            
            fixed4 paintCol = tex2D(_PaintTexture, i.uv);
            fixed paintAlpha = paintCol.a * _PaintOpacity;
            col.rgb = lerp(col.rgb, paintCol.rgb, paintAlpha);

            fixed3 N = normalize(i.wnorm);
            fixed3 L = normalize(_WorldSpaceLightPos0.xyz);
            
            // 標準Lambert漫反射
            fixed NdotL = max(0, dot(N, L));
            
            // Unity陰影衰減
            fixed shadow = UNITY_SHADOW_ATTENUATION(i, i.worldPos);
            
            // 最終光照（完全按照Legacy Diffuse的計算）
            fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;
            fixed3 diffuse = _LightColor0.rgb * NdotL * shadow;
            
            col.rgb = col.rgb * (ambient + diffuse);
            return col;
        }
        ENDCG

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }
            Cull Off  // 雙面渲染但使用單一Pass

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            ENDCG
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            CGPROGRAM
            #pragma vertex vert_shadow
            #pragma fragment frag_shadow
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            
            struct appdata_shadow {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f_shadow {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };
            
            v2f_shadow vert_shadow(appdata_shadow v)
            {
                v2f_shadow o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = v.uv;
                return o;
            }
            
            float4 frag_shadow(v2f_shadow i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
