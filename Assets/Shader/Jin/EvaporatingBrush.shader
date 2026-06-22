Shader "Custom/EvaporatingBrush"
{
    Properties
    {
        _MainTex ("Brush Texture", 2D) = "white" {}
        _BrushColor ("Brush Color", Color) = (1,1,1,1)
        _BrushUV ("Brush UV", Vector) = (0,0,0,0)
        _BrushSize ("Brush Size", Float) = 0.01
        _BrushHardness ("Brush Hardness", Float) = 0.5
    }

    SubShader
    {
        // UI나 캔버스에 그리기 적합한 투명(Transparent) 태그
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha // 배경과 부드럽게 섞이는 핵심 블렌딩 모드

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
            float4 _BrushColor;
            float4 _BrushUV;
            float _BrushSize;
            float _BrushHardness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 클릭한 좌표(_BrushUV)로부터 현재 픽셀까지의 거리
                float dist = distance(i.uv, _BrushUV.xy);
                
                // 중심은 진하고 테두리는 부드러워지는 브러시 효과 (smoothstep)
                float edge = _BrushSize * _BrushHardness;
                float alphaMask = smoothstep(_BrushSize, edge, dist);

                // 스크립트에서 넘겨준 색상(_BrushColor)에 마스크 알파값 적용
                fixed4 col = _BrushColor;
                col.a *= alphaMask;

                return col;
            }
            ENDCG
        }
    }
}