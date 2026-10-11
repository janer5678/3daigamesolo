// The Aero level's look (Frutiger Aero): bright, clean and glossy. Soft light from the sun, a sharp
// white highlight, a bright rim round the edges, and a bit of the sky reflected in everything, more at
// glancing angles. Vertex colours carry the colour; a light haze in the distance.
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

            float4 _Color, _SkyTop, _SkyLow;
            float _Gloss, _SpecPower, _Spec, _Rim;

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
                return fixed4(pw_fog(col, i.posWS), 1);
            }
            ENDCG
        }
    }
}
