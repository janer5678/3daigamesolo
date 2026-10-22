// The Sketchbook level over the whole frame (SketchFX): the lines boil (the frame wobbles a hair, a few times a
// second, like animation drawn by hand), paper grain and a soft dark edge like a page under a lamp.
Shader "3daigamesolo/Post/Sketch"
{
    Properties
    {
        _MainTex ("Frame", 2D) = "white" {}
        _Boil ("Line boil", Float) = 0.0012
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
            float _Boil;

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float tick = floor(_Time.y * 8);
                float2 uv = i.uv + (float2(noise(i.uv * 30 + tick), noise(i.uv * 30 + tick + 17)) - 0.5) * _Boil * 2;
                float3 col = tex2D(_MainTex, uv).rgb;
                float grain = 0.94 + 0.06 * noise(i.uv * float2(900, 500));
                col *= grain;
                float2 d = i.uv - 0.5;
                col *= 1 - dot(d, d) * 0.45;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
