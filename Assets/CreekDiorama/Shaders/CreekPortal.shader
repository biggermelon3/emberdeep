Shader "Creek Diorama/Portal"
{
    Properties { _Color("Portal turquoise",Color)=(.02,.7,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+20" }
        Pass
        {
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv*2-1;float r=length(p);clip(1-r);
                float angle=atan2(p.y,p.x),t=_Time.y;
                float spiral=pow(saturate(.5+.5*sin(r*32-angle*3-t*3)),5);
                float rim=smoothstep(.83,.99,r);
                half3 color=lerp(half3(.008,.035,.10),_Color.rgb,.12+spiral*.34+rim*.7);
                color+=half3(.45,.9,1)*pow(rim,6)*.65;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
