Shader "Custom/DoubleSidedTransparentDepthOcclusionLit"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color   ("Color Tint",    Color) = (1,1,1,0.5)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "Lighting.cginc"

        sampler2D _MainTex;
        fixed4   _Color;

        struct appdata {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 uv     : TEXCOORD0;
        };
        struct v2f {
            float4 pos    : SV_POSITION;
            float3 wnorm  : TEXCOORD0;
            float2 uv     : TEXCOORD1;
        };

        v2f vert(appdata v)
        {
            v2f o;
            o.pos   = UnityObjectToClipPos(v.vertex);
            o.uv    = v.uv;
            o.wnorm = UnityObjectToWorldNormal(v.normal);
            return o;
        }

        fixed4 frag(v2f i) : SV_Target
        {
            // 預乘顏色與透明度
            fixed4 col = tex2D(_MainTex, i.uv) * _Color;

            // 漫反射光
            fixed3 N = normalize(i.wnorm);
            fixed3 L = normalize(_WorldSpaceLightPos0.xyz);
            fixed  NdotL = max(0, dot(N, L));
            fixed3 diff = _LightColor0.rgb * NdotL;

            // 改用 Unity 全域環境光
            fixed3 amb = UNITY_LIGHTMODEL_AMBIENT.xyz;

            // 組合
            col.rgb *= (amb + diff);
            return col;
        }
        ENDCG

        // Pass 1：背面 (不寫深度)
        Pass
        {
            Name "INTERNAL"
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }

        // Pass 2：正面 (寫深度)
        Pass
        {
            Name "EXTERNAL"
            Cull Back
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
