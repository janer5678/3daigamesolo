// The sky: cyan going pale towards the horizon, with flat-cut anime clouds - white on top, a
// lavender-pink band underneath, hard edges. Drawn on the inside of a big sphere before
// anything else, and never writes depth, so everything sits in front of it.
Shader "3daigamesolo/World/Sky"
{
    Properties
    {
        _Zenith     ("Zenith", Color)        = (0.36, 0.80, 0.95, 1)
        _Horizon    ("Horizon", Color)       = (0.90, 0.96, 1.00, 1)
        _Below      ("Below horizon", Color) = (0.98, 0.88, 0.94, 1)
        _Cloud      ("Cloud", Color)         = (1, 1, 1, 1)
        _CloudShade ("Cloud shade", Color)   = (0.93, 0.78, 0.92, 1)
        _CloudCover ("Cloud cover", Range(0,1.2)) = 0.54
        _CloudDrift ("Cloud drift", Vector)  = (0.006, 0.002, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" }
        Cull Front
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "SkateWorld.cginc"

            float4 _Zenith, _Horizon, _Below, _Cloud, _CloudShade, _CloudDrift;
            float _CloudCover;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.posWS - _WorldSpaceCameraPos);
                float up = dir.y;
                float3 col = up > 0 ? lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(up), 0.55)) : _Below.rgb;

                if (up > 0.015)
                {
                    float2 uv = dir.xz / (up + 0.10) * 0.9 + _Time.y * _CloudDrift.xy;
                    float c   = pw_fbm(uv);
                    float top = pw_fbm(uv + float2(0.05, 0.05));   // a step up and over: lit side
                    float fade = saturate((up - 0.015) / 0.12);
                    if (c > _CloudCover)
                    {
                        float3 cloud = top > c - 0.015 ? _Cloud.rgb : _CloudShade.rgb;
                        col = lerp(col, cloud, fade);
                    }
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
