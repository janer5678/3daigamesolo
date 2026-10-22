// The Infinity level's look over the whole frame (InfinityFX), after the fractal fly-through video: the frame
// is pulled out from the middle in streaks (zoom blur), colour fringed, sharpened and embossed till it's
// crunchy, then mapped onto a palette that keeps cycling (black, a strong colour, a pale one), blacks crushed;
// and now and then lines tear sideways and flash with thin rainbow scratches.
Shader "3daigamesolo/Post/Infinity"
{
    Properties
    {
        _MainTex ("Frame", 2D) = "black" {}
        _Mix    ("Palette mix", Range(0, 1)) = 0.6
        _Cycle  ("Palette turns per second", Float) = 0.035
        _Emboss ("Emboss", Range(0, 3)) = 1.4
        _Streak ("Zoom streaks", Float) = 0.015
        _Fringe ("Colour fringing", Float) = 0.003
        _Glitch ("Glitch lines", Float) = 0.6
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
            float4 _MainTex_TexelSize;
            float _Mix, _Cycle, _Emboss, _Streak, _Fringe, _Glitch;

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float3 hsv(float h, float s, float v) { float3 k = saturate(abs(frac(h + float3(0, 2, 1) / 3.0) * 6 - 3) - 1); return v * lerp(1, k, s); }
            float luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

            // the frame pulled out from the middle: several samples further and further in, each colour a little
            // differently (the fringe)
            float3 streaked(float2 uv)
            {
                float2 d = uv - 0.5;
                float3 acc = 0;
                const int N = 8;
                for (int k = 0; k < N; k++)
                {
                    float s = 1 - _Streak * k / N;
                    acc.r += tex2D(_MainTex, 0.5 + d * s * (1 + _Fringe)).r;
                    acc.g += tex2D(_MainTex, 0.5 + d * s).g;
                    acc.b += tex2D(_MainTex, 0.5 + d * s * (1 - _Fringe)).b;
                }
                return acc / N;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                // torn lines: a few bands of the frame jump sideways for a moment
                float band = floor(uv.y * 110), tick = floor(t * 14);
                float tear = step(1 - 0.012 * _Glitch, hash(float2(band, tick)));
                uv.x += tear * (hash(float2(band + 7, tick)) - 0.5) * 0.12;

                float3 col = streaked(uv);
                // crunchy: sharpened hard, and embossed (lit from the top left)
                float2 px = _MainTex_TexelSize.xy * 1.5;
                float3 nb = (tex2D(_MainTex, uv + float2(px.x, 0)).rgb + tex2D(_MainTex, uv - float2(px.x, 0)).rgb
                           + tex2D(_MainTex, uv + float2(0, px.y)).rgb + tex2D(_MainTex, uv - float2(0, px.y)).rgb) * 0.25;
                col += (col - nb) * 1.6;
                float relief = luma(tex2D(_MainTex, uv - px).rgb) - luma(tex2D(_MainTex, uv + px).rgb);
                col += relief * _Emboss;

                // onto the cycling palette: black, then a strong colour, then a pale one beside it on the wheel
                float h = frac(t * _Cycle);
                float l = saturate(luma(col));
                float3 strong = hsv(h, 1, 1), pale = hsv(h + 0.12, 0.35, 1);
                float3 mapped = lerp(0, strong, smoothstep(0.03, 0.3, l));
                mapped = lerp(mapped, pale, smoothstep(0.5, 0.9, l));
                // and the colours as they were, pushed: more saturated
                float3 pushed = lerp(luma(col), col, 1.8);
                col = lerp(pushed, mapped, _Mix);
                // a little posterised, blacks crushed, contrast up
                col = lerp(col, floor(col * 6 + 0.5) / 6, 0.35);
                col = saturate((col - 0.05) * 1.25);

                // thin rainbow scratches across now and then
                float scratch = step(1 - 0.006 * _Glitch, hash(float2(band + 31, floor(t * 9))));
                float thin = step(frac(uv.y * 110), 0.35);
                col = lerp(col, hsv(uv.x * 2 + t, 0.9, 1), scratch * thin * 0.85);

                // darker round the edges
                float2 d = i.uv - 0.5;
                col *= 1 - dot(d, d) * 0.9;
                return float4(col, 1);
            }
            ENDCG
        }
    }
}
