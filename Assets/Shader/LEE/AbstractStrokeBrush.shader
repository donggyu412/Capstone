// AbstractStrokeBrush.shader
// ─────────────────────────────────────────────────────────────────────
// 추상화(다색 붓터치 겹침) 그림 전용 브러시.
//
// ── 특징 ────────────────────────────────────────────────────────────
//  · 붓털 분산 없음 — 하나의 굵고 부드러운 원형 브러시
//  · 끝부분이 둥근 원형 (round cap) — 곡선을 따라 이동해도 끝이 부드러움
//  · 높은 불투명도 — 위에 칠한 색이 아래 색을 거의 가림
//  · 가장자리만 살짝 부드럽게 블렌딩 (anti-aliasing 수준)
// ─────────────────────────────────────────────────────────────────────
Shader "Custom/AbstractStrokeBrush"
{
    Properties
    {
        _MainTex       ("Canvas Texture",  2D)              = "white" {}
        _BrushUV       ("Brush UV",        Vector)          = (0,0,0,0)
        _BrushColor    ("Brush Color",     Color)           = (1,0,0,1)
        _BrushSize     ("Brush Size",      Range(0.005, 0.15)) = 0.035
        _EdgeSoftness  ("Edge Softness",   Range(0.0, 0.3))  = 0.08
        _Opacity       ("Opacity",         Range(0.0, 1.0))  = 0.95
        _Pressure      ("Pressure",        Range(0, 1))      = 1.0
        _MinSizeScale  ("Min Size at 0 Pressure", Range(0.3, 1.0)) = 0.6
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
            float4    _BrushUV;
            float4    _BrushColor;
            float     _BrushSize;
            float     _EdgeSoftness;
            float     _Opacity;
            float     _Pressure;
            float     _MinSizeScale;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 canvasCol = tex2D(_MainTex, i.uv);

                // 필압 → 브러시 크기 (둥근 끝을 유지하면서 약간만 변화)
                float effectiveSize = _BrushSize * lerp(_MinSizeScale, 1.0, saturate(_Pressure));

                // 원형 마스크 (round cap)
                float dist = distance(i.uv, _BrushUV.xy);
                float edge = effectiveSize * _EdgeSoftness;
                float mask = 1.0 - smoothstep(effectiveSize - edge, effectiveSize, dist);

                // 높은 불투명도로 아래 색을 거의 가림
                float finalAlpha = mask * _Opacity * _BrushColor.a;

                return lerp(canvasCol, float4(_BrushColor.rgb, canvasCol.a), finalAlpha);
            }
            ENDCG
        }
    }
}
