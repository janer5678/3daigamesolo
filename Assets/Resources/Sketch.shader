// The level sketcher's look (LevelSketch): flat colour, shaded a little by which way a face points, with a
// grid drawn on every face in world space (thin lines every metre, thicker every five) so you can read sizes
// at a glance. The same shader draws the see-through preview of what you're about to place (blend and depth
// writing are properties). It lives in Resources so it's always in the build.
Shader "3daigamesolo/Sketch"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Grid ("Grid lines", Range(0, 1)) = 0.35
        _Lit ("Shading", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Blend src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Blend dst", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Depth write", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            Blend [_Src] [_Dst]
            ZWrite [_ZWrite]
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _Color;
            float _Grid, _Lit;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 wn : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wn = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float GridLine(float2 uv, float size)
            {
                float2 g = uv / size;
                float2 w = max(fwidth(g), 1e-4);
                float2 a = abs(frac(g - 0.5) - 0.5) / w;
                return 1 - saturate(min(a.x, a.y));
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float shade = 0.6 + 0.4 * saturate(dot(n, normalize(float3(0.4, 1, 0.25)))) + 0.06 * n.x;
                float light = lerp(1, shade, _Lit);
                float3 an = abs(n);
                float2 uv = an.y > 0.5 ? i.wp.xz : (an.x > 0.5 ? i.wp.zy : i.wp.xy);
                float g = max(GridLine(uv, 1) * 0.45, GridLine(uv, 5));
                float3 c = _Color.rgb * light;
                c = lerp(c, c * 0.5, g * _Grid);
                return float4(c, saturate(_Color.a + g * _Grid * 0.6));
            }
            ENDCG
        }
    }
}
