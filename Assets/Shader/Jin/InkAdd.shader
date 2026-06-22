// InkAdd.shader
// ─────────────────────────────────────────────────────────────────────
// 브러시가 닿은 UV 위치에 잉크를 Gaussian 형태로 inkMap에 추가합니다.
//
// 입력: _MainTex = 현재 inkMap (RFloat, R채널 = 잉크 양)
// 출력: 잉크가 추가된 새 inkMap
//
// 사용법:
//   1. 이 셰이더로 머티리얼 생성 → CanvasPainter.inkAddMaterial에 연결
//   2. CanvasPainter에서 Graphics.Blit(inkMap, inkMapTemp, inkAddMaterial)
// ─────────────────────────────────────────────────────────────────────

Shader "Custom/InkAdd"
{
    Properties
    {
        _MainTex     ("Ink Map (RFloat)",      2D)    = "black" {}
        _BrushUV     ("Brush UV Position",     Vector) = (0.5, 0.5, 0, 0)
        _AddAmount   ("Add Ink Amount",        Float)  = 0.01
        _BrushRadius ("Brush Radius (UV 0~1)", Float)  = 0.02
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
            float4    _BrushUV;
            float     _AddAmount;
            float     _BrushRadius;

            fixed4 frag(v2f_img i) : SV_Target
            {
                // 현재 픽셀의 잉크 양
                float currentInk = tex2D(_MainTex, i.uv).r;

                // 브러시 중심까지의 거리 계산
                float2 delta = i.uv - _BrushUV.xy;
                float  dist  = length(delta);

                // ── Gaussian 가중치 ─────────────────────────────────
                // 브러시 중심 → 잉크 최대 추가
                // 가장자리로 갈수록 자연스럽게 감소
                // sigma = _BrushRadius / 2 로 설정 (반경 내에 자연스럽게 수렴)
                float sigma   = _BrushRadius * 0.5;
                float gaussian = exp(-(dist * dist) / (2.0 * sigma * sigma));

                // 추가량 = 전달받은 양 × Gaussian 가중치
                float inkToAdd = _AddAmount * gaussian;

                return float4(currentInk + inkToAdd, 0, 0, 1);
            }
            ENDCG
        }
    }
}
