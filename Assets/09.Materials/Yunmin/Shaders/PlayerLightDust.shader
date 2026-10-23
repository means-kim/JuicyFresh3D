Shader "JuicyFresh/Effects/PlayerLightDust"
{
    Properties
    {
        [HDR] _EffectColor ("Effect Color", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Float) = 2.5
        _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PlayerLightDust"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

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
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 centeredUV = input.uv * 2.0 - 1.0;
                float distanceSquared = dot(centeredUV, centeredUV);
                float softCore = exp2(-4.5 * distanceSquared);
                float edgeFade = 1.0 - smoothstep(0.65, 1.0, distanceSquared);
                float alpha = softCore * edgeFade * input.color.a
                    * saturate(_Opacity) * saturate(_EffectColor.a);
                half3 color = input.color.rgb * _EffectColor.rgb * max(0.0, _Brightness);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
