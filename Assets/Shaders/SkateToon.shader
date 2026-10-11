// Trees, ramps, rails, the skater - anything built out of vertex-coloured pieces. The vertex
// colour is the lit colour; the shadow is that colour pushed towards lavender.
Shader "3daigamesolo/World/Toon"
{
    Properties
    {
        _Color       ("Tint", Color)          = (1, 1, 1, 1)
        _ShadeTint   ("Shade tint", Color)    = (0.58, 0.58, 0.86, 1)

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

            float4 _Color, _ShadeTint;

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
                return fixed4(pw_fog(col, i.posWS), 1);
            }
            ENDCG
        }
    }
}
