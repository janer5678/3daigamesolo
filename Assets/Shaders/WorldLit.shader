// The realistic look, for the levels after real games (Elden Ruins, Surf Mesa): a texture times the vertex colour, lit
// by the sun (the scene's directional light, with real shadows) and a sky/ground ambient, a little gloss, and faded
// into the level's haze with distance (brighter towards the sun). Foliage and grass are cut out of their texture
// (_Cutoff) and sway in the wind where the vertex colour's alpha is below 1 (0 sways most). _Emission lights it up on
// its own (1 is full bright, more than that glows into the bloom). The haze, sun haze and ambient colours are
// globals the level sets (LitWorld).
Shader "3daigamesolo/World/Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Emission ("Glow (1 full bright, more blooms)", Range(0, 6)) = 0
        _Cutoff ("Cut out below this alpha (0: solid)", Range(0, 1)) = 0
        _Wind ("Wind sway (metres)", Float) = 0
        _Gloss ("Gloss", Range(0, 1)) = 0
        _Wrap ("Light wrap (foliage)", Range(0, 1)) = 0
        _Scroll ("Texture scroll (uv per second)", Vector) = (0, 0, 0, 0)
        _FogAmount ("Haze (1 normal, less for landmarks seen through it)", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_ST;
    float4 _Color;
    float _Emission, _Cutoff, _Wind, _Gloss, _Wrap, _FogAmount;
    float4 _Scroll;

    float3 Sway(float3 wp, float weight)
    {
        if (_Wind <= 0) return wp;
        float t = _Time.y;
        float gust = 0.6 + 0.4 * sin(t * 0.7 + wp.x * 0.05 + wp.z * 0.03);
        wp.x += weight * _Wind * gust * sin(t * 1.9 + wp.x * 0.35 + wp.z * 0.21);
        wp.z += weight * _Wind * gust * 0.7 * cos(t * 1.5 + wp.z * 0.31 + wp.x * 0.17);
        return wp;
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma target 3.0
            #include "AutoLight.cginc"
            #include "Lighting.cginc"

            float4 _LitSky, _LitGround, _LitFog, _LitFogSun;
            float _LitFogDensity, _LitFogStart, _LitFogHeight, _LitFogMax, _LitFogFalloff;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float3 wp : TEXCOORD2;
                float4 col : COLOR;
                SHADOW_COORDS(3)
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                wp = Sway(wp, 1 - v.color.a);
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.wp = wp;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + _Scroll.xy * _Time.y;
                o.col = v.color;
                TRANSFER_SHADOW(o);
                return o;
            }

            float4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                float4 tex = tex2D(_MainTex, i.uv);
                if (_Cutoff > 0) clip(tex.a - _Cutoff);
                float3 albedo = tex.rgb * i.col.rgb * _Color.rgb;
                float3 n = normalize(i.wn) * (facing > 0 || _Wrap > 0 ? 1 : -1);   // (foliage: both sides lit the same)
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                float d = dot(n, L);
                float ndl = lerp(saturate(d), saturate(d * 0.5 + 0.5) * 0.85, _Wrap);
                float atten = SHADOW_ATTENUATION(i);
                float3 amb = lerp(_LitGround.rgb, _LitSky.rgb, n.y * 0.5 + 0.5);
                float3 c = albedo * (amb + _LightColor0.rgb * ndl * atten);
                float3 h = normalize(L + V);
                c += _LightColor0.rgb * pow(saturate(dot(n, h)), 8 + 120 * _Gloss) * _Gloss * atten * (0.3 + tex.r * 0.7);
                c = lerp(c, albedo, saturate(_Emission)) + albedo * max(0, _Emission - 1);

                // the haze: thicker with distance, thinner up high, warmer towards the sun
                float dist = length(i.wp - _WorldSpaceCameraPos);
                float f = 1 - exp(-max(0, dist - _LitFogStart) * _LitFogDensity);
                f *= exp(-max(0, i.wp.y - _LitFogHeight) * _LitFogFalloff);
                f = min(f, _LitFogMax) * _FogAmount;
                float sunward = pow(saturate(dot(-V, L)), 6);
                float3 fogc = lerp(_LitFog.rgb, _LitFogSun.rgb, sunward);
                c = lerp(c, fogc, f * (1 - saturate(_Emission - 1) * 0.5));
                return float4(c, 1);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma target 3.0

            struct v2f { V2F_SHADOW_CASTER; float2 uv : TEXCOORD1; };

            v2f vert(appdata_full v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                if (_Cutoff > 0) clip(tex2D(_MainTex, i.uv).a - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
