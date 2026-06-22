Shader "Custom/SplatBrush"
{
    Properties
    {
        _MainTex ("Brush Shape (Alpha)", 2D) = "white" {}
        // C# 스크립트에서 _Color 프로퍼티에 접근해 팔레트 색상을 전달합니다.
        _Color ("Paint Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        // 배경을 가리지 않고 자연스럽게 겹치도록 투명 블렌딩 설정
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha 
        ZWrite Off

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
            float4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 브러시 텍스처의 알파값(모양)만 추출합니다.
                fixed4 texColor = tex2D(_MainTex, i.uv);
                
                // [핵심] C#에서 받은 색상(_Color.rgb)과 텍스처의 형태(texColor.a)를 결합합니다.
                // 결과는 투명한 배경 위에 팔레트 색상으로 된 스플래터 모양이 됩니다.
                return fixed4(_Color.rgb, texColor.a * _Color.a);
            }
            ENDCG
        }
    }
}