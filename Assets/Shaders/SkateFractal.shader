// The Infinity level's look, after a fly-through of a 3D fractal: everything glows in one colour that
// slowly cycles (cyan, green, red, white, orange...) and shifts with height and distance, so near and far
// copies of the structure are different colours at once. Surfaces are dark, with glowing grid lines
// nested at several scales (each a quarter the size of the last), so every face looks like it's made of
// smaller copies of itself. It all fades out into black.
Shader "3daigamesolo/World/Fractal"
{
    Properties
    {
        _HueSpeed   ("Colour cycle (turns per second)", Float) = 0.025
        _HueSpread  ("Colour change per metre away", Float) = 0.0035
        _Saturation ("Saturation", Range(0, 1)) = 0.8
        _Glow       ("Line glow", Float) = 1.6
        _Base       ("Face brightness", Range(0, 1)) = 0.32
        _Cell       ("Biggest grid cell (m)", Float) = 8
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

            float _HueSpeed, _HueSpread, _Saturation, _Glow, _Base, _Cell, _FogDensity;
            float4 _Fog;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; float3 nWS : TEXCOORD1; float4 col : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nWS = UnityObjectToWorldNormal(v.normal);
                o.pos = UnityObjectToClipPos(v.vertex);
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

            float lines(float2 uv)
            {
                float l = 0, w = 1;
                for (int k = 0; k < 3; k++) { l = max(l, cellLines(uv) * w); uv *= 4; w *= 0.55; }
                return l;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                float dist = distance(i.posWS, _WorldSpaceCameraPos);
                // the colour: cycling in time, shifting with height and with distance from the middle
                float hue = frac(_Time.y * _HueSpeed + i.posWS.y * 0.006 + length(i.posWS.xz) * _HueSpread + i.col.a * 0.25);
                float3 glow = hsv(hue, _Saturation, 1);
                // the lines, projected onto whichever plane the face is closest to
                float3 an = abs(n);
                float2 uv = an.y > max(an.x, an.z) ? i.posWS.xz : (an.x > an.z ? i.posWS.zy : i.posWS.xy);
                float l = lines(uv / _Cell);
                // faces: dark, lit from above and a little from the side, tinted by the vertex colour
                float light = 0.45 + 0.55 * saturate(dot(n, normalize(float3(0.3, 1, 0.2))));
                float3 col = glow * i.col.rgb * _Base * light + glow * l * _Glow;
                // a bright rim where a face turns away from you
                float rim = pow(1 - saturate(abs(dot(n, normalize(_WorldSpaceCameraPos - i.posWS)))), 3);
                col += glow * rim * 0.5;
                col = lerp(col, _Fog.rgb, 1 - exp(-dist * _FogDensity));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
