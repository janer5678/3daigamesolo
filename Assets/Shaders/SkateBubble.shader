// See-through and glossy, with a rainbow sheen at the edges: soap bubbles, glass, the jelly rider, the CD.
// Mostly clear in the middle, more solid (and more rainbow) at glancing angles. _Alpha 1 makes it solid.
Shader "3daigamesolo/World/Bubble"
{
    Properties
    {
        _Color      ("Tint", Color) = (0.6, 0.85, 1, 1)
        _Alpha      ("Opacity in the middle", Range(0, 1)) = 0.12
        _Rainbow    ("Rainbow sheen", Range(0, 1)) = 0.7
        _SpecPower  ("Highlight sharpness", Float) = 90
        _Spec       ("Highlight strength", Float) = 1.2
        _SunDir     ("Sun direction", Vector) = (-0.3, 0.7, 0.6, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color, _SunDir;
            float _Alpha, _Rainbow, _SpecPower, _Spec;

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

            float3 hsv(float h)
            {
                return saturate(abs(frac(h + float3(0, 2.0 / 3, 1.0 / 3)) * 6 - 3) - 1);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.nWS), v = normalize(_WorldSpaceCameraPos - i.posWS), l = normalize(_SunDir.xyz);
                float nv = saturate(dot(n, v));
                float fres = pow(1 - nv, 2.5);
                float3 rainbow = hsv(frac(nv * 1.7 + i.posWS.y * 0.05 + _Time.y * 0.05));
                float3 col = lerp(i.col.rgb, rainbow, fres * _Rainbow) + fres * 0.35;
                float spec = pow(saturate(dot(reflect(-l, n), v)), _SpecPower) * _Spec;
                col += spec;
                float a = saturate(_Alpha + fres * 0.85 + spec);
                return fixed4(col, a * i.col.a);
            }
            ENDCG
        }
    }
}
