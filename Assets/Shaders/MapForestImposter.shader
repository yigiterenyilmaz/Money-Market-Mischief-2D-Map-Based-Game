// Forest imposter (bake edilmiş orman RT'si) display shader'ı.
//
// NEDEN ÖZEL SHADER: Sprite'lar RT'ye Unity sprite pipeline'ıyla çizilir → RT'nin RGB'si
// PREMULTIPLIED (renk*alpha), alpha'sı düz (straight) birikir. Bu RT'yi Sprites/Default ile
// gösterirsen shader rgb'yi alpha ile BİR KEZ DAHA çarpar → yumuşak kanopi kenarları (düşük
// alpha pikselleri) gerçek ağaçlardan belirgin KOYU görünür. Burada premultiplied veri olduğu
// gibi Blend One OneMinusSrcAlpha ile basılır; _Color.a crossfade için rgb ve a'yı BİRLİKTE
// ölçekler (premultiplied fade'in doğru hali).
Shader "Custom/MapForestImposter"
{
    Properties
    {
        _MainTex ("Baked Forest RT", 2D) = "white" {}
        _Color   ("Tint (a = crossfade)", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.texcoord.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv); // rgb premultiplied, a straight
                c.rgb *= _Color.rgb * _Color.a;   // premultiplied fade: rgb ve a birlikte
                c.a   *= _Color.a;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
