// The ink line. Drawn as a second material on the same renderer (Unity draws the last submesh
// again for every extra material).
//
// Classic inverted hull: back faces only, pushed out along the normal by a fixed number of
// SCREEN pixels, so a ridge a kilometre away gets the same weight of line as a rail next to
// you. It fades into the haze like everything else.
Shader "3daigamesolo/World/Outline"
{
    Properties
    {
        _OutlineColor ("Line colour", Color) = (0.17, 0.10, 0.29, 1)
        _OutlineWidth ("Line width (pixels)", Float) = 1.6

        _Haze        ("Haze (low)", Color)    = (0.98, 0.88, 0.94, 1)
        _HazeHigh    ("Haze (high)", Color)   = (0.86, 0.95, 1.00, 1)
        _FogDensity  ("Fog density", Float)   = 0.0011
        _FogHeight   ("Fog height falloff", Float) = 220
        _FogMax      ("Fog max", Range(0,1))  = 0.93
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        Cull Front

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "SkateWorld.cginc"

            float4 _OutlineColor;
            float _OutlineWidth;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                float4 cs = UnityObjectToClipPos(v.vertex);
                float3 nWS = UnityObjectToWorldNormal(v.normal);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = length(nCS);
                if (len > 1e-5)
                    cs.xy += (nCS / len) * _OutlineWidth * 2.0 / _ScreenParams.xy * cs.w;
                o.pos = cs;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(pw_fog(_OutlineColor.rgb, i.posWS), 1);
            }
            ENDCG
        }
    }
}
