// The Aero level's look (Frutiger Aero): bright, clean and glossy. Soft light from the sun, a sharp
// white highlight, a bright rim round the edges, and a bit of the sky reflected in everything, more at
// glancing angles. Vertex colours carry the colour; a light haze in the distance. Optionally a glowing
// digital grid over everything (_GridColor's alpha), with a bright scan line sweeping across it.
Shader "3daigamesolo/World/Glossy"
{
    Properties
    {
        _Color      ("Tint", Color) = (1, 1, 1, 1)
        _SkyTop     ("Sky reflected (up)", Color) = (0.25, 0.62, 1, 1)
        _SkyLow     ("Sky reflected (horizon)", Color) = (0.85, 0.95, 1, 1)
        _Gloss      ("Reflection", Range(0, 1)) = 0.35
        _SpecPower  ("Highlight sharpness", Float) = 60
        _Spec       ("Highlight strength", Float) = 0.9
        _Rim        ("Rim strength", Float) = 0.45
        _GridColor  ("Grid (alpha = strength)", Color) = (0.6, 1, 1, 0)
        _GridCell   ("Grid cell (m)", Float) = 6

        _SunDir      ("Sun direction", Vector) = (-0.3, 0.7, 0.6, 0)
        _Haze        ("Haze (low)", Color)    = (0.82, 0.93, 1.00, 1)
        _HazeHigh    ("Haze (high)", Color)   = (0.62, 0.84, 1.00, 1)
        _FogDensity  ("Fog density", Float)   = 0.0012
        _FogHeight   ("Fog height falloff", Float) = 300
        _FogMax      ("Fog max", Range(0,1))  = 0.85
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
            #include "SkateWorld.cginc"

            float4 _Color, _SkyTop, _SkyLow, _GridColor;
            float _Gloss, _SpecPower, _Spec, _Rim, _GridCell;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; float3 nWS : TEXCOORD1; float4 col : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nWS = UnityObjectToWorldNormal(v.normal);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.col = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.nWS), l = normalize(_SunDir.xyz), v = normalize(_WorldSpaceCameraPos - i.posWS);
                float3 col = i.col.rgb * (0.62 + 0.38 * saturate(dot(n, l)) + 0.12 * n.y);
                float fres = pow(1 - saturate(dot(n, v)), 3);
                float3 r = reflect(-v, n);
                float3 sky = lerp(_SkyLow.rgb, _SkyTop.rgb, saturate(r.y));
                col = lerp(col, sky, saturate(_Gloss * (0.25 + fres)));
                col += pow(saturate(dot(reflect(-l, n), v)), _SpecPower) * _Spec;
                col += fres * _Rim;
                if (_GridColor.a > 0)
                {
                    float3 an = abs(n);
                    float2 uv = (an.y > max(an.x, an.z) ? i.posWS.xz : (an.x > an.z ? i.posWS.zy : i.posWS.xy)) / _GridCell;
                    float2 f = abs(frac(uv) - 0.5);
                    float grid = smoothstep(0.03, 0.0, 0.5 - max(f.x, f.y));
                    float fine = smoothstep(0.02, 0.0, 0.5 - max(abs(frac(uv * 4) - 0.5).x, abs(frac(uv * 4) - 0.5).y)) * 0.35;
                    float scan = pow(saturate(sin(dot(i.posWS, float3(0.02, 0.01, 0.025)) * 6.28 - _Time.y * 1.5) * 0.5 + 0.5), 30);
                    float fade = 1 - saturate(distance(i.posWS, _WorldSpaceCameraPos) / 260);
                    col += _GridColor.rgb * _GridColor.a * (max(grid, fine) * fade + scan * 0.6);
                }
                return fixed4(pw_fog(col, i.posWS), 1);
            }
            ENDCG
        }
    }
}
