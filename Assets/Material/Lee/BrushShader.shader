Shader "Custom/BrushShader"
{
    Properties
    {
        _MainTex ("Canvas Texture", 2D) = "white" {}
        _BrushColor ("Brush Color", Color) = (0, 0, 0, 1)
        // 획 시작점
        _StrokeStart ("Stroke Start UV", Vector) = (0.5, 0.5, 0, 0)
        // 획 끝점
        _StrokeEnd ("Stroke End UV", Vector) = (0.5, 0.5, 0, 0)
        // 획 굵기
        _StrokeWidth ("Stroke Width", Float) = 0.05
        _PaperHeight ("Paper Height", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BrushColor;
                float4 _StrokeStart;
                float4 _StrokeEnd;
                float  _StrokeWidth;
                float  _PaperHeight;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            // 점 p에서 선분 (a→b)까지의 최단 거리 계산
            float DistToSegment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float2 ap = p - a;
                float t = clamp(dot(ap, ab) / (dot(ab, ab) + 1e-6), 0.0, 1.0);
                float2 closest = a + t * ab;
                return length(p - closest);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 canvasColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                float dist = DistToSegment(IN.uv, _StrokeStart.xy, _StrokeEnd.xy);

                if (dist < _StrokeWidth)
                {
                    float edge = 1.0 - smoothstep(_StrokeWidth * 0.5, _StrokeWidth, dist);
                    float inkOpacity = edge * _PaperHeight;
                    return lerp(canvasColor, _BrushColor, inkOpacity);
                }

                return canvasColor;
            }
            ENDHLSL
        }
    }
}