// InkDrip.shader
// ─────────────────────────────────────────────────────────────────────
// inkMap을 읽어 흘러내리는 잉크를 캔버스에 시각적으로 합성합니다.
//
// ── 동작 원리 ────────────────────────────────────────────────────────
//  · inkMap의 R값이 _DripThreshold 초과분만 캔버스에 반영
//  · 반영량은 매우 작음 (프레임당 소량) → 여러 프레임에 걸쳐 서서히 착색
//  · canvasRenderTexture에 영구적으로 덧씌워짐 (잉크 자국이 남음)
//  · 잉크 농도가 높을수록 더 진하게 착색 (요구사항 4)
// ─────────────────────────────────────────────────────────────────────

Shader "Custom/InkDrip"
{
    Properties
    {
        _MainTex       ("Canvas Texture",     2D)    = "white" {}
        _InkMap        ("Ink Map (RFloat)",   2D)    = "black" {}
        _InkColor      ("Ink Color",          Color) = (0.08, 0.12, 0.75, 1.0)
        _DripThreshold ("Drip Threshold",     Float) = 0.5

        _StainRate     ("Stain Rate",         Float) = 0.015

        _MaxOpacity    ("Max Ink Opacity",    Range(0,1)) = 0.92
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _InkMap;
            float4    _InkColor;
            float     _DripThreshold;
            float     _StainRate;
            float     _MaxOpacity;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float4 canvas    = tex2D(_MainTex, i.uv);
                float  inkAmount = tex2D(_InkMap,  i.uv).r;

                float excessInk     = max(0.0, inkAmount - _DripThreshold);
                float stainStrength = saturate(excessInk * _StainRate) * _MaxOpacity;
                float blendFactor   = stainStrength * _InkColor.a;

                return lerp(canvas, float4(_InkColor.rgb, canvas.a), blendFactor);
            }
            ENDCG
        }
    }
}