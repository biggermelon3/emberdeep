Shader "Emberdeep/Tracked Hand Interior Occlusion"
{
    Properties { _HandColor("Hand color",Color)=(.66,.76,.75,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "TrackedHand"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _HandColor;
            float4x4 _HandClipWorldToLocal[12];
            float4 _HandClipHalfSize[12];
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A input)
            {
                V output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                output.normal=TransformObjectToWorldNormal(input.normalOS);
                output.positionCS=TransformWorldToHClip(output.world);return output;
            }
            half4 frag(V input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                [unroll] for(int i=0;i<12;i++)
                {
                    if(_HandClipHalfSize[i].w>.5)
                    {
                        float3 p=abs(mul(_HandClipWorldToLocal[i],float4(input.world,1)).xyz)-_HandClipHalfSize[i].xyz;
                        clip(max(p.x,max(p.y,p.z)));
                    }
                }
                half light=.65+.35*saturate(dot(normalize(input.normal),normalize(half3(.3,.8,-.4))));
                return half4(_HandColor.rgb*light,1);
            }
            ENDHLSL
        }
    }
}
