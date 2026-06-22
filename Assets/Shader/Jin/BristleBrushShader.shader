Shader "Custom/BristleBrushShader"
{
    Properties
    {
        _MainTex        ("Canvas Texture",          2D)              = "white" {}
        _BrushUV        ("Brush UV",                Vector)          = (0,0,0,0)
        _BrushColor     ("Brush Color",             Color)           = (0,0,0,1)
        _BrushSize      ("Brush Size",              Range(0.001,0.1)) = 0.02
        _Pressure       ("Pressure",                Range(0,1))      = 1.0
        _MinSizeScale   ("Min Size at 0 Pressure",  Range(0.01,0.5)) = 0.05
        _MinAlphaScale  ("Min Alpha at 0 Pressure", Range(0.0,1.0))  = 0.15
        _PressureCurve  ("Pressure Curve",          Range(0.5,3.0))  = 1.5
        _BristleCount   ("Bristle Count",           Range(4,24))     = 16
        _BristleSize    ("Bristle Tip Size",        Range(0.0005,0.015)) = 0.003
        _BristleSpread  ("Bristle Spread",          Range(0.2,1.0))  = 0.85
        _BristleJitter  ("Bristle Jitter",          Range(0.0,0.5))  = 0.15
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

            #define MAX_BRISTLES 24

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            sampler2D _MainTex;
            float4 _BrushUV, _BrushColor;
            float  _BrushSize, _Pressure;
            float  _MinSizeScale, _MinAlphaScale, _PressureCurve;
            float  _BristleCount, _BristleSize, _BristleSpread, _BristleJitter;

            // 해시 함수 (붓털 지터에 사용)
            float hash1(float n)  { return frac(sin(n) * 43758.5453); }
            float2 hash2(float n) { return float2(hash1(n), hash1(n * 1.7319)); }

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

                // 필압 곡선
                float p = pow(saturate(_Pressure), _PressureCurve);

                // 접촉 반경: 필압에 따라 붓이 캔버스에 눌리는 넓이
                // 낮은 필압 = 중심 붓털만 닿음(가는선), 높은 필압 = 전체 닿음(두꺼운선)
                float contactRadius = _BrushSize * lerp(_MinSizeScale, 1.0, p);

                // 전체 알파 (필압에 따른 잉크 농도)
                float globalAlpha = _BrushColor.a * lerp(_MinAlphaScale, 1.0, p);

                // 브러시 영역 밖이면 스킵 (성능 최적화)
                float2 brushCenter = _BrushUV.xy;
                float distToBrush = distance(i.uv, brushCenter);
                if (distToBrush > _BrushSize + _BristleSize)
                    return canvasCol;

                // 붓털 계산 (Sunflower 골든각 배열)
                // angle = i * 2.399448 (골든각 라디안)
                // r = sqrt((i+0.5)/N)  => 반지름 0~1 균일 분포
                float totalPaint = 0;
                int n = (int)clamp(_BristleCount, 1, MAX_BRISTLES);

                for (int b = 0; b < MAX_BRISTLES; b++)
                {
                    if (b >= n) break;

                    float angle = b * 2.399448;
                    float r     = sqrt((b + 0.5) / (float)n);

                    float2 baseOffset = float2(cos(angle), sin(angle))
                                      * r * _BrushSize * _BristleSpread;

                    // 작은 지터로 자연스러움 추가
                    float2 jitter = (hash2((float)b * 0.3137 + 0.1) * 2.0 - 1.0)
                                  * _BristleJitter * _BristleSize * 3.0;

                    float2 bristlePos = brushCenter + baseOffset + jitter;

                    // 접촉 판별: 브러시 중심으로부터 거리가 contactRadius 이내면 닿음
                    float bristleDistFromCenter = length(baseOffset);
                    if (bristleDistFromCenter > contactRadius) continue;

                    // 닿은 붓털의 원형 마스크
                    float bristleDist = distance(i.uv, bristlePos);
                    float bristleMask = 1.0 - smoothstep(
                        _BristleSize * 0.4,
                        _BristleSize,
                        bristleDist
                    );
                    totalPaint = max(totalPaint, bristleMask);
                }

                return lerp(canvasCol, _BrushColor, totalPaint * globalAlpha);
            }
            ENDCG
        }
    }
}
