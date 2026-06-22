Shader "Custom/EraserShader"
{
    Properties
    {
        _MainTex       ("Canvas Texture",           2D)              = "white" {}
        _BrushUV       ("Brush UV",                 Vector)          = (0,0,0,0)

        // RT는 흰색(FFFFFF)으로 초기화되므로 지우개도 흰색으로 복원.
        // 화면에 보이는 아이보리(FFF5DC)는 CanvasMaterial Base Color가 담당.
        _EraseColor    ("Erase Color (RT value = white)", Color)      = (1,1,1,1)

        _BrushSize     ("Eraser Size",              Range(0.001,0.15)) = 0.03
        _BrushHardness ("Eraser Hardness",          Range(0,1))      = 0.6
        _Pressure      ("Pressure",                 Range(0,1))      = 1.0
        _MinSizeScale  ("Min Size at 0 Pressure",   Range(0.05,1.0)) = 0.3
        _PressureCurve ("Pressure Curve",           Range(0.5,3.0))  = 1.2
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
            float4 _BrushUV, _EraseColor;
            float  _BrushSize, _BrushHardness;
            float  _Pressure, _MinSizeScale, _PressureCurve;

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
                float dist = distance(i.uv, _BrushUV.xy);

                float p    = pow(saturate(_Pressure), _PressureCurve);
                float size = _BrushSize * lerp(_MinSizeScale, 1.0, p);

                float eraseMask = 1.0 - smoothstep(size * _BrushHardness, size, dist);

                // RT 흰색 값으로 복원 (화면 아이보리는 CanvasMaterial Base Color가 담당)
                return lerp(canvasCol, float4(_EraseColor.rgb, canvasCol.a), eraseMask);
            }
            ENDCG
        }
    }
}