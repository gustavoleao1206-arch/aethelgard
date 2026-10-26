// Skybox de GRADIENTE (URP) — céu noturno teal de Aethelgard (fase de arte).
// Gradiente vertical topo→horizonte→base pela direção de visão. Combina com a névoa
// escura e faz os acentos de Ressonância brilharem contra um céu de mistério.
Shader "JogoDaTrupe/SkyboxGradient"
{
    Properties
    {
        _TopColor     ("Topo (zênite)",  Color) = (0.02, 0.05, 0.09, 1)
        _HorizonColor ("Horizonte",      Color) = (0.10, 0.19, 0.21, 1)
        _BottomColor  ("Base (nadir)",   Color) = (0.02, 0.04, 0.05, 1)
        _Exponent     ("Curva do gradiente", Range(0.2, 5)) = 1.3
    }

    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _TopColor;
            float4 _HorizonColor;
            float4 _BottomColor;
            float  _Exponent;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir        = IN.positionOS.xyz; // no skybox, a posição do vértice = direção
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float h = normalize(IN.dir).y; // -1 (base) .. +1 (topo)
                float3 col = (h >= 0.0)
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb,    pow(saturate( h), _Exponent))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-h), _Exponent));
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
