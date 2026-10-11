// Sun rays: soft beams fanning out from the middle of a quad (added on top of whatever's behind), slowly
// turning. Put on a big quad that faces the camera.
Shader "3daigamesolo/World/Rays"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 0.92, 0.55)
        _Beams ("Number of beams", Float) = 14
        _Turn  ("Turning speed", Float) = 0.02
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _Beams, _Turn;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                float a = atan2(i.uv.y, i.uv.x) / 6.2831853 + _Time.y * _Turn;
                float beams = pow(saturate(sin(a * 6.2831853 * _Beams) * 0.5 + 0.5), 6) * 0.7
                            + pow(saturate(sin(a * 6.2831853 * _Beams * 0.37 + 1.3) * 0.5 + 0.5), 10) * 0.6;
                float glow = saturate(1 - r) ;
                float core = pow(saturate(1 - r * 3.2), 2);
                float k = (beams * glow * glow + core) * _Color.a;
                return fixed4(_Color.rgb * k, 1);
            }
            ENDCG
        }
    }
}
