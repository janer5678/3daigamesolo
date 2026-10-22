// Light and air for the realistic levels: unlit, see-through, never writes depth. Additive (beams of light, the
// golden glow of a Site of Grace, the Erdtree's halo, the teleport lights) or blended (the sea of mist, cloud banks),
// set by the blend properties. Texture times vertex colour times _Color; it can scroll (two layers) and pulse, and it
// fades out as it gets close to the camera, so you don't fly into the edges of the cards.
Shader "3daigamesolo/World/Glow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour (alpha: strength)", Color) = (1, 1, 1, 1)
        _Scroll ("Scroll (uv per second; zw: second layer)", Vector) = (0, 0, 0, 0)
        _NearFade ("Fade out closer than (m)", Float) = 2
        _Pulse ("Pulse (speed, amount)", Vector) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.BlendMode)] _Src ("Blend src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _Dst ("Blend dst", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend [_Src] [_Dst]
            ZWrite Off
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _Color, _Scroll, _Pulse;
            float _NearFade;

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; float4 col : COLOR; float dist : TEXCOORD2; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float2 uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv = uv + _Scroll.xy * _Time.y;
                o.uv2 = uv * 0.7 + _Scroll.zw * _Time.y + 0.37;
                o.col = v.color;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.dist = length(wp - _WorldSpaceCameraPos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 t = tex2D(_MainTex, i.uv);
                if (_Scroll.z != 0 || _Scroll.w != 0) t = (t + tex2D(_MainTex, i.uv2)) * 0.5;
                float4 c = t * i.col * _Color;
                float pulse = 1 + _Pulse.y * sin(_Time.y * _Pulse.x);
                float near = saturate((i.dist - _NearFade * 0.5) / max(_NearFade, 0.01));
                c.a *= near * pulse;
                c.rgb *= c.a;   // premultiplied: additive (One One) adds it, blended (One OneMinusSrcAlpha) lays it over
                return c;
            }
            ENDCG
        }
    }
}
