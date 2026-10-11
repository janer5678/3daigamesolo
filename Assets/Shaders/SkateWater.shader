// The river. A ribbon running down the valley beside the run. Cyan with hard white glints
// sliding downstream (+x).
Shader "3daigamesolo/World/Water"
{
    Properties
    {
        _Water      ("Water", Color)       = (0.42, 0.88, 0.96, 1)
        _WaterDeep  ("Water deep", Color)  = (0.30, 0.66, 0.90, 1)
        _Glint      ("Glint", Color)       = (1, 1, 1, 1)
        _Flow       ("Flow speed", Float)  = 0.6

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

            float4 _Water, _WaterDeep, _Glint;
            float _Flow;

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
                float2 p = i.posWS.xz;
                float band = pw_fbm(p * float2(0.05, 0.12) + float2(-_Time.y * 0.25 * _Flow, 0));
                float3 col = band > 0.5 ? _Water.rgb : _WaterDeep.rgb;
                float glint = pw_noise(p * float2(0.35, 0.9) + float2(-_Time.y * _Flow, 0));
                if (glint > 0.86) col = _Glint.rgb;
                return fixed4(pw_fog(col, i.posWS), 1);
            }
            ENDCG
        }
    }
}
