Shader "Emberdeep/Outcome Veil"
{
    // One soft-edged, alpha-blended sheet over the board for the end-of-level moment (darken on a loss,
    // gold wash on a clear). Vertex alpha feathers the edges; _Color carries tint and strength.
    Properties { _Color("Tint and strength",Color)=(0,0,0,0) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a)
            {
                V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.color=a.color;return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                return half4(_Color.rgb,_Color.a*i.color.a);
            }
            ENDHLSL
        }
    }
}
