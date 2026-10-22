// A painted-realistic sky on the inside of a big sphere: the horizon colour rising into the zenith colour, a glow
// round the sun, a second glow anywhere you like (the Erdtree's golden light), and soft drifting clouds from a noise
// texture, lit from the sun's side. Drawn before everything and never writes depth.
Shader "3daigamesolo/World/RealSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.35, 0.5, 0.75, 1)
        _Horizon ("Horizon", Color) = (0.8, 0.82, 0.8, 1)
        _Below ("Below the horizon", Color) = (0.6, 0.62, 0.6, 1)
        _SunGlow ("Sun glow", Color) = (1, 0.85, 0.55, 1)
        _GlowColor ("Second glow", Color) = (1, 0.8, 0.3, 0)
        _GlowDir ("Second glow direction", Vector) = (0, 0.2, 1, 0)
        _Clouds ("Cloud noise", 2D) = "black" {}
        _CloudColor ("Clouds lit", Color) = (1, 1, 1, 1)
        _CloudShade ("Clouds shaded", Color) = (0.6, 0.62, 0.7, 1)
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.5
        _CloudScale ("Cloud scale", Float) = 0.6
        _CloudDrift ("Cloud drift", Vector) = (0.004, 0.001, 0, 0)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Pass
        {
            ZWrite Off
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Zenith, _Horizon, _Below, _SunGlow, _GlowColor, _GlowDir, _CloudColor, _CloudShade, _CloudDrift;
            float _CloudCover, _CloudScale;
            sampler2D _Clouds;

            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                o.wp = mul(unity_ObjectToWorld, vertex).xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.wp - _WorldSpaceCameraPos);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float up = d.y;
                float3 c = up > 0 ? lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(up), 0.55)) : lerp(_Horizon.rgb, _Below.rgb, saturate(-up * 6));
                float sun = saturate(dot(d, L));
                c += _SunGlow.rgb * (pow(sun, 8) * 0.35 + pow(sun, 200) * 2.5);
                float g = saturate(dot(d, normalize(_GlowDir.xyz)));
                c += _GlowColor.rgb * _GlowColor.a * (pow(g, 6) * 0.6 + pow(g, 40) * 0.8);

                // clouds on a flat layer above
                if (up > 0.01)
                {
                    float2 p = d.xz / (up + 0.12) * _CloudScale + _CloudDrift.xy * _Time.y;
                    float n = tex2D(_Clouds, p).r * 0.65 + tex2D(_Clouds, p * 2.7 + 0.3).r * 0.35;
                    float cover = smoothstep(1 - _CloudCover, 1 - _CloudCover + 0.35, n);
                    float lit = saturate(0.5 + 0.5 * dot(normalize(float3(d.x, 0, d.z) + 0.001), normalize(float3(L.x, 0, L.z) + 0.001)));
                    float3 cc = lerp(_CloudShade.rgb, _CloudColor.rgb, lit * 0.6 + n * 0.4) + _SunGlow.rgb * pow(sun, 6) * 0.5;
                    c = lerp(c, cc, cover * smoothstep(0.01, 0.15, up) * 0.9);
                }
                return float4(c, 1);
            }
            ENDCG
        }
    }
}
