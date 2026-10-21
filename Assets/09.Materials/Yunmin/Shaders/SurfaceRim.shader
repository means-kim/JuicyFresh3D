Shader "JuicyFresh/Effects/SilhouetteOutline"
{
    Properties
    {
        [HDR] _EffectColor ("Effect Color", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Float) = 3
        _Opacity ("Opacity", Range(0, 1)) = 0
        _OutlineWidth ("Outline Width (Pixels)", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SilhouetteOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Front

            // All original silhouettes are masked before any expanded hull is drawn.
            // Only use the lowest user stencil bit; preserve URP's deferred stencil bits.
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 0
                Comp NotEqual
                Pass Keep
            }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;
                float _Brightness;
                float _Opacity;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;

                // Project the skinned normal, including perspective, then expand in pixels.
                float4 normalCS = mul(UNITY_MATRIX_VP, float4(normalInputs.normalWS, 0.0));
                float2 projectedNormal = normalCS.xy * output.positionCS.w
                    - output.positionCS.xy * normalCS.w;
                float2 screenSize = max(_ScaledScreenParams.xy, float2(1.0, 1.0));
                float2 pixelDirection = projectedNormal * screenSize;
                pixelDirection *= rsqrt(max(dot(pixelDirection, pixelDirection), 1e-6));
                output.positionCS.xy += pixelDirection * (2.0 * max(0.0, _OutlineWidth) / screenSize)
                    * output.positionCS.w;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float alpha = saturate(_Opacity) * saturate(_EffectColor.a);
                return half4(_EffectColor.rgb * max(0.0, _Brightness), alpha);
            }
            ENDHLSL
        }
    }
}
