// The realistic levels over the whole frame (RealLook): bloom (anything brighter than a threshold spills light round
// itself, through a chain of blurred half-size copies), then a filmic tone curve, a colour grade (exposure,
// contrast, saturation, a tint pushed into the shadows and another into the highlights), a soft vignette and a
// little film grain.
Shader "3daigamesolo/Post/Real"
{
    Properties
    {
        _MainTex ("Frame", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex, _Bloom;
    float4 _MainTex_TexelSize;
    float _Threshold, _BloomAmount, _Exposure, _Contrast, _Saturation, _Vignette, _Grain;
    float4 _ShadowTint, _HighTint;

    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert(appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

    float3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    ENDCG

    SubShader
    {
        ZTest Always Cull Off ZWrite Off

        // 0: keep only what's brighter than the threshold (soft knee), half size
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                float3 c = Box4(i.uv, 1);
                float b = max(c.r, max(c.g, c.b));
                float k = saturate((b - _Threshold * 0.6) / max(_Threshold * 0.8, 1e-4));
                return float4(c * k * k * saturate((b - _Threshold * 0.5) / max(b, 1e-4) + 0.5), 1);
            }
            ENDCG
        }
        // 1: down a size, blurred
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target { return float4(Box4(i.uv, 1), 1); }
            ENDCG
        }
        // 2: up a size, blurred, added onto the one underneath (blend One One)
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target { return float4(Box4(i.uv, 0.5), 1); }
            ENDCG
        }
        // 3: the frame plus the bloom, tone mapped and graded
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            float3 Aces(float3 x) { return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14)); }
            float4 frag(v2f i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                c += tex2D(_Bloom, i.uv).rgb * _BloomAmount;
                c = pow(Aces(pow(max(c, 0), 2.2) * _Exposure), 1 / 2.2);   // (the project works in gamma space)
                float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(float3(l, l, l), c, _Saturation);
                c = lerp(c, c * _ShadowTint.rgb * 2, (1 - smoothstep(0, 0.45, l)) * _ShadowTint.a);
                c = lerp(c, c * _HighTint.rgb * 2, smoothstep(0.4, 1, l) * _HighTint.a);
                c = saturate((c - 0.5) * _Contrast + 0.5);
                float2 q = i.uv - 0.5;
                c *= 1 - _Vignette * smoothstep(0.25, 0.9, dot(q, q) * 2.2);
                float n = frac(sin(dot(i.uv * 1000 + _Time.y * 37, float2(12.9898, 78.233))) * 43758.5453);
                c += (n - 0.5) * _Grain;
                return float4(c, 1);
            }
            ENDCG
        }
    }
}
