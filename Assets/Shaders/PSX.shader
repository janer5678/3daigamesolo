// PlayStation-style textured surfaces for the Akihabara level. Vertices snap to a coarse screen grid
// (the wobble), textures are point-sampled and partly affine-mapped (the warp), lighting is per vertex
// from one moon direction plus a flat ambient, colour is cut to 5 bits a channel with an ordered
// dither, and everything fades into a coloured distance fog. Signs set _Emission to 1 (full bright),
// and can scroll (LED tickers) and flicker (neon) on their own, with no scripts.
//
// The fog, moon, wobble and warp amounts are globals, set by PSXCamera.
Shader "3daigamesolo/PSX/Textured"
{
    Properties
    {
        _MainTex   ("Texture", 2D)                = "white" {}
        _Color     ("Tint", Color)                = (1, 1, 1, 1)
        _Emission  ("Full bright (signs)", Range(0, 1)) = 0
        _Scroll    ("Scroll (uv per second)", Vector) = (0, 0, 0, 0)
        _Flicker   ("Flicker: rate, on fraction, low", Vector) = (0, 1, 1, 0)
        _Cutoff    ("Alpha cutoff", Range(0, 1))  = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _Scroll, _Flicker;
            float _Emission, _Cutoff;

            float4 _PSXFogColor;     // rgb, a unused
            float4 _PSXFog;          // x start, y end
            float4 _PSXSun;          // xyz direction (towards the light), w strength
            float4 _PSXAmbient;      // rgb
            float4 _PSXSnap;         // xy grid (e.g. 320, 180), z affine amount 0..1

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                noperspective float2 uvAffine : TEXCOORD1;
                float4 col : COLOR;
                float fog : TEXCOORD2;
                float phase : TEXCOORD3;
                float4 screen : TEXCOORD4;
            };

            float hash3(float3 p) { p = frac(p * 0.3183 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }

            v2f vert(appdata v)
            {
                v2f o;
                float3 posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                float4 clip = UnityObjectToClipPos(v.vertex);
                // the wobble: snap to a coarse grid in screen space
                float2 grid = max(_PSXSnap.xy, float2(64, 48)) * 0.5;
                if (clip.w > 0.01) clip.xy = round(clip.xy / clip.w * grid) / grid * clip.w;
                o.pos = clip;
                o.screen = ComputeScreenPos(clip);
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex) + _Scroll.xy * _Time.y;
                o.uv = uv;
                o.uvAffine = uv;
                // per-vertex light from the moon, plus ambient; signs are full bright
                float3 n = UnityObjectToWorldNormal(v.normal);
                float ndl = saturate(dot(n, normalize(_PSXSun.xyz)));
                float3 lit = _PSXAmbient.rgb + ndl * _PSXSun.w;
                o.col = float4(lerp(lit, float3(1, 1, 1), _Emission), 1) * v.color * _Color;
                float d = distance(posWS, _WorldSpaceCameraPos);
                o.fog = saturate((d - _PSXFog.x) / max(_PSXFog.y - _PSXFog.x, 1));
                o.phase = hash3(floor(posWS / 3.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = lerp(i.uv, i.uvAffine, _PSXSnap.z);
                fixed4 t = tex2D(_MainTex, uv);
                if (t.a < _Cutoff) discard;
                float3 col = t.rgb * i.col.rgb;
                // neon flicker: on for part of each cycle, each sign on its own phase
                if (_Flicker.x > 0)
                {
                    float c = frac(_Time.y * _Flicker.x + i.phase * 7.3);
                    col *= c < _Flicker.y ? 1.0 : _Flicker.z;
                }
                col = lerp(col, _PSXFogColor.rgb, i.fog * (1 - _Emission * 0.6));
                // 5 bits a channel with a 4x4 ordered dither
                const float4x4 bayer = float4x4(0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);
                int2 p = int2(fmod(floor(i.screen.xy / max(i.screen.w, 1e-4) * _ScreenParams.xy), 4));
                float dith = (bayer[p.x][p.y] / 16.0 - 0.5) / 31.0;
                col = floor((col + dith) * 31.0 + 0.5) / 31.0;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
