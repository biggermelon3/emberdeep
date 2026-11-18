Shader "Emberdeep/Bounded Environment Effects"
{
    Properties { _EffectTime("Local animation clock",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _EffectTime;
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float4 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float4 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv.xy*2-1;float r=length(p),aa=max(.025,fwidth(r));
                half alpha=1;half3 color=i.color.rgb;
                if(i.uv.z<2.5)alpha=pow(saturate(1-r*r),2);
                else if(i.uv.z<3.5)alpha=1-smoothstep(.2,1,abs(p.x));
                else
                {
                    float curve=.16*sin(p.x*4+i.uv.w);
                    alpha=(1-smoothstep(.10,.45,abs(p.y-curve)))*(1-smoothstep(.45,1,abs(p.x)));
                    alpha*=.5+.5*sin(p.x*7+i.uv.w)*sin(p.x*7+i.uv.w);
                }
                return half4(color,alpha*i.color.a);
            }
            ENDHLSL
        }
    }
}
