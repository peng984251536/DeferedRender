Shader "Unlit/MyDefferedShader02"
{
    Properties
    {
        _MainTex("MainTex",2D) = "while"{}

        _AlbedoColor("albedo_color",color)=(1,1,1,1)
        _SpecularColor("SpecularColor",color)=(1,1,1,1)

        _NormalMap ("NormalMap", 2D) = "white" {}
        _normalScale("NormalScale",Range(0,1)) = 1
        
        _MySmoothness("smoothness",Range(0,1)) = 0
        _MyMetallic("metallic",Range(0,1)) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        //暂时把 高光度信息理解为 rgb格式的金属度
        //oneMinusReflectivity = kDielectricSpec.a * (1-metallic)    --金属度越高，反射出去的能量就越低
        //reflectivity =1.0 - kDielectricSpec.a * (1-metallic)    --控制反射能量的参数,金属度越高可以反射的能量越高
        //brdfDiffuse = albedo * oneMinusReflectivity--金属度越高，直接反射的能量就越低
        //brdfSpecular = lerp(kDieletricSpec.rgb, albedo, metallic)--金属度越高，直接反射的能量就越高
        //grazingTerm = saturate(smoothness + reflectivity)
        half3 MyEnvironmentBRDF(float3 indirectDiffuse, float3 indirectSpecular, half diffuseTerm, half3 specularTerm,
                                float roughness, float fresnelTerm, float grazingTerm)
        {
            half3 diffuse = indirectDiffuse * diffuseTerm;

            float surfaceReduction = 1.0 / (roughness * roughness + 1.0);
            surfaceReduction = half3(surfaceReduction * lerp(specularTerm, grazingTerm, fresnelTerm));
            half3 specular = indirectSpecular * surfaceReduction;

            return diffuse + specular;
        }

        half3 MyGlossyEnvironmentReflection(half3 reflectVector, float3 positionWS, half perceptualRoughness,
                                            half occlusion, float2 normalizedScreenSpaceUV)
        {
            #if !defined(_ENVIRONMENTREFLECTIONS_OFF)
            half3 irradiance;

            #if defined(_REFLECTION_PROBE_BLENDING) || USE_FORWARD_PLUS
            irradiance = CalculateIrradianceFromReflectionProbes(reflectVector, positionWS, perceptualRoughness, normalizedScreenSpaceUV);
            #else
            #ifdef _REFLECTION_PROBE_BOX_PROJECTION
            reflectVector = BoxProjectedCubemapDirection(reflectVector, positionWS, unity_SpecCube0_ProbePosition, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax);
            #endif // _REFLECTION_PROBE_BOX_PROJECTION
            half mip = PerceptualRoughnessToMipmapLevel(perceptualRoughness);
            half4 encodedIrradiance = half4(
                SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVector, mip));

            irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
            #endif // _REFLECTION_PROBE_BLENDING
            return irradiance * occlusion;
            #else
            return _GlossyEnvironmentColor.rgb * occlusion;
            #endif // _ENVIRONMENTREFLECTIONS_OFF
        }
        ENDHLSL

        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "GBuffer"
            Tags
            {
                "LightMode" = "UniversalGBuffer"
            }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP //是否使用法线贴图
            #pragma shader_feature_local_fragment _ALPHATEST_ON //是否开启alpha测试
            //#pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            //#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            //#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON

            #pragma vertex MyGBufferVertex
            #pragma fragment MyGBufferFragment


            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            float4 _MainTex_ST;
            float4 _NormalMap_ST;
            float4 _AlbedoColor;
            float4 _SpecularColor;
            float _normalScale;
            float _MySmoothness;
            float _MyMetallic;

            BRDFData Init_BRDFData(half3 albedo, float metallic, float smoothness)
            {
                BRDFData brdf_data=(BRDFData)0;
                brdf_data.albedo = albedo;
                half oneMinusReflectivity = kDielectricSpec.a * (1 - metallic); //非直接反射的能量比例
                brdf_data.diffuse = albedo * kDielectricSpec.a * (1 - metallic); //漫反射的贡献度
                brdf_data.specular = lerp(kDieletricSpec.rgb, albedo, metallic); //镜面反射贡献度
                brdf_data.reflectivity = 1 - oneMinusReflectivity;
                brdf_data.roughness = pow(1 - smoothness, 2); //todo-为啥要把粗糙度平方

                brdf_data.roughness2 = pow(brdf_data.roughness, 2);
                brdf_data.grazingTerm = saturate(smoothness + brdf_data.reflectivity);
                brdf_data.normalizationTerm = brdf_data.roughness * half(4.0) + half(2.0);
                brdf_data.roughness2MinusOne = brdf_data.roughness2 - half(1.0);

                return brdf_data;
            }

            struct MyAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 texcoord : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1; //静态光照贴图
                float2 dynamicLightmapUV : TEXCOORD2; //动态光照贴图
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct MyVaryings
            {
                float2 uv : TEXCOORD0;

                #if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
                float3 positionWS : TEXCOORD1;
                #endif

                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3; // xyz: tangent, w: sign

                #ifdef _ADDITIONAL_LIGHTS_VERTEX
    half3 vertexLighting            : TEXCOORD4;    // xyz: vertex lighting
                #endif

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord              : TEXCOORD5;
                #endif

                #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                half3 viewDirTS : TEXCOORD6;
                #endif

                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
                #ifdef DYNAMICLIGHTMAP_ON
                float2  dynamicLightmapUV       : TEXCOORD8; // Dynamic lightmap UVs
                #endif

                float3 viewDirWS:TEXCOORD9;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };


            // Used in Standard (Physically Based) shader
            MyVaryings MyGBufferVertex(MyAttributes input)
            {
                MyVaryings output = (MyVaryings)0;

                //实例化
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                //利用unity的函数获取一组顶点相关的信息
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

                // normalWS and tangentWS already normalize.
                // this is required to avoid skewing the direction during interpolation
                // also required for per-vertex lighting and SH evaluation
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);

                // already normalized from normal transform to WS.
                output.normalWS = normalInput.normalWS;

                //正确获取烘焙贴图的uv坐标
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                #ifdef DYNAMICLIGHTMAP_ON
    output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif
                //计算球协函数
                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);


                real sign = input.tangentOS.w * GetOddNegativeScale();
                half4 tangentWS = half4(normalInput.tangentWS.xyz, sign);


                //切线
                output.tangentWS = tangentWS;

                //世界空间坐标
                output.positionWS = vertexInput.positionWS;

                //世界空间视图坐标
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);

                //阴影相关
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                output.positionCS = vertexInput.positionCS;
                return output;
            }
            //Deferred Directional
            // 在片元着色器，需要获取的GBuffer有
            // baseColor + mask
            // normal（rg） + meta（b） + rougness （a）
            // GI(间接光)
            //模板buffer
            //深度buffer
            // --在后面的渲染中还需要：1、SSR，2、GTAO和阴影混合
            FragmentOutput MyGBufferFragment(MyVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 normalMap = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv);
                float4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                //基础色
                //利用接口做alpah测试,在当前材质设置
                Alpha(baseColor.a, _AlbedoColor, _Cutoff);
                baseColor.rgb = baseColor.rgb * _AlbedoColor.rgb;

                //法线
                float3 bitangent = input.tangentWS.w * cross(input.normalWS.xyz, input.tangentWS.xyz);
                float3 normalTS;
                normalTS.xy = normalMap.xy * 2 - 1;
                normalTS.xy *= _normalScale;
                normalTS.z = sqrt(1 - saturate(dot(normalTS.xy, normalTS.xy)));
                float3 normalWS = TransformTangentToWorld(normalTS,
                                                          half3x3(input.tangentWS.xyz, bitangent.xyz,
                                                                  input.normalWS.xyz));
                float2 n = PackNormalOctRectEncode(normalWS); //对法线贴图进行压缩


                //PBR相关信息
                half smoothness = 1 - normalMap.a;
                half metallic =  baseColor.a;


                //BRDFData
                BRDFData brdf_data = Init_BRDFData(baseColor.rgb, metallic, smoothness);


                //------------间接光-----------//
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
                #else
                float shadowCoord = float4(0, 0, 0, 0);
                #endif
                #if defined(DYNAMICLIGHTMAP_ON)
                    float3 bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
                #else
                float3 bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, input.normalWS);
                #endif
                float shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
                //获取主光源信息
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                //获取被烘焙的全局光
                MixRealtimeAndBakedGI(mainLight, input.normalWS, bakedGI, shadowMask);
                half3 GI_color = GlobalIllumination(brdf_data, bakedGI, 1,
                                                    input.positionWS, normalWS, input.viewDirWS);


                float3 specular = _SpecularColor.rgb*(1-(1-metallic)*kDielectricSpec.a) ;
                
                FragmentOutput output;
                output.GBuffer0 = half4(baseColor.rgb, 1); // diffuse           diffuse         diffuse   meta
                output.GBuffer1 = half4(specular*0.5, 1); // encoded-normal    encoded-normal  charInfo  smoothness
                output.GBuffer2 = half4(normalWS, smoothness); // GI                GI              GI        1
                output.GBuffer3 = half4(GI_color,1); // GI                GI              GI        1
                //output.GBuffer0 = half4(input.uv.x,input.uv.y,0,1);
                #if _RENDER_PASS_ENABLED
                    output.GBuffer4 = inputData.positionCS.z;
                #endif
                #if OUTPUT_SHADOWMASK
                     output.GBUFFER_SHADOWMASK = inputData.shadowMask; // will have unity_ProbesOcclusion value if subtractive lighting is used (baked)
                #endif
                #ifdef _WRITE_RENDERING_LAYERS
                    uint renderingLayers = GetMeshRenderingLayer();
                    output.GBUFFER_LIGHT_LAYERS = float4(EncodeMeshRenderingLayer(renderingLayers), 0.0, 0.0, 0.0);
                #endif

                //return float4(normalWS,1);
                //return float4(baseColor.rgb, 1);
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            // Lightmode matches the ShaderPassName set in UniversalRenderPipeline.cs. SRPDefaultUnlit and passes with
            // no LightMode tag are also rendered by Universal Render Pipeline
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            ZTest On
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM


            // -------------------------------------
            // Shader Stages
            #pragma vertex vert
            #pragma fragment frag
            

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _AlbedoColor;

            struct appdata
            {
                float4 vertex: POSITION;
                float3 normal:NORMAL;
                float2 texcoord :TEXCOORD0;
                float4 tangent:TANGENT;
            };

            struct v2f
            {
                float4 vertex: SV_POSITION;
                float2 uv:TEXCOORD0;
                float2 uv2:TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                //v.vertex.xyz += _Width * normalize(v.vertex.xyz);

                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                //return baseColor.a;
                //return float4(i.uv.x,i.uv.y,0,1);
                //return float4(baseColor.rgb, 1);
                return float4(baseColor.rgb*_AlbedoColor.rgb, 1);
            }
            ENDHLSL
        }
        
                Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            // -------------------------------------
            // Render State Commands
            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            // -------------------------------------
            // Shader Stages
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // -------------------------------------
            // Includes
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}