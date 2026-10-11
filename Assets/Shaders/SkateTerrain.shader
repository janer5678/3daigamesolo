// The ground, the banks and the mountains, all one shader so they agree on where the snow
// starts. Colour is picked from height and steepness in world space, so the near ground and the
// far ranges (two meshes at different resolutions) meet without a seam.
//
// On top of the Pururin terrain: the vertex colour marks the skate run (red) and the plaza
// (green). The run is the pink dirt path from the reference; the plaza is pale painted concrete
// with faint slab lines.
Shader "3daigamesolo/World/Terrain"
{
    Properties
    {
        _Ground      ("Ground", Color)        = (0.99, 0.88, 0.82, 1)
        _GroundShade ("Ground shade", Color)  = (0.86, 0.64, 0.78, 1)
        _Grass       ("Fields", Color)        = (0.62, 0.92, 0.86, 1)
        _GrassShade  ("Fields shade", Color)  = (0.40, 0.70, 0.82, 1)
        _Rock        ("Rock", Color)          = (0.74, 0.68, 0.93, 1)
        _RockShade   ("Rock shade", Color)    = (0.42, 0.34, 0.70, 1)
        _Snow        ("Snow", Color)          = (1.00, 0.99, 1.00, 1)
        _SnowShade   ("Snow shade", Color)    = (0.66, 0.86, 0.98, 1)
        _Flower      ("Flowers", Color)       = (0.97, 0.62, 0.78, 1)
        _FlowerShade ("Flowers shade", Color) = (0.80, 0.46, 0.72, 1)
        _Path        ("Path", Color)          = (0.98, 0.76, 0.80, 1)
        _PathShade   ("Path shade", Color)    = (0.82, 0.54, 0.74, 1)
        _PathEdge    ("Path edge", Color)     = (0.62, 0.46, 0.82, 1)
        _Plaza       ("Plaza", Color)         = (0.97, 0.92, 0.97, 1)
        _PlazaShade  ("Plaza shade", Color)   = (0.78, 0.72, 0.92, 1)
        _PlazaLine   ("Plaza slab line", Color) = (0.86, 0.76, 0.93, 1)
        _SnowLine    ("Snow line", Float)     = 230
        _CliffSlope  ("Steeper than this is rock (normal.y)", Float) = 0.74

        _SunDir      ("Sun direction", Vector) = (-0.62, 0.4, 0.55, 0)
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

            float4 _Ground, _GroundShade, _Grass, _GrassShade, _Rock, _RockShade, _Snow, _SnowShade, _Flower, _FlowerShade;
            float4 _Path, _PathShade, _PathEdge, _Plaza, _PlazaShade, _PlazaLine;
            float _SnowLine, _CliffSlope;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 posWS : TEXCOORD0; float3 nWS : TEXCOORD1; float4 mask : COLOR; };

            v2f vert(appdata v)
            {
                v2f o;
                o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nWS = UnityObjectToWorldNormal(v.normal);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.mask = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.nWS);
                float3 p = i.posWS;
                float cel = pw_cel(n);

                // hard-edged patches of field over the ground, like painted shapes
                float patch = pw_fbm(p.xz * 0.011 + 3.7);
                float3 lit   = patch > 0.53 ? _Grass.rgb      : _Ground.rgb;
                float3 shade = patch > 0.53 ? _GrassShade.rgb : _GroundShade.rgb;

                float camDist = distance(p, _WorldSpaceCameraPos);
                if (camDist < 140)
                {
                    float dab = pw_noise(p.xz * 2.4);
                    float speck = pw_noise(p.xz * 2.9 + 40);
                    if (dab > 0.88 - camDist * 0.0005)      { lit = _Flower.rgb; shade = _FlowerShade.rgb; }
                    else if (speck > 0.86)                   { lit = lerp(lit, 1, 0.45); shade = lerp(shade, lit, 0.35); }
                }

                bool rock = false;
                float breakup = pw_fbm(p.xz * 0.045);
                if (n.y < _CliffSlope + (breakup - 0.5) * 0.18)
                {
                    lit = _Rock.rgb; shade = _RockShade.rgb;
                    rock = true;
                }

                float snowLine = _SnowLine + (breakup - 0.5) * 90;
                if (p.y > snowLine && n.y > 0.30 - saturate((p.y - snowLine) / 300) * 0.25)
                {
                    lit = _Snow.rgb; shade = _SnowShade.rgb;
                    rock = false;
                }

                // the run: pink dirt with a ragged painted edge and a darker lip along it
                float edgeNoise = (pw_noise(p.xz * 0.35) - 0.5) * 0.35;
                float path = i.mask.r + edgeNoise;
                if (path > 0.5)
                {
                    lit = _Path.rgb; shade = _PathShade.rgb; rock = false;
                    float streak = pw_noise(float2(p.x * 0.6, p.z * 2.2));
                    if (camDist < 120 && streak > 0.84) { lit = lerp(lit, _PathShade.rgb, 0.45); }
                }
                else if (path > 0.36)
                {
                    lit = _PathEdge.rgb; shade = _PathEdge.rgb * 0.8; rock = false;
                }

                // the plaza: pale slabs with thin seams
                if (i.mask.g > 0.5)
                {
                    lit = _Plaza.rgb; shade = _PlazaShade.rgb; rock = false;
                    float2 slab = abs(frac(p.xz / 4.0) - 0.5);
                    if (max(slab.x, slab.y) > 0.485) lit = _PlazaLine.rgb;
                }

                float3 col = pw_band(cel, lit, shade);

                // rock faces painted in strata, like the layered cliffs in a background plate
                if (rock && frac(p.y / 11 + breakup * 1.6) < 0.22)
                    col = lerp(col, _RockShade.rgb * 0.8, 0.5);
                return fixed4(pw_fog(col, p), 1);
            }
            ENDCG
        }
    }
}
