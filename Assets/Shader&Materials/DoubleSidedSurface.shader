Shader "Custom/DoubleSidedSurface"
{
    Properties
    {
        _MainTex     ("主貼圖 (Albedo)", 2D)   = "white" {}
        _Color       ("顏色 (Tint Color)", Color) = (1,1,1,1)
        _Metallic    ("金屬度 (Metallic)", Range(0,1))   = 0.5
        _Smoothness  ("光滑度 (Smoothness)", Range(0,1)) = 0.5
        _Occlusion   ("環境遮蔽 (Occlusion)", 2D)       = "white" {}
        _Emission    ("自發光 (Emission)", Color)      = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        // 關鍵：關閉面剔除 → 讓正反面都渲染
        Cull Off

        CGPROGRAM
        // 讓 Surface Shader 使用 Standard Lighting Model（包含金屬/光滑參數）
        #pragma surface surf Standard fullforwardshadows

        // 如果你需要支援透明／半透明，可改成：#pragma surface surf Standard alpha:fade

        sampler2D _MainTex;
        fixed4 _Color;
        half    _Metallic;
        half    _Smoothness;
        sampler2D _Occlusion;
        fixed4 _Emission;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_Occlusion;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Albedo × Tint
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo    = tex.rgb;
            o.Alpha     = tex.a;

            // Metallic / Smoothness
            o.Metallic  = _Metallic;
            o.Smoothness= _Smoothness;

            // 環境遮蔽
            fixed occ = tex2D(_Occlusion, IN.uv_Occlusion).r;
            o.Occlusion = occ;

            // 自發光
            o.Emission  = _Emission.rgb;
        }
        ENDCG
    }
    FallBack "Standard"
}
