Shader "Emberdeep/Crafted Details"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A {float4 p:POSITION;float3 n:NORMAL;half4 c:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 w:TEXCOORD1;half4 c:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.w=TransformObjectToWorld(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.n),v=GetWorldSpaceNormalizeViewDir(i.w);Light light=GetMainLight();
                half diffuse=smoothstep(-.35,.85,dot(n,light.direction));
                half spec=pow(saturate(dot(n,normalize(light.direction+v))),28);
                return half4(i.c.rgb*(.48+diffuse*.52)+half3(1,.86,.62)*spec*.15,1);
            }
            ENDHLSL
        }
    }
}
