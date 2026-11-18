Shader "Emberdeep/Soft Toy Animals"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.normal=TransformObjectToWorldNormal(a.normalOS);o.color=a.color;return o;}
            half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float light=dot(normalize(i.normal),normalize(float3(-.4,.85,-.3)));float shade=lerp(.65,1.08,smoothstep(-.3,.7,light));return half4(i.color.rgb*shade,1);}
            ENDHLSL
        }
    }
}
