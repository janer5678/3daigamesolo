// The Infinity level's look, after a fly-through of a 3D fractal, and always moving like it: everything
// glows in one colour that cycles (cyan, green, red, white, orange...) and shifts with height and distance;
// the faces are dark with glowing grid lines nested at several scales that zoom inward for ever (each
// scale growing into the next); waves of light pulse out through the structure; and anything marked as
// decoration (uv.x) sways and breathes. It all fades out into black.
Shader "3daigamesolo/World/Fractal"
{
    Properties
    {
        _HueSpeed   ("Colour cycle (turns per second)", Float) = 0.06
        _HueSpread  ("Colour change per metre away", Float) = 0.0035
        _Saturation ("Saturation", Range(0, 1)) = 0.8
        _Glow       ("Line glow", Float) = 1.6
        _Base       ("Face brightness", Range(0, 1)) = 0.32
        _Cell       ("Biggest grid cell (m)", Float) = 8
        _Zoom       ("Zoom speed (scales per second)", Float) = 0.18
        _Pulse      ("Light waves (strength)", Float) = 0.8
        _Sway       ("Decoration sway (m)", Float) = 0.9
        _Fog        ("Fog colour", Color) = (0, 0, 0, 1)
        _FogDensity ("Fog density", Float) = 0.0065
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

            float _HueSpeed, _HueSpread, _Saturation, _Glow, _Base, _Cell, _Zoom, _Pulse, _Sway, _FogDensity;
            float4 _Fog;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; float3 nWS : TEXCOORD1; float4 col : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                float3 p = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 n = UnityObjectToWorldNormal(v.normal);
                // decoration breathes in and out along its normal and sways sideways
                float w = v.uv.x;
                if (w > 0)
                {
                    float t = _Time.y;
                    p += n * sin(t * 1.7 + p.y * 0.35 + p.x * 0.07) * _Sway * 0.5 * w;
                    p.xz += float2(sin(t * 0.9 + p.y * 0.21), cos(t * 0.8 + p.y * 0.19)) * _Sway * w * saturate(-v.vertex.y * 0.1 + 0.3);
                }
                o.posWS = p;
                o.nWS = n;
                o.pos = mul(UNITY_MATRIX_VP, float4(p, 1));
                o.col = v.color;
                return o;
            }

            float3 hsv(float h, float s, float v)
            {
                float3 k = saturate(abs(frac(h + float3(0, 2.0 / 3, 1.0 / 3)) * 6 - 3) - 1);
                return v * lerp(float3(1, 1, 1), k, s);
            }

            // a glowing line round the edge of every cell, and a diamond inside it (a kaleidoscope motif)
            float cellLines(float2 uv)
            {
                float2 f = abs(frac(uv) - 0.5);
                float edge = smoothstep(0.035, 0.0, 0.5 - max(f.x, f.y));
                float diamond = smoothstep(0.03, 0.0, abs(f.x + f.y - 0.36));
                return max(edge, diamond * 0.6);
            }

            // four scales of grid, each 4x the last, sliding up through the scales for ever: as one grows too
            // big and fades it's replaced by the next, so it seems to zoom in without end
            float lines(float2 uv)
            {
                float phase = 1 - frac(_Time.y * _Zoom);   // (cells growing: zooming in)
                float l = 0;
                for (int k = 0; k < 4; k++)
                {
                    float level = k + phase;
                    float scale = exp2(level * 2);
                    float fade = sin(3.14159 * level / 4);
                    l = max(l, cellLines(uv * scale / exp2(4 * 2) * 16) * fade);
                }
                return l;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                float dist = distance(i.posWS, _WorldSpaceCameraPos);
                float t = _Time.y;
                float hue = frac(t * _HueSpeed + i.posWS.y * 0.006 + length(i.posWS.xz) * _HueSpread + i.col.a * 0.6);
                float3 glow = hsv(hue, _Saturation, 1);
                float3 an = abs(n);
                float2 uv = an.y > max(an.x, an.z) ? i.posWS.xz : (an.x > an.z ? i.posWS.zy : i.posWS.xy);
                float l = lines(uv / _Cell);
                // waves of light rolling outward through everything, and down
                float wave = pow(saturate(sin(dist * 0.08 - t * 2.2 + i.posWS.y * 0.05) * 0.5 + 0.5), 6) * _Pulse;
                float light = 0.45 + 0.55 * saturate(dot(n, normalize(float3(0.3, 1, 0.2))));
                float3 col = glow * i.col.rgb * _Base * light + glow * l * _Glow * (1 + wave) + glow * wave * 0.25;
                float rim = pow(1 - saturate(abs(dot(n, normalize(_WorldSpaceCameraPos - i.posWS)))), 3);
                col += glow * rim * 0.5;
                col = lerp(col, _Fog.rgb, 1 - exp(-dist * _FogDensity));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
