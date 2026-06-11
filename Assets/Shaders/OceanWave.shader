Shader "Custom/OceanWave"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _MaskTex ("Land Mask (R=land, G=shoreDist)", 2D) = "white" {}
        _WaveColor1 ("Wave Color Light", Color) = (0.3, 0.5, 0.7, 0.2)
        _WaveColor2 ("Wave Color Dark", Color) = (0.05, 0.12, 0.25, 0.15)
        _FoamColor ("Foam Color", Color) = (0.8, 0.9, 1.0, 0.3)
        _WaveScale1 ("Wave Scale 1", Float) = 8.0
        _WaveScale2 ("Wave Scale 2", Float) = 15.0
        _WaveSpeed1 ("Wave Speed 1", Vector) = (0.06, 0.04, 0, 0)
        _WaveSpeed2 ("Wave Speed 2", Vector) = (-0.04, 0.06, 0, 0)
        _FoamScale ("Foam Scale", Float) = 25.0
        _FoamSpeed ("Foam Speed", Vector) = (0.01, -0.015, 0, 0)
        _FoamThreshold ("Foam Threshold", Range(0.5, 0.95)) = 0.72
        _Intensity ("Overall Intensity", Range(0, 1)) = 0.25
        _GameTime ("Game Time", Float) = 0.0

        // Kıyı dalgası
        _ShoreWaveIntensity ("Shore Wave Intensity", Range(0, 1)) = 0.55
        _ShoreWaveColor ("Shore Wave Color", Color) = (0.85, 0.92, 1.0, 0.5)
        _ShoreWaveSpeed ("Shore Wave Speed", Float) = 1.5
        _ShoreWaveFrequency ("Shore Wave Frequency", Float) = 3.0
        _ShoreFoamIntensity ("Shore Foam Intensity", Range(0, 1)) = 0.7
    }

    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MaskTex;
            float4 _WaveColor1;
            float4 _WaveColor2;
            float4 _FoamColor;
            float _WaveScale1;
            float _WaveScale2;
            float4 _WaveSpeed1;
            float4 _WaveSpeed2;
            float _FoamScale;
            float4 _FoamSpeed;
            float _FoamThreshold;
            float _Intensity;
            float _GameTime;

            float _ShoreWaveIntensity;
            float4 _ShoreWaveColor;
            float _ShoreWaveSpeed;
            float _ShoreWaveFrequency;
            float _ShoreFoamIntensity;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float2 shift = float2(100, 100);
                for (int i = 0; i < 4; i++)
                {
                    v += amp * noise(p);
                    p = p * 2.0 + shift;
                    amp *= 0.5;
                }
                return v;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // mask: R=kara(1)/su(0), G=kıyı mesafesi (0=kıyı, 1=açık deniz)
                fixed4 mask = tex2D(_MaskTex, i.uv);
                float waterMask = 1.0 - mask.r;
                float shoreDist = mask.g;

                if (waterMask < 0.01) return fixed4(0, 0, 0, 0);

                float t = _GameTime;

                // === AÇIK DENİZ DALGALARI (mevcut sistem) ===
                float2 waveUV1 = i.uv * _WaveScale1 + _WaveSpeed1.xy * t;
                float wave1 = fbm(waveUV1);

                float2 waveUV2 = i.uv * _WaveScale2 + _WaveSpeed2.xy * t;
                float wave2 = fbm(waveUV2);

                float waveMix = wave1 * 0.6 + wave2 * 0.4;
                fixed4 waveCol = lerp(_WaveColor1, _WaveColor2, waveMix);

                // açık deniz köpüğü
                float2 foamUV = i.uv * _FoamScale + _FoamSpeed.xy * t;
                float foam = fbm(foamUV);
                float foamMask = smoothstep(_FoamThreshold, 1.0, foam);
                fixed4 foamCol = _FoamColor * foamMask;

                fixed4 openOcean = waveCol + foamCol;
                openOcean.a *= waterMask * _Intensity;

                // === KIYIYA VURAN DALGA ===
                // shoreDist < 1 olan bölgede aktif (kıyıya yakın su)
                float shoreZone = 1.0 - shoreDist; // 1=kıyı, 0=açık deniz

                if (shoreZone < 0.01) return openOcean;

                // noise ile dalga hattına doğallık ekle
                float edgeNoise = noise(i.uv * 40.0) * 0.15;

                // kıyıya doğru hareket eden dalga bantları
                // shoreDist'i frekansla çarpıp zamanda kaydırarak sahile doğru ilerleyen dalga
                float wavePhase = shoreDist * _ShoreWaveFrequency + t * _ShoreWaveSpeed;
                float shoreWave = sin(wavePhase * 6.2831853) * 0.5 + 0.5;

                // dalga bandını daralt — daha gerçekçi çizgi efekti
                shoreWave = pow(shoreWave, 3.0);

                // açık denize doğru dalga yumuşak sönümlensin, kıyıda tam güçte kalsın
                float shoreAtten = smoothstep(0.0, 0.15, shoreZone);
                shoreWave *= shoreAtten;

                // en kıyıdaki sürekli köpük bandı (shoreDist 0-0.25 arası)
                float surfFoam = smoothstep(0.25, 0.0, shoreDist);
                // köpük hattına noise ile düzensizlik
                surfFoam *= smoothstep(-0.05, 0.05, surfFoam - edgeNoise);

                // kıyı dalga rengi
                float shoreAlpha = shoreWave * _ShoreWaveIntensity + surfFoam * _ShoreFoamIntensity;
                shoreAlpha += edgeNoise * shoreZone * 0.3;
                shoreAlpha = saturate(shoreAlpha);

                fixed4 shoreCol = _ShoreWaveColor;
                shoreCol.a = shoreAlpha * waterMask;

                // açık deniz + kıyı dalgasını birleştir
                // kıyıya yakın bölgede kıyı dalgası baskın, açıkta açık deniz dalgası baskın
                fixed4 result;
                result.rgb = lerp(openOcean.rgb, shoreCol.rgb, shoreZone * shoreAlpha);
                result.a = max(openOcean.a, shoreCol.a);

                return result;
            }
            ENDCG
        }
    }
}
