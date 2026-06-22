Shader "Custom/InkEraseShader"
{
    // ─────────────────────────────────────────────────────────────────
    // InkMap 지우기 셰이더
    //
    // 지우개 위치의 inkMap 값을 0으로 만들어
    // 잉크 흘러내림 효과가 지워진 자리에서 계속 나타나지 않도록 함.
    // CanvasPainter.EraseInkAtUV()에서 Graphics.Blit으로 사용.
    // ─────────────────────────────────────────────────────────────────

    Properties
    {
        _MainTex     ("Ink Map (RFloat)", 2D)    = "black" {}
        _BrushUV     ("Brush UV",         Vector) = (0,0,0,0)
        _EraseRadius ("Erase Radius",     Float)  = 0.03
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
            float     _EraseRadius;

            float4 frag(v2f_img i) : SV_Target
            {
                float inkHere = tex2D(_MainTex, i.uv).r;
                float dist    = distance(i.uv, _BrushUV.xy);

                // 지우개 반경 안은 0으로, 바깥은 원래값 유지
                // smoothstep으로 경계를 부드럽게 처리
                float keepFactor = smoothstep(_EraseRadius * 0.5, _EraseRadius, dist);
                return float4(inkHere * keepFactor, 0, 0, 1);
            }
            ENDCG
        }
    }
}
