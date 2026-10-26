// Partícula que BRILHA (URP, aditiva) — VFX de habilidades/Ecos (fase de arte).
// Círculo suave PROCEDURAL (sem textura) tingido por _Color (do MATERIAL — a fábrica cria
// um material por cor). NÃO depende da cor de vértice do ParticleSystem (que pode não
// chegar a um material custom) — por isso o glow aparece de forma confiável. Blend aditivo
// → é realçado pelo bloom. Sem névoa (o brilho não deve escurecer com a distância).
Shader "JogoDaTrupe/ParticleGlow"
{
    Properties
    {
        _Color    ("Cor", Color) = (1, 1, 1, 1)
        _Softness ("Suavidade da borda", Range(0.01, 1)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True"
        }
        Blend SrcAlpha One      // aditivo
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Color;
            float  _Softness;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float d = distance(IN.uv, float2(0.5, 0.5)) * 2.0; // 0 centro .. 1 borda
                float a = 1.0 - smoothstep(1.0 - _Softness, 1.0, d); // círculo suave
                a *= saturate(1.0 - d);                              // falloff extra p/ glow macio
                return half4(_Color.rgb, _Color.a * a);              // Blend SrcAlpha One = aditivo
            }
            ENDHLSL
        }
    }
}
