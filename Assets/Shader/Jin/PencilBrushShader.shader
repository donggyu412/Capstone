Shader "Custom/PencilBrushShader"
{
    Properties
    {
        _MainTex       ("Canvas Texture",  2D)          = "white" {}
        _BrushTex      ("Pencil Texture",  2D)          = "white" {}
        _BrushUV       ("Brush UV",        Vector)       = (0,0,0,0)
        _BrushColor    ("Brush Color",     Color)        = (0,0,0,1)
        _BrushSize     ("Brush Size",      Range(0.001, 0.1)) = 0.01
        _BrushHardness ("Brush Hardness",  Range(0,1))  = 0.5

        // ── 필압 (RobotBrush / MousePainter에서 SetFloat로 전달) ──
        _Pressure      ("Pressure",        Range(0,1))  = 1.0
        _MinSizeScale  ("Min Size at 0 Pressure", Range(0.05, 1.0)) = 0.2
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
            sampler2D _BrushTex;
            float4    _BrushUV;
            float4    _BrushColor;
            float     _BrushSize;
            float     _BrushHardness;
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
                // 1. 기존 도화지 색상
                fixed4 canvasCol = tex2D(_MainTex, i.uv);

                // ── 필압 → 브러시 크기 변환 ────────────────────────
                // 연필은 크기(두께)만 변하고 농도(alpha)는 항상 일정
                float effectiveSize = _BrushSize * lerp(_MinSizeScale, 1.0, _Pressure);

                // 2. 브러시 영역 안 위치 계산 (0~1로 변환)
                float2 brushUV = (i.uv - _BrushUV.xy) / effectiveSize + 0.5;

                float paintFactor = 0;
                if (brushUV.x >= 0 && brushUV.x <= 1 &&
                    brushUV.y >= 0 && brushUV.y <= 1)
                {
                    // 3. 연필 텍스처 알파값으로 입자 느낌 표현
                    paintFactor = tex2D(_BrushTex, brushUV).a;

                    // 4. 외곽 부드럽게
                    float dist = distance(i.uv, _BrushUV.xy);
                    float edge = 1.0 - smoothstep(effectiveSize * _BrushHardness, effectiveSize, dist);
                    paintFactor *= edge;
                }

                // 5. 도화지 색과 혼합 (연필은 농도 고정 0.4)
                return lerp(canvasCol, _BrushColor, paintFactor * _BrushColor.a * 0.4);
            }
            ENDCG
        }
    }
}
