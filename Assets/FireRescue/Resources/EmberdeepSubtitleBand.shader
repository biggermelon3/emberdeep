// The camp spirit's subtitle backing (EmberdeepForestSpirit): the soft band of EmberdeepEnvironmentFx (uv.z 3), drawn
// over the scene like the subtitle's TextMesh (whose font shader ignores depth). First capture 2026-11-22: depth-tested,
// the tent's roof cut through the band while the text stayed on top.
Shader "Emberdeep/Subtitle Band"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION;float4 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float4 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv.xy*2-1;
                half alpha=i.uv.z<2.5?pow(saturate(1-dot(p,p)),2):1-smoothstep(.55,1,abs(p.x)); // a wide solid core, a short soft edge
                return half4(i.color.rgb,alpha*i.color.a);
            }
            ENDHLSL
        }
    }
}
