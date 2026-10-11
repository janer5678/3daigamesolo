// Trees, ramps, rails, the skater - anything built out of vertex-coloured pieces. The vertex
// colour is the lit colour; the shadow is that colour pushed towards lavender.
Shader "3daigamesolo/World/Toon"
{
    Properties
    {
        _Color       ("Tint", Color)          = (1, 1, 1, 1)
        _ShadeTint   ("Shade tint", Color)    = (0.58, 0.58, 0.86, 1)
        _Glitter     ("Glitter (0 off)", Range(0,1)) = 0
        _DotColor    ("Halftone dots (alpha = strength)", Color) = (1, 1, 1, 0)
        _DotScale    ("Dots per metre", Float) = 1.5

        _SunDir      ("Sun direction", Vector) = (-0.62, 0.4, 0.55, 0)
        _Haze        ("Haze (low)", Color)    = (0.98, 0.88, 0.94, 1)
        _HazeHigh    ("Haze (high)", Color)   = (0.86, 0.95, 1.00, 1)
        _FogDensity  ("Fog density", Float)   = 0.0011
        _FogHeight   ("Fog height falloff", Float) = 220
        _FogMax      ("Fog max", Range(0,1))  = 0.93
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "SkateWorld.cginc"

            float4 _Color, _ShadeTint, _DotColor;
            float _Glitter, _DotScale;

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
                float3 lit = i.col.rgb;
                float3 col = pw_band(pw_cel(i.nWS), lit, lit * _ShadeTint.rgb);

                // halftone dots across flat floors, swelling and shrinking in slow waves (the Y2K banner look)
                if (_DotColor.a > 0)
                {
                    float2 p = i.posWS.xz * _DotScale;
                    float2 cell = frac(p) - 0.5;
                    float size = 0.12 + 0.22 * pw_noise(i.posWS.xz * 0.04);
                    float dotMask = step(length(cell), size) * step(0.7, i.nWS.y);
                    col = lerp(col, _DotColor.rgb, dotMask * _DotColor.a);
                }
                // glitter: tiny cells that twinkle on and off
                if (_Glitter > 0)
                {
                    float3 gp = floor(i.posWS * 22);
                    float h = pw_hash(gp.xz + gp.y * 7.13);
                    float tw = saturate(sin(_Time.y * 2.6 + h * 60) * 1.6 - 0.6);
                    float spark = step(1 - _Glitter * 0.05, h) * tw;
                    col = lerp(col, float3(1, 1, 1), spark * 0.85);
                }
                return fixed4(pw_fog(col, i.posWS), 1);
            }
            ENDCG
        }
    }
}
