// InkFlow.shader
// ─────────────────────────────────────────────────────────────────────
// 매 프레임 inkMap의 잉크를 중력 방향으로 흘립니다.
//
// ── 물리 원리 ────────────────────────────────────────────────────────
//  · 임계값(_DripThreshold) 초과분만 흐름
//    → 소량의 잉크는 캔버스에 고정, 과잉 잉크만 흘러내림
//  · 흐르는 양 = 초과분 × 흐름 속도 (요구사항 4: 양에 비례)
//  · 업스트림(중력 반대) 픽셀의 초과 잉크가 현재 픽셀로 들어옴
//  · 현재 픽셀의 초과 잉크는 다운스트림(중력 방향) 픽셀로 나감
//
// ── 중력 UV 좌표 변환 (C# 측) ─────────────────────────────────────
//  C#의 CanvasPainter.SimulateInkFlow()에서:
//    uvGravity.x = Dot(worldGravity, transform.right)  ← U축 성분
//    uvGravity.y = Dot(worldGravity, transform.up)     ← V축 성분
//  → 캔버스가 어느 방향이든 실세계 중력 방향으로 흐름
// ─────────────────────────────────────────────────────────────────────

Shader "Custom/InkFlow"
{
    Properties
    {
        _MainTex       ("Ink Map (RFloat)",            2D)     = "black" {}
        _GravityUV     ("Gravity Dir in UV Space",     Vector)  = (0, -1, 0, 0)
        _DripThreshold ("Drip Threshold",              Float)   = 0.5
        _FlowSpeed     ("Base Flow Speed (per frame)", Float)   = 0.005
        _VelocityScale ("Velocity Scale (잉크量 비례)", Float)   = 3.0
        _DryRate       ("Ink Dry Rate (per frame)",   Float)   = 0.001
        _TexelSize     ("Texel Size (xy) & Res (zw)",  Vector)  = (0.001, 0.001, 1024, 1024)
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
            float4    _GravityUV;
            float     _DripThreshold;
            float     _FlowSpeed;
            float     _VelocityScale;
            float     _DryRate;
            float4    _TexelSize;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float inkHere = tex2D(_MainTex, i.uv).r;

                // ── 중력 방향 (UV 공간, 정규화됨) ─────────────────
                float2 gravDir = normalize(_GravityUV.xy);

                // ── 업스트림 UV ────────────────────────────────────
                // 업스트림 = 중력 반대 방향 → 잉크가 흘러오는 곳
                // 텍셀 1개 단위로 샘플링 (픽셀 단위 이동)
                float2 upstreamUV = i.uv - gravDir * _TexelSize.xy;

                // 업스트림이 캔버스 밖이면 유입 없음
                bool upstreamValid = all(upstreamUV >= float2(0, 0))
                                  && all(upstreamUV <= float2(1, 1));
                float inkUpstream  = upstreamValid ? tex2D(_MainTex, upstreamUV).r : 0.0;

                // ── 임계값 초과분 계산 ─────────────────────────────
                // 임계값 이하는 흐르지 않음 → 일부 잉크는 캔버스에 "고정"
                // 임계값 초과분만 유동적으로 흐름
                float excessHere     = max(0.0, inkHere     - _DripThreshold);
                float excessUpstream = max(0.0, inkUpstream - _DripThreshold);

                // ── 잉크 흐름 계산 ─────────────────────────────────
                // 잉크 양에 비례해 속도가 빨라짐 (현실: 잉크가 많을수록 압력 증가)
                //
                // velocity = baseSpeed × (1 + excessInk × VelocityScale)
                //   excess=0.0 → velocity = baseSpeed           (기본 속도)
                //   excess=0.5 → velocity = baseSpeed × 2.5     (2.5배)
                //   excess=1.0 → velocity = baseSpeed × 4.0     (4배)
                //   excess=2.0 → velocity = baseSpeed × 7.0     (7배)
                //
                // flowOut = excessInk × velocity
                float velocityHere     = _FlowSpeed * (1.0 + excessHere     * _VelocityScale);
                float velocityUpstream = _FlowSpeed * (1.0 + excessUpstream * _VelocityScale);

                float flowOut = excessHere     * velocityHere;
                float flowIn  = excessUpstream * velocityUpstream;

                // ── 자연 건조 ──────────────────────────────────────
                // 잉크가 서서히 마름 (0이면 영원히 안 마름)
                float dried = inkHere * _DryRate;

                // ── 최종 잉크 양 ───────────────────────────────────
                float newInk = inkHere - flowOut + flowIn - dried;

                return float4(max(0.0, newInk), 0, 0, 1);
            }
            ENDCG
        }
    }
}
