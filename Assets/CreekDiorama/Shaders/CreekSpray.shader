Shader "Creek Diorama/Waterfall Spray"
{
    Properties { _Color("Droplet color",Color)=(.75,.95,1,1) _Speed("Spray speed",Range(0,3))=1.2 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+5" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;float _Speed;
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 particle:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float alpha:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float age=frac(_Time.y*_Speed+i.particle.x);
                float angle=i.particle.y*6.28318;
                float3 p=i.positionOS.xyz;
                p+=float3(cos(angle)*age*.035,4*age*(1-age)*i.particle.w,sin(angle)*age*.014);
                float3 world=TransformObjectToWorld(p);
                float scale=length(TransformObjectToWorldDir(float3(1,0,0),false));
                world+=(UNITY_MATRIX_V[0].xyz*i.uv.x+UNITY_MATRIX_V[1].xyz*i.uv.y)*i.particle.z*scale;
                o.positionCS=TransformWorldToHClip(world);o.uv=i.uv;o.alpha=sin(age*PI);return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float circle=1-smoothstep(.35,1,dot(i.uv,i.uv));
                return half4(_Color.rgb,circle*i.alpha*.7);
            }
            ENDHLSL
        }
    }
}
