// Shader STYLIZED LOW-POLY (URP) — mockup de estilo da fase de arte (Jogo da Trupe).
// Sombreamento FACETADO: a normal de cada triângulo é derivada da posição em tela
// (ddx/ddy), então mesmo os primitivos lisos (cápsula/cilindro/esfera) ganham o
// visual de facetas de baixa contagem de polígonos. Luz difusa suave + ambiente
// generoso = aquele ar limpo, colorido e leve do low-poly estilizado.
Shader "JogoDaTrupe/FlatLowPoly"
{
    Properties
    {
        _BaseColor      ("Cor base", Color) = (1,1,1,1)
        _AmbientStrength("Ambiente", Range(0,1.5)) = 0.55
        _LightBoost     ("Ganho da luz", Range(0.5,2)) = 1.1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "FlatForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _BaseColor;
            float  _AmbientStrength;
            float  _LightBoost;

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float  fogCoord    : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.fogCoord    = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Normal FACETADA: derivadas de tela da posição em mundo → normal do
                // triângulo. Orienta para a câmera (remove ambiguidade de sinal).
                float3 N = normalize(cross(ddy(IN.positionWS), ddx(IN.positionWS)));
                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
                if (dot(N, viewDir) < 0.0) N = -N;

                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(N, mainLight.direction));

                float3 baseCol = _BaseColor.rgb;
                float3 col = baseCol * mainLight.color * ndotl * _LightBoost;
                col += baseCol * SampleSH(N) * _AmbientStrength;
                col += baseCol * _AmbientStrength * 0.35; // piso ambiente uniforme (leve)

                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
