// The Circulation level's look, after the Renai Circulation video: flat pastel cel shading, the lit side its
// colour and the shade side a soft lilac-blue of it, a pale rim; a texture (cards, floating kana) times the
// vertex colour, cut out where it's see-through. Far away it fades into the white.
Shader "3daigamesolo/World/Pastel"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Shade   ("Shade tint", Color) = (0.78, 0.82, 0.98, 1)
        _SunDir  ("Sun direction", Vector) = (-0.4, 0.8, -0.45, 0)
        _Fade    ("Fades into the white by (m)", Float) = 380
        _Back    ("The white", Color) = (0.99, 0.98, 0.97, 1)
        _Cutoff  ("Alpha cutoff", Range(0, 1)) = 0.5
        _Glow    ("Unshaded (0..1)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _Shade, _SunDir, _Back;
            float _Fade, _Cutoff, _Glow;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; float3 nWS : TEXCOORD1; float2 uv : TEXCOORD2; float4 col : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nWS = UnityObjectToWorldNormal(v.normal);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.col = v.color;
                return o;
            }

            fixed4 frag(v2f i, fixed face : VFACE) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                clip(t.a - _Cutoff);
                float3 n = normalize(i.nWS) * (face > 0 ? 1 : -1);
                float3 base = t.rgb * i.col.rgb;
                float lit = step(0.0, dot(n, normalize(_SunDir.xyz)));
                float3 col = lerp(base * _Shade.rgb, base, lit);
                float3 v = normalize(_WorldSpaceCameraPos - i.posWS);
                col += pow(1 - saturate(dot(n, v)), 4) * 0.12;
                col = lerp(col, base, _Glow);
                float far = saturate(distance(i.posWS, _WorldSpaceCameraPos) / _Fade);
                col = lerp(col, _Back.rgb, far * far);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
