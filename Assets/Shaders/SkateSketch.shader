// The Sketchbook level's look, after a pencil drawing washed with lime-green watercolour: until it's painted,
// everything is off-white paper with pencil hatching (denser the darker it is, cross-hatched in the shadows);
// paint it (_Paint, per object, 0 to 1) and a watercolour wash spreads over it in blotches, the pencil still
// showing through. The floor can carry the wavy black-and-white pattern of Portuguese paving (_Waves). Far away
// it all fades into the paper.
Shader "3daigamesolo/World/Sketch"
{
    Properties
    {
        _Paint    ("Painted (0 to 1)", Range(0, 1)) = 0
        _Wash     ("Watercolour", Color) = (0.62, 0.88, 0.2, 1)
        _Paper    ("Paper", Color) = (0.97, 0.96, 0.92, 1)
        _Graphite ("Pencil", Color) = (0.2, 0.2, 0.22, 1)
        _Hatch    ("Hatching lines across the screen", Float) = 150
        _Waves    ("Paving waves per metre (0 off)", Float) = 0
        _SunDir   ("Sun direction", Vector) = (-0.5, 0.75, -0.4, 0)
        _Fade     ("Fades into the paper by (m)", Float) = 220
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Paint, _Hatch, _Waves, _Fade, _SketchAll;
            float4 _Wash, _Paper, _Graphite, _SunDir;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; float3 nWS : TEXCOORD1; float4 scr : TEXCOORD2; float4 col : COLOR; };

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y);
            }
            float fbm(float2 p) { return noise(p) * 0.55 + noise(p * 2.1 + 3.7) * 0.3 + noise(p * 4.3 + 9.1) * 0.15; }

            v2f vert(appdata v)
            {
                v2f o;
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nWS = UnityObjectToWorldNormal(v.normal);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.scr = ComputeScreenPos(o.pos);
                o.col = v.color;
                return o;
            }

            float lines(float u, float width)
            {
                float d = abs(frac(u) - 0.5);
                return 1 - smoothstep(width * 0.5 - 0.08, width * 0.5 + 0.08, d);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                float light = saturate(dot(n, normalize(_SunDir.xyz))) * 0.55 + 0.45 + n.y * 0.1;
                float tone = dot(i.col.rgb, float3(0.3, 0.55, 0.15)) * light;
                float dark = saturate(1 - tone);

                // the paving's waves: dark bands snaking across the floor
                float wave = 0;
                if (_Waves > 0)
                {
                    float v = i.posWS.z * _Waves + sin(i.posWS.x * _Waves * 2.2) * 0.35;
                    wave = smoothstep(0.32, 0.36, abs(frac(v) - 0.5)) * step(0.7, n.y);
                    dark = max(dark, wave * 0.75);
                }

                // pencil: diagonal hatching in screen space, thicker the darker it is, crossed in the shadows
                float2 s = i.scr.xy / i.scr.w;
                s.x *= _ScreenParams.x / _ScreenParams.y;
                float wob = (noise(s * 9 + floor(_Time.y * 6)) - 0.5) * 0.25;   // (the lines boil a little)
                float2 u = s * _Hatch;
                float h1 = lines(u.x + u.y + wob, saturate(dark * 0.9 - 0.08));
                float h2 = lines(u.x - u.y + wob * 1.3, saturate(dark * 0.9 - 0.45));
                float pencil = max(h1, h2) * (0.55 + 0.45 * noise(u * 0.13));
                pencil = max(pencil, wave * 0.9);
                float grain = 0.95 + 0.05 * noise(s * 380);
                float3 paper = _Paper.rgb * grain;
                float3 sketch = lerp(paper, _Graphite.rgb, pencil * 0.8);

                // watercolour: the object's colour pulled towards the wash, blotchy, darker at the blotches' edges
                float blot = fbm(i.posWS.xz * 0.21 + i.posWS.y * 0.17);
                float3 wash = lerp(i.col.rgb, _Wash.rgb, 0.62) * lerp(0.72, 1.08, light) * lerp(0.86, 1.06, blot);
                wash = lerp(wash, paper, 0.12);
                float3 painted = wash * (1 - pencil * 0.45);
                // it spreads in blotches as _Paint goes from 0 to 1
                float p = saturate(max(_Paint, _SketchAll));
                float reveal = smoothstep(blot * 0.9 - 0.02, blot * 0.9 + 0.08, p * 1.05);
                float edge = (1 - abs(reveal * 2 - 1)) * 0.25;
                float3 col = lerp(sketch, painted, reveal) * (1 - edge * 0.5);

                // far away it fades into the paper
                float far = saturate(distance(i.posWS, _WorldSpaceCameraPos) / _Fade);
                col = lerp(col, paper, far * far * 0.85);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
