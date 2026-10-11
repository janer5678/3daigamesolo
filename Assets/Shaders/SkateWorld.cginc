// Shared bits for every shader in the world. Lifted from J's World's Pururin world (the look
// at 0:11 of the reference video): flat cel colour in hard bands, pastel everything, an ink
// line, and pink-white haze that thickens with distance and thins with height.
//
// Nothing is lit by a Unity light. The "sun" is a direction every shader agrees on, and the
// shading is a hard step on that, like a painted anime background.
#ifndef SKATE_WORLD_INCLUDED
#define SKATE_WORLD_INCLUDED

float4 _SunDir;
float4 _Haze;
float4 _HazeHigh;
float  _FogDensity;
float  _FogHeight;
float  _FogMax;

float pw_hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float pw_noise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = pw_hash(i);
    float b = pw_hash(i + float2(1, 0));
    float c = pw_hash(i + float2(0, 1));
    float d = pw_hash(i + float2(1, 1));
    float2 u = f * f * (3 - 2 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float pw_fbm(float2 p)
{
    float s = 0, a = 0.5;
    for (int k = 0; k < 4; k++) { s += a * pw_noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
    return s;
}

// 1 lit, 0.5 the in-between band, 0 shadow. Hard steps, no gradient - that is the style.
float pw_cel(float3 n)
{
    float ndl = dot(normalize(n), normalize(_SunDir.xyz));
    return ndl > 0.3 ? 1.0 : (ndl > -0.12 ? 0.5 : 0.0);
}

float3 pw_band(float cel, float3 lit, float3 shade)
{
    return cel > 0.75 ? lit : (cel > 0.25 ? lerp(shade, lit, 0.5) : shade);
}

float pw_fogAmount(float3 posWS)
{
    float dist = distance(posWS, _WorldSpaceCameraPos);
    float h = max(posWS.y, 0);
    float dens = _FogDensity * exp(-h / max(_FogHeight, 1));
    return (1 - exp(-dist * dens)) * _FogMax;
}

float3 pw_hazeColour(float3 posWS)
{
    return lerp(_Haze.rgb, _HazeHigh.rgb, saturate(posWS.y / 450));
}

float3 pw_fog(float3 col, float3 posWS)
{
    return lerp(col, pw_hazeColour(posWS), pw_fogAmount(posWS));
}

#endif
