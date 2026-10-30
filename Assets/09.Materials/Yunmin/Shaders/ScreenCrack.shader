// 빈사 화면 깨짐 풀스크린 셰이더입니다. URP Full Screen Pass Renderer Feature(Fetch Color Buffer)에 연결합니다.
// 값은 ScreenCrackEffect_m이 전역 셰이더 프로퍼티로 넣어 머티리얼 에셋이 런타임에 바뀌지 않습니다.
// 충격 지점 기준 로그-극좌표 공간의 Voronoi 셀을 유리 조각으로 사용해 중심일수록 잘게 깨집니다.
Shader "JuicyFresh/Effects/ScreenCrack"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "ScreenCrack"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // x: 진행도(0~1)
            float _ScreenCrackAmount;
            // xy: 충격 지점 UV, z: 시드, w: 최대 퍼짐 반경(화면 높이 기준)
            float4 _ScreenCrackCenter;
            // x: 방사 조각 수, y: 고리 밀도, z: 금 두께, w: 금 주변 굴절 폭
            float4 _ScreenCrackShape;
            // x: 조각 어긋남 세기, y: 금 주변 굴절 세기, z: 색수차, w: 충격 지점 반경
            float4 _ScreenCrackDistortion;
            float4 _ScreenCrackLineColor;

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float2 WrapCell(float2 cell, float period)
            {
                // 각도 방향 이음새가 끊기지 않도록 x를 조각 수로 감쌉니다.
                cell.x -= period * floor(cell.x / period);
                return cell;
            }

            float2 CellPoint(float2 cell, float period, float seed)
            {
                return 0.15 + 0.7 * Hash22(WrapCell(cell, period) + seed * 17.13);
            }

            // 경계까지의 거리와 가장 가까운 셀 정보를 구합니다 (Inigo Quilez의 Voronoi 경계 거리).
            float CrackVoronoi(float2 x, float period, float seed, out float2 cellId, out float2 toCenter)
            {
                float2 n = floor(x);
                float2 f = frac(x);

                float2 mg = 0;
                float2 mr = 0;
                float md = 8.0;
                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2(i, j);
                        float2 r = g + CellPoint(n + g, period, seed) - f;
                        float d = dot(r, r);
                        if (d < md)
                        {
                            md = d;
                            mr = r;
                            mg = g;
                        }
                    }
                }

                md = 8.0;
                [unroll]
                for (int y = -2; y <= 2; y++)
                {
                    [unroll]
                    for (int x2 = -2; x2 <= 2; x2++)
                    {
                        float2 g = mg + float2(x2, y);
                        float2 r = g + CellPoint(n + g, period, seed) - f;
                        float2 diff = r - mr;
                        if (dot(diff, diff) > 0.00001)
                        {
                            md = min(md, dot(0.5 * (mr + r), normalize(diff)));
                        }
                    }
                }

                cellId = n + mg;
                toCenter = mr;
                return md;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float amount = saturate(_ScreenCrackAmount);
                if (amount <= 0.0)
                {
                    return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                }

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float seed = _ScreenCrackCenter.z;
                float sectors = max(3.0, round(_ScreenCrackShape.x));
                float ringDensity = max(0.1, _ScreenCrackShape.y);

                // 화면 높이 기준 좌표로 바꿔 가로세로 비율과 무관하게 둥글게 퍼지게 합니다.
                float2 p = (uv - _ScreenCrackCenter.xy) * float2(aspect, 1.0);
                float radius = max(length(p), 1e-4);
                float2 radial = p / radius;
                float2 tangent = float2(-radial.y, radial.x);
                float angle01 = atan2(p.y, p.x) / TWO_PI + 0.5;

                // 로그-극좌표: 각도 한 바퀴가 조각 수만큼, 반경은 로그 스케일이라 중심일수록 셀이 작아집니다.
                float logScale = sectors / TWO_PI;
                float2 g = float2(angle01 * sectors, log(radius + 0.01) * logScale * ringDensity);

                float2 cellId;
                float2 toCenter;
                float edgeG = CrackVoronoi(g, sectors, seed, cellId, toCenter);

                // 로그-극좌표 1단위가 화면에서 차지하는 길이로 환산합니다.
                float unitToScreen = (radius + 0.01) / logScale;
                float edge = edgeG * unitToScreen;

                // 셀 중심 반경이 퍼짐 반경 안에 들어온 조각만 깨집니다.
                float2 cellCenterG = cellId + CellPoint(cellId, sectors, seed);
                float cellRadius = exp(cellCenterG.y / (logScale * ringDensity)) - 0.01;
                float spread = amount * _ScreenCrackCenter.w;
                float shardReveal = saturate((spread - cellRadius) / 0.05);
                float lineReveal = saturate((spread - radius) / 0.02);

                // 조각마다 다른 방향으로 살짝 어긋나 보이게 합니다.
                float2 shardOffset = (Hash22(WrapCell(cellId, sectors) + seed * 3.7 + 11.0) - 0.5) * 2.0 * _ScreenCrackDistortion.x;

                // 금 근처는 경계 쪽으로 굴절돼 유리 단면처럼 휘어 보입니다.
                float2 bevelDirG = -toCenter;
                float2 bevelDir = bevelDirG.x * tangent + bevelDirG.y * radial;
                bevelDir = dot(bevelDir, bevelDir) > 1e-8 ? normalize(bevelDir) : float2(0.0, 0.0);
                float bevel = 1.0 - smoothstep(0.0, max(_ScreenCrackShape.w, 1e-4), edge);
                float2 bevelOffset = bevelDir * bevel * _ScreenCrackDistortion.y;

                float2 offset = (shardOffset * shardReveal + bevelOffset * lineReveal) * float2(1.0 / aspect, 1.0);
                float2 chroma = bevelDir * bevel * lineReveal * _ScreenCrackDistortion.z * float2(1.0 / aspect, 1.0);

                float2 baseUV = uv + offset;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, baseUV);
                color.r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, baseUV + chroma).r;
                color.b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, baseUV - chroma).b;

                // 금 선: 가는 밝은 선 + 조금 넓은 어두운 테두리.
                float lineWidth = max(_ScreenCrackShape.z, 1e-5);
                float crackCore = 1.0 - smoothstep(lineWidth * 0.35, lineWidth, edge);
                float crackShadow = 1.0 - smoothstep(lineWidth, lineWidth * 3.0, edge);
                color.rgb *= 1.0 - crackShadow * 0.35 * lineReveal;
                color.rgb = lerp(color.rgb, _ScreenCrackLineColor.rgb, crackCore * _ScreenCrackLineColor.a * lineReveal);

                // 충격 지점은 잘게 부서져 하얗게 뭉개집니다.
                float impactRadius = max(_ScreenCrackDistortion.w, 1e-4);
                float impact = (1.0 - smoothstep(0.0, impactRadius, radius)) * lineReveal;
                color.rgb = lerp(color.rgb, _ScreenCrackLineColor.rgb, impact * 0.6 * _ScreenCrackLineColor.a);

                return color;
            }
            ENDHLSL
        }
    }
}
