Shader "Emberdeep/V2/Particles"
{
    Properties
    {
        _DstBlend("Destination blend", Float)=10
        _Style("Mist 0 / Flame 1 / Glow 2 / Water 3 / Strand 4", Float)=0
        _Tint("Tint", Color)=(1,1,1,1)
        _Opacity("Opacity", Range(0,1))=1
        _FoamAmount("Foam", Range(0,1))=0
        _EdgeSoftness("Edge softness", Range(0.02,0.5))=0.18
        _Highlight("Water highlight", Range(0,1))=0.55
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "EmberdeepParticles"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _DstBlend, _Style, _Opacity, _FoamAmount, _EdgeSoftness, _Highlight;
            half4 _Tint;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS:POSITION;
                half4 color:COLOR;
                float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                half4 color:COLOR;
                float2 uv:TEXCOORD0;
                float3 world:TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                output.uv=input.uv;
                output.color=input.color*_Tint;
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.uv*2-1;
                float r2=dot(p,p);
                float soft=saturate(1-r2);
                float alpha=soft*soft;
                half3 color=input.color.rgb;
                // World-space phase is identical for both eyes. No camera-depth sampling,
                // refraction or view-dependent noise is needed for the tabletop effects.
                float phase=_Time.y*6.2+input.world.x*19+input.world.z*13;
                if(_Style>3.5)
                {
                    // Closed four-sided mesh strands; the seam repeats at u=0/1.
                    float roundLight=.5+.5*cos(input.uv.x*6.28318-.7);
                    float narrow=pow(saturate(roundLight),12);
                    float glint=.82+.18*sin(input.world.y*71+input.world.z*53-_Time.y*8);
                    color=lerp(color*.7,color,roundLight)+half3(.35,.48,.49)*narrow*_Highlight*glint;
                    alpha=.66+.18*roundLight;
                }
                else if(_Style>2.5)
                {
                    // Opaque-looking core, soft edge and one offset glint read as liquid,
                    // including a cannon particle with zero velocity.
                    alpha=1-smoothstep(1-_EdgeSoftness,1,r2);
                    float bead=sqrt(saturate(1-r2));
                    float highlight=pow(saturate(1-dot(p-float2(-.26,.3),p-float2(-.26,.3))*3.4),3);
                    color=color*lerp(.6,1,bead)+half3(.65,.73,.75)*highlight*_Highlight;
                    color=lerp(color,half3(.88,.96,.94),_FoamAmount*.65);
                    alpha*=.55+.35*bead;
                }
                else if(_Style>1.5)
                {
                    alpha=soft*soft*soft;
                    color*=.85+.15*soft;
                }
                else if(_Style>.5)
                {
                    // Start from FireRescue/SoftParticles: a squared soft alpha field,
                    // lifetime-driven colour and small wind distortion. V2's thresholded
                    // silhouette/forked shoulders produced opaque triangular spikes.
                    float height=saturate(input.uv.y);
                    float flamePhase=_Time.y*9+input.world.x*23+input.world.z*19;
                    float bend=sin(p.y*6-flamePhase)*.13*height+sin(p.y*13-flamePhase*1.4)*.045;
                    float x=p.x+bend;
                    float width=lerp(.9,.18,height*height);
                    float core=saturate(1-abs(x)/width);
                    alpha=core*core*smoothstep(-1,-.7,p.y)*(1-smoothstep(.55,1,p.y));
                    alpha*=.8+.2*sin(p.y*15+x*7-flamePhase);
                    color*=lerp(.85,1.12,soft);
                }
                else
                {
                    // Restrained rolling lobes instead of uniform grey circles.
                    float lobes=.83+.17*sin(p.x*4.3+p.y*2.6+phase*.3)*sin(p.y*3.7-phase*.22);
                    alpha=soft*soft*lobes;
                    color*=.83+.17*soft;
                }
                return half4(color,input.color.a*alpha*_Opacity);
            }
            ENDHLSL
        }
    }
}
