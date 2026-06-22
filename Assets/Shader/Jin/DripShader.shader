Shader "Custom/DripShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _DripSpeed ("Drip Speed", Range(0.0001, 0.01)) = 0.001 // 흐르는 속도/길이
        _Threshold ("Ink Threshold", Range(0, 1)) = 0.9 // 물감 인식 기준 (아이보리 배경 제외용)
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float _DripSpeed;
            float _Threshold;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 현재 픽셀 색상
                fixed4 currentCol = tex2D(_MainTex, i.uv);

                // 2. 바로 위쪽 픽셀의 색상을 가져옵니다 (y축으로 아주 살짝 위)
                // X축(U) 방향으로 가져오도록 수정 (오른쪽으로 흘렀으므로, 축을 바꿔줍니다)
                float2 upUV = i.uv + float2(_DripSpeed, 0); 

                // 만약 위로 역류한다면 부호를 반대로 바꿔주세요: float2(-_DripSpeed, 0);
                fixed4 upCol = tex2D(_MainTex, upUV);

                // 3. 밝기를 계산합니다. 
                // (배경색이 아이보리색이므로, 밝기가 일정 수치보다 낮으면 물감으로 간주)
                float upBrightness = Luminance(upCol.rgb);

                // 4. 위쪽 픽셀에 물감이 묻어있다면?
                if (upBrightness < _Threshold)
                {
                    // 위쪽 물감 색상을 현재 픽셀에 10%씩 부드럽게 섞어서 흘러내리게 만듭니다.
                    currentCol = lerp(currentCol, upCol, 0.1);
                }

                return currentCol;
            }
            ENDCG
        }
    }
}