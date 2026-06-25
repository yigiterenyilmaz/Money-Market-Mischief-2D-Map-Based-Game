// Per-pixel "ray traced" long-shadow smear for MapDecorPlacer flat (Trace) shadows.
//
// Each opaque sprite pixel casts a dark streak that STARTS at that pixel's own position and extends
// along the light direction (_SmearDir). The union of all streaks is the shadow, so a shadow
// segment's base is exactly where that part of the building is.
//
// Two refinements:
//   * Stray-pixel removal (erosion): a caster only counts if enough of its NEIGHBORHOOD is opaque.
//     Thin/isolated bits (smoke wisps detached from the body) drop out; the solid body survives.
//   * Soft edges (blur): we read neighborhood COVERAGE (0..1) and ramp alpha with smoothstep between
//     _EdgeLow.._EdgeHigh instead of a hard on/off. Bigger _BlurRadius = softer.
Shader "Custom/MapShadowSmear"
{
    Properties
    {
        _MainTex    ("Sprite Atlas", 2D) = "white" {}
        _Color      ("Shadow Color", Color) = (0,0,0,0.35)
        _UvRect     ("UV SubRect", Vector) = (0,0,1,1)
        _SmearDir   ("Smear Dir (UV)", Vector) = (0,0,0,0)
        _Steps      ("March Steps", Float) = 48
        _Cutoff     ("Alpha Cutoff", Float) = 0.1
        _BlurRadius ("Blur Radius (texels)", Float) = 1.2
        _EdgeLow    ("Coverage Low (stray cut)", Float) = 0.35
        _EdgeHigh   ("Coverage High (solid)", Float) = 0.8
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // (1/w, 1/h, w, h) — set automatically by Unity
            float4 _Color;
            float4 _UvRect;
            float4 _SmearDir;
            float  _Steps;
            float  _Cutoff;
            float  _BlurRadius;
            float  _EdgeLow;
            float  _EdgeHigh;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // Fraction of a 3x3 neighborhood (radius _BlurRadius texels) that is opaque. Doubles as
            // erosion (thin features → low) and as a soft-edge ramp (boundary → partial).
            float Coverage (float2 uv, float2 rMin, float2 rMax)
            {
                float2 tx = _MainTex_TexelSize.xy * _BlurRadius;
                float sum = 0.0;
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 s = uv + float2(x, y) * tx;
                        if (s.x < rMin.x || s.x > rMax.x || s.y < rMin.y || s.y > rMax.y)
                            continue; // outside sub-rect = empty
                        sum += (tex2D(_MainTex, s).a > _Cutoff) ? 1.0 : 0.0;
                    }
                }
                return sum / 9.0;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 rMin = _UvRect.xy;
                float2 rMax = _UvRect.xy + _UvRect.zw;

                int steps = (int)max(1.0, _Steps);
                float bestCov = 0.0;

                // March back along the smear direction. Evaluate neighborhood coverage only where the
                // center is opaque (cheap elsewhere). Take the max so a solid body wins over a stray
                // pixel further along the same ray.
                [loop]
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    float2 sUV = i.uv - _SmearDir.xy * t;
                    if (sUV.x < rMin.x || sUV.x > rMax.x || sUV.y < rMin.y || sUV.y > rMax.y)
                        continue;

                    if (tex2D(_MainTex, sUV).a > _Cutoff)
                    {
                        float cov = Coverage(sUV, rMin, rMax);
                        bestCov = max(bestCov, cov);
                        if (bestCov >= _EdgeHigh) break;
                    }
                }

                float a = _Color.a * smoothstep(_EdgeLow, _EdgeHigh, bestCov);
                if (a <= 0.003) discard;
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
