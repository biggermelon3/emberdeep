Shader "Emberdeep/Blue Tutorial Model"
{
    Properties { _BaseColor("Blue clay",Color)=(.15,.55,.9,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.world=TransformObjectToWorld(a.positionOS.xyz);o.normal=TransformObjectToWorldNormal(a.normalOS);o.color=a.color;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Demonstration gloves carry suede, leather and fleece tones per vertex; props stay one clay colour.
                half3 n=normalize(i.normal);
                float light=dot(n,normalize(float3(-.4,.8,-.65)));
                half3 pigment=_BaseColor.rgb*i.color.rgb;
                half rim=pow(1-saturate(dot(n,GetWorldSpaceNormalizeViewDir(i.world))),3);
                return half4(pigment*(.55+.45*smoothstep(-.4,.8,light))+pigment*rim*.35,1);
            }
            ENDHLSL
        }
    }
}
