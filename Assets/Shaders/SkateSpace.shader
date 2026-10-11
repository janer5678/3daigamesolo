// Deep space: a navy-black sky with drifting blue and violet nebula wisps, a dense field of tiny
// stars, and a few big ones with four-point glints. Drawn on the inside of a big sphere before
// anything else, like the day sky, and never writes depth.
Shader "3daigamesolo/World/Space"
{
    Properties
    {
        _Deep      ("Deep space", Color)     = (0.010, 0.012, 0.045, 1)
        _Mid       ("Glow", Color)           = (0.040, 0.060, 0.220, 1)
        _NebulaA   ("Nebula blue", Color)    = (0.180, 0.300, 0.950, 1)
        _NebulaB   ("Nebula violet", Color)  = (0.680, 0.320, 0.900, 1)
        _Stars     ("Star density", Range(0, 1)) = 0.55
        _BigStars  ("Big star density", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" }
        Cull Front
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Deep, _Mid, _NebulaA, _NebulaB;
            float _Stars, _BigStars;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float3 hash33(float3 p)
            {
                return float3(hash3(p), hash3(p + 19.19), hash3(p + 47.47));
            }

            float noise3(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(hash3(i), hash3(i + float3(1, 0, 0)), f.x),
                                 lerp(hash3(i + float3(0, 1, 0)), hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash3(i + float3(0, 0, 1)), hash3(i + float3(1, 0, 1)), f.x),
                                 lerp(hash3(i + float3(0, 1, 1)), hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float fbm3(float3 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += a * noise3(p); p = p * 2.07 + 13.7; a *= 0.5; }
                return s;
            }

            // stars on a grid of cells over the sky; each cell may hold one, somewhere inside it
            float starField(float3 d, float scale, float chance, float size)
            {
                float3 q = d * scale;
                float3 cell = floor(q);
                float h = hash3(cell);
                if (h < 1 - chance) return 0;
                float3 at = normalize((cell + 0.2 + 0.6 * hash33(cell)) / scale);
                float dist = length(d - at) * scale;
                float tw = 0.65 + 0.35 * sin(_Time.y * (1.5 + 3 * h) + h * 50);
                return smoothstep(size, 0, dist) * tw;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.posWS - _WorldSpaceCameraPos);

                // nebula: a broad glow, blue clouds, and thin violet wisps along their edges
                float n = fbm3(d * 2.2 + float3(0, _Time.y * 0.004, 0));
                float wisp = 1 - abs(2 * fbm3(d * 3.4 + 7.3) - 1);
                wisp = pow(saturate(wisp), 6);
                float region = smoothstep(0.42, 0.72, fbm3(d * 1.2 + 21.0));
                float3 col = lerp(_Deep.rgb, _Mid.rgb, smoothstep(0.35, 0.8, n));
                col += _NebulaA.rgb * smoothstep(0.5, 0.85, n) * region * 0.55;
                col += _NebulaB.rgb * wisp * region * 0.7;
                col += _NebulaA.rgb * wisp * (1 - region) * 0.12;

                // stars: lots of tiny ones, fewer bright ones
                float s = starField(d, 260, 0.08 * _Stars, 0.32) * 0.9;
                s += starField(d, 120, 0.05 * _Stars, 0.28);
                col += s * float3(1.0, 0.95, 0.92);

                // a handful of big stars with four-point glints
                float3 q = d * 22;
                float3 cell = floor(q);
                if (hash3(cell + 3.3) > 1 - 0.18 * _BigStars)
                {
                    float3 at = normalize((cell + 0.3 + 0.4 * hash33(cell + 9.1)) / 22);
                    float3 t = normalize(cross(at, float3(0, 1, 0.001)));
                    float3 b = cross(at, t);
                    float3 off = d - at;
                    float x = dot(off, t) * 22, y = dot(off, b) * 22;
                    float core = 0.0009 / (x * x + y * y + 0.0009);
                    float spikes = exp(-abs(y) * 140) * exp(-abs(x) * 9) + exp(-abs(x) * 140) * exp(-abs(y) * 9);
                    col += (core + spikes * 0.9) * float3(1.0, 0.97, 0.95);
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
