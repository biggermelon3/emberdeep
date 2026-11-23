Shader "Emberdeep/V2 Shallow Creek"
{
    Properties { _BaseColor("Deep teal",Color)=(.035,.34,.39,1) _FoamColor("Ripple crests",Color)=(.48,.87,.84,1) _FlowSpeed("Flow speed",Float)=.28 _WaveScale("Ripple scale",Float)=13 }
    SubShader
    {
        // Opaque shallow water avoids transparent sorting and XR depth-copy costs.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_FoamColor; float _FlowSpeed,_WaveScale;
            float4x4 _WorldToCreek;
            float _CreekFrameEnabled,_CreekTime;
            CBUFFER_END
            // Board clock + Plan grade (LoopSpec §6, EmberdeepPlanLook): globals, not per material. With _EmberdeepClockOn 0
            // (camp, legacy flow) this runs on _Time as before, and the grade is 0 outside Plan.
            float _EmberdeepClock, _EmberdeepClockOn, _EmberdeepPlanGrade; float4 _EmberdeepPlanFlood;
            float EmberdeepTime(){return lerp(_Time.y,_EmberdeepClock,_EmberdeepClockOn);}
            // Time stopped: a whitish chalk grade everywhere outside the flood radius (world xz around the board
            // centre), which grows from the centre when the fire resumes; a thin warm band marks its front.
            half3 EmberdeepGrade(half3 color,float3 world,half strength)
            {
                if(_EmberdeepPlanGrade<=0)return color;
                float d=distance(world.xz,_EmberdeepPlanFlood.xz);
                half outside=smoothstep(_EmberdeepPlanFlood.w-.05,_EmberdeepPlanFlood.w,d);
                half lum=dot(color,half3(.299,.587,.114));
                color=lerp(color,(lum*.55+.47)*half3(1,.985,.955),_EmberdeepPlanGrade*outside*strength);
                half front=saturate(1-abs(d-_EmberdeepPlanFlood.w)/.05)*step(.001,_EmberdeepPlanFlood.w);
                return color+half3(1,.62,.30)*front*_EmberdeepPlanGrade*strength*.35;
            }
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A a) { V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o; }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float enabled=saturate(_CreekFrameEnabled);
                float3 local=mul(_WorldToCreek,float4(i.world,1)).xyz;
                float2 p=lerp(i.world.xz/.08,local.xz,enabled);
                float t=lerp(EmberdeepTime(),_CreekTime,enabled)*_FlowSpeed;
                float center=2.1+.035*sin((p.x+4.2)/.42*.8);
                float cross=(p.y-center)/.5,shore=saturate(abs(cross));
                // Two travelling wave families create small moving normals, not broad white bands.
                float a=p.x*10.7+cross*4.3+t*10;
                float b=p.x*17.1-cross*8.2+t*14.3+sin(p.x*1.8)*.7;
                float fa=1-smoothstep(.65,2.5,fwidth(a)),fb=1-smoothstep(.65,2.5,fwidth(b));
                float nx=cos(a)*.085*fa+cos(b)*.042*fb;
                float nz=cos(a)*.09*fa-cos(b)*.07*fb;
                float3 n=normalize(float3(-nx,1,-nz));
                n=normalize(lerp(n,mul(transpose((float3x3)_WorldToCreek),n),enabled));
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float facing=saturate(dot(n,view));
                float fresnel=.065+.58*pow(1-facing,3);
                float3 reflection=reflect(-view,n);
                half3 reflectedSky=lerp(half3(.30,.38,.36),half3(.09,.22,.29),saturate(reflection.y));
                float cloudReflection=smoothstep(.1,.95,sin(reflection.x*8+reflection.z*5)+sin(reflection.z*9-reflection.y*4)*.5);
                reflectedSky=lerp(reflectedSky,half3(.53,.61,.57),cloudReflection*.55);
                half3 deep=lerp(_BaseColor.rgb*.28,half3(.025,.14,.16),.65);
                half3 shallow=half3(.12,.28,.22);
                half3 bed=lerp(deep,shallow,smoothstep(.32,1,shore));
                float broad=sin(p.x*1.3+cross*2.1)+sin(p.x*2.6-cross*3.4);
                bed*=.96+broad*.035;
                float caustic=pow(saturate(.5+.5*sin(a*.72+sin(b*.44))),6)*fa;
                bed+=half3(.12,.22,.15)*caustic*.12*(1-fresnel);
                half3 color=lerp(bed,reflectedSky,fresnel);
                float ripples=pow(saturate(sin(a)+sin(b)*.35-.65),2)*fa*fb;
                color+=half3(.20,.35,.31)*ripples*.10;
                float spec=saturate(dot(reflection,normalize(float3(.12,.65,.75))));
                float specFilter=1/(1+length(fwidth(n))*60);
                color+=half3(.66,.77,.69)*pow(spec,24)*.12*specFilter;
                float bankWave=sin(p.x*5.3+t*3+cross*4);
                float bankFilter=1-smoothstep(.6,2.4,fwidth(p.x*5.3+cross*4));
                float bank=smoothstep(.82,.99,shore)*smoothstep(.35,.85,bankWave)*bankFilter;
                color=lerp(color,_FoamColor.rgb,bank*.18);
                color=EmberdeepGrade(color,i.world,1);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
