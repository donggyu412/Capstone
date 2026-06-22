Shader "Custom/BlurBrushShader"
{
    Properties
    {
        _MainTex ("Canvas Texture", 2D) = "white" {}
        _BlurSize ("Blur Spread", Range(0.001, 0.05)) = 0.005
        
        // CanvasPainter와 연동하기 위해 기존 브러시와 동일한 프로퍼티 추가
        _BrushUV ("Brush UV", Vector) = (0,0,0,0)
        _BrushSize ("Brush Size", Float) = 0.01
        _BrushHardness ("Brush Hardness", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _BlurSize;
            float4 _BrushUV;
            float _BrushSize;
            float _BrushHardness;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 현재 픽셀이 마우스 중심(Brush UV)에서 얼마나 떨어져 있는지 거리 계산
                float dist = distance(i.uv, _BrushUV.xy);

                // 2. 브러시 크기 반경을 벗어난 픽셀은 처리하지 않음 (투명 영역으로 버림)
                if (dist > _BrushSize) discard;

                // 3. 브러시 테두리를 부드럽게 만들기 위한 알파(투명도) 계산
                float alpha = 1.0 - smoothstep(_BrushSize * _BrushHardness, _BrushSize, dist);

                // 4. 주변 9방향 픽셀 샘플링 (흐림 효과 연산)
                float2 offset = float2(_BlurSize, _BlurSize);
                fixed4 col = fixed4(0,0,0,0);

                col += tex2D(_MainTex, i.uv + float2(-offset.x, -offset.y));
                col += tex2D(_MainTex, i.uv + float2(0, -offset.y));
                col += tex2D(_MainTex, i.uv + float2(offset.x, -offset.y));

                col += tex2D(_MainTex, i.uv + float2(-offset.x, 0));
                col += tex2D(_MainTex, i.uv);
                col += tex2D(_MainTex, i.uv + float2(offset.x, 0));

                col += tex2D(_MainTex, i.uv + float2(-offset.x, offset.y));
                col += tex2D(_MainTex, i.uv + float2(0, offset.y));
                col += tex2D(_MainTex, i.uv + float2(offset.x, offset.y));

                col /= 9.0; // 픽셀 평균값

                // 5. 최종 흐려진 색상에 원형 브러시 알파 모양을 씌움
                col.a *= alpha;
                
                return col;
            }
            ENDCG
        }
    }
}