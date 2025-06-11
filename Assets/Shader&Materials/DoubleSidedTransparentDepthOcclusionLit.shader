Shader "Custom/DoubleSidedTransparentDepthOcclusionLit"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color   ("Color Tint",    Color) = (1,1,1,0.5)
        _PaintTexture ("Paint Texture", 2D) = "clear" {}
        _PaintOpacity ("Paint Opacity", Range(0,1)) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "Lighting.cginc"

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
            fixed4 col = tex2D(_MainTex, i.uv) * _Color;
            
            fixed4 paintCol = tex2D(_PaintTexture, i.uv);
            
            fixed paintAlpha = paintCol.a * _PaintOpacity;
            col.rgb = lerp(col.rgb, paintCol.rgb, paintAlpha);
            col.a = max(col.a, paintAlpha * 0.5);

            fixed3 N = normalize(i.wnorm);
            fixed3 L = normalize(_WorldSpaceLightPos0.xyz);
            fixed  NdotL = max(0, dot(N, L));
            fixed3 diff = _LightColor0.rgb * NdotL;

            fixed3 amb = UNITY_LIGHTMODEL_AMBIENT.xyz;

            col.rgb *= (amb + diff);
            return col;
        }
        ENDCG

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
