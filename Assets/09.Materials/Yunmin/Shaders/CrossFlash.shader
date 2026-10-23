Shader "JuicyFresh/Effects/CrossFlash"
{
    Properties
    {
        [HDR] _EffectColor ("Effect Color", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Float) = 4
        _Opacity ("Opacity", Range(0, 1)) = 1
        _LineWidth ("Line Width", Range(0.005, 0.25)) = 0.035
        _ArmFalloff ("Arm Falloff", Range(0.2, 8)) = 2
        _HorizontalStretch ("Horizontal Stretch", Float) = 1
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
            Name "CrossFlash"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha One, Zero One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EffectColor;
                float _Brightness;
                float _Opacity;
                float _LineWidth;
                float _ArmFalloff;
                float _HorizontalStretch;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centeredUV = input.uv * 2.0 - 1.0;
                // Compensate for the wider quad to preserve vertical beam thickness and core shape.
                float2 shapeUV = centeredUV * float2(max(1.0, _HorizontalStretch), 1.0);
                float2 width = max(float2(_LineWidth, _LineWidth), fwidth(shapeUV));
                float2 beam = exp2(-2.0 * shapeUV * shapeUV / (width * width));
                float2 armFade = pow(saturate(1.0 - abs(centeredUV)), _ArmFalloff);
                float crossIntensity = max(beam.x * armFade.y, beam.y * armFade.x);
                float core = exp2(-24.0 * dot(shapeUV, shapeUV)) * 0.15;
                float edgeFade = 1.0 - smoothstep(0.9, 1.0, max(abs(centeredUV.x), abs(centeredUV.y)));
                float alpha = saturate((crossIntensity + core) * edgeFade) * _Opacity * _EffectColor.a;
                return half4(_EffectColor.rgb * _Brightness, alpha);
            }
            ENDHLSL
        }
    }
}
