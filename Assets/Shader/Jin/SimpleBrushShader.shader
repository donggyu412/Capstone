Shader "Custom/SimpleBrushShader"
{
    Properties
    {
        _MainTex       ("Texture",                    2D)              = "white" {}
        _BrushUV       ("Brush UV",                   Vector)          = (0,0,0,0)
        _BrushColor    ("Brush Color",                Color)           = (0, 0, 0, 1)
        _BrushSize     ("Brush Size",                 Range(0.001, 0.1)) = 0.01
        _BrushHardness ("Brush Hardness",             Range(0,1))      = 0.8
        _Pressure      ("Pressure",                   Range(0, 1))     = 1.0
        _MinSizeScale  ("Min Size at 0 Pressure",     Range(0.01, 0.5)) = 0.05
        _MinAlphaScale ("Min Alpha at 0 Pressure",    Range(0.0,  1.0)) = 0.15
        _PressureCurve ("Pressure Curve (1=linear)",  Range(0.5,  3.0)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        ZWrite Off ZTest Always Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            float4 _BrushUV, _BrushColor;
            float  _BrushSize, _BrushHardness;
            float  _Pressure, _MinSizeScale, _MinAlphaScale, _PressureCurve;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col  = tex2D(_MainTex, i.uv);
                float  dist = distance(i.uv, _BrushUV.xy);

                // ── 필압 곡선 보정 ──────────────────────────────────
                // PressureCurve > 1 : 약한 필압 구간은 가늘고,
                //                     세게 눌러야 급격히 두꺼워짐
                //                     → 타블렛 필압의 비선형 특성과 유사
                float p = pow(saturate(_Pressure), _PressureCurve);

                // ── 필압 → 크기 / 농도 변환 ────────────────────────
                // MinSizeScale=0.05 : 필압 0 → 최대 크기의 5% (매우 가는 선)
                // MinSizeScale=0.3  : 필압 0 → 최대 크기의 30% (완만한 변화)
                float size  = _BrushSize   * lerp(_MinSizeScale,  1.0, p);
                float alpha = _BrushColor.a * lerp(_MinAlphaScale, 1.0, p);

                float paint = 1.0 - smoothstep(size * _BrushHardness, size, dist);
                return lerp(col, _BrushColor, paint * alpha);
            }
            ENDCG
        }
    }
}
