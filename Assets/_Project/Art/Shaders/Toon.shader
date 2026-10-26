// Shader TOON / cel-shaded (URP) — mockup de estilo da fase de arte (Jogo da Trupe).
// Passe único (mesmo esqueleto comprovado do FlatLowPoly): sombreamento em FAIXAS
// (bandas), luz de borda (rim) e emissão para o brilho de Ressonância. Casa com o tom
// "mistério de anime". Contorno (inverted-hull) fica como polimento futuro — o look
// cel-shaded já vem das bandas + rim + acentos que brilham.
Shader "JogoDaTrupe/Toon"
{
    Properties
    {
        [MainColor] _BaseColor ("Cor base", Color) = (1,1,1,1)
        _Steps          ("Faixas de sombra", Range(2,5)) = 3
        _ShadowTint     ("Tom da sombra", Color) = (0.42,0.46,0.58,1)
        _AmbientStrength("Ambiente", Range(0,1.5)) = 0.55
        _RimColor       ("Cor da borda", Color) = (0.55,0.9,1.0,1)
        _RimPower       ("Foco da borda", Range(0.5,8)) = 3.0
        _RimStrength    ("Força da borda", Range(0,2)) = 0.6
        _Emission       ("Emissão (Ressonância)", Color) = (0,0,0,1)
        _OutlineColor   ("Cor do contorno", Color) = (0.035,0.045,0.07,1)
        _OutlineWidth   ("Largura do contorno (m)", Range(0,0.3)) = 0.05
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        // ---------- Passe de CONTORNO (inverted-hull radial do centro do objeto) ----------
        // Expande a casca a partir do CENTRO do objeto em espaço de MUNDO — como nossos
        // objetos são primitivos convexos CENTRADOS (cubo/cápsula/esfera/cilindro), isso dá
        // contorno uniforme e SEM fendas nos cantos (o problema do inverted-hull por normal),
        // com espessura constante em metros. Cull Front: renderiza só as costas expandidas,
        // que o teste de profundidade deixa aparecer só na silhueta.
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float  _OutlineWidth;
            float4 _OutlineColor;

            struct AttributesO { float4 positionOS : POSITION; };
            struct VaryingsO   { float4 positionHCS : SV_POSITION; float fogCoord : TEXCOORD0; };

            VaryingsO vertOutline(AttributesO IN)
            {
                VaryingsO OUT;
                float3 posWS    = TransformObjectToWorld(IN.positionOS.xyz);
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0)); // origem do objeto
                float3 dir      = posWS - centerWS;
                float  len      = max(length(dir), 1e-4);
                posWS += (dir / len) * _OutlineWidth; // expande radial (largura constante em m)
                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.fogCoord    = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 fragOutline(VaryingsO IN) : SV_Target
            {
                float3 col = MixFog(_OutlineColor.rgb, IN.fogCoord); // some junto na névoa
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float4 _BaseColor;
            float  _Steps;
            float4 _ShadowTint;
            float  _AmbientStrength;
            float4 _RimColor;
            float  _RimPower;
            float  _RimStrength;
            float4 _Emission;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float  fogCoord    : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = p.positionCS;
                OUT.positionWS  = p.positionWS;
                OUT.normalWS    = normalize(TransformObjectToWorldNormal(IN.normalOS));
                OUT.fogCoord    = ComputeFogFactor(p.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight();

                // Wrap-lighting suave e depois QUANTIZA em faixas (o "look" cel-shaded).
                float ndl   = saturate(dot(N, mainLight.direction) * 0.5 + 0.5);
                float steps = max(_Steps, 2.0);
                float band  = saturate(floor(ndl * steps) / (steps - 1.0));

                float3 baseCol   = _BaseColor.rgb;
                float3 shadowCol = baseCol * _ShadowTint.rgb;
                float3 lightCol  = baseCol * mainLight.color;
                float3 col       = lerp(shadowCol, lightCol, band);

                col += baseCol * SampleSH(N) * _AmbientStrength;

                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
                float  rim     = pow(saturate(1.0 - dot(N, viewDir)), _RimPower);
                col += _RimColor.rgb * rim * _RimStrength;

                col += _Emission.rgb;
                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
