// The Circulation level over the whole frame (CirculationLevel), after the Renai Circulation video: anything
// against the white gets a soft pale-blue copy of itself offset to one side and a pink one to the other (the
// video's doubled silhouettes), and the whole thing's lifted a little towards pastel.
Shader "3daigamesolo/Post/Pastel"
{
    Properties
    {
        _MainTex ("Frame", 2D) = "white" {}
        _Offset ("Shadow offset (screen)", Vector) = (0.012, -0.008, 0, 0)
        _Blue ("Blue shadow", Color) = (0.74, 0.84, 1, 1)
        _Pink ("Pink shadow", Color) = (1, 0.82, 0.88, 1)
        _Lift ("Pastel lift", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Offset, _Blue, _Pink;
            float _Lift;

            // how much a pixel isn't the white background
            float ink(float3 c) { return saturate((1 - min(min(c.r, c.g), c.b)) * 3); }

            float4 frag(v2f_img i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv).rgb;
                float here = ink(col);
                float blue = ink(tex2D(_MainTex, i.uv - _Offset.xy).rgb) * (1 - here);
                float pink = ink(tex2D(_MainTex, i.uv + _Offset.xy * 0.6).rgb) * (1 - here) * (1 - blue);
                col = lerp(col, _Blue.rgb, blue * 0.85);
                col = lerp(col, _Pink.rgb, pink * 0.6);
                col = lerp(col, float3(1, 0.96, 0.98), _Lift);
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
