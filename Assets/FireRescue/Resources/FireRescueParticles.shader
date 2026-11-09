Shader "FireRescue/SoftParticles"
{
    Properties { _DstBlend("Destination blend", Float)=10 _Flame("Flame turbulence",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha [_DstBlend]
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _DstBlend, _Flame;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;o.world=TransformObjectToWorld(v.positionOS.xyz);return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv*2-1;
                float phase=_Time.y*9+i.world.x*23+i.world.z*19;
                float soft=saturate(1-dot(p,p));
                float alpha=soft*soft;
                if(_Flame>.5)
                {
                    // Narrow, wind-bent tips instead of a glowing circular sprite.
                    float height=saturate(p.y*.5+.5);
                    p.x+=sin(p.y*6-phase)*.17*height+sin(p.y*13-phase*1.4)*.06;
                    float width=lerp(.9,.055,height*height);
                    float core=saturate(1-abs(p.x)/width);
                    alpha=core*core*smoothstep(-1,-.7,p.y)*(1-smoothstep(.65,1,p.y));
                    alpha*=.75+.25*sin(p.y*15+p.x*7-phase);
                }
                return half4(i.color.rgb*lerp(.85,1.2,soft),i.color.a*alpha);
            }
            ENDHLSL
        }
    }
}
