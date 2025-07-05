Shader "Custom/EdgeOnlyOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.1)) = 0.02
    }
    
    SubShader
    {
        Tags {"Queue"="Geometry-10" "RenderType"="Opaque"}
        
        Pass
        {
            Name "OutlinePass"
            
            Cull Front
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            float4 _OutlineColor;
            float _OutlineWidth;
            
            v2f vert(appdata v)
            {
                v2f o;
                
                // 沿法線方向推出頂點
                float3 norm = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float3 clipNorm = mul((float3x3)UNITY_MATRIX_VP, norm);
                
                float2 offset = normalize(clipNorm.xy) * _OutlineWidth * clipPos.w;
                clipPos.xy += offset;
                
                o.pos = clipPos;
                return o;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}