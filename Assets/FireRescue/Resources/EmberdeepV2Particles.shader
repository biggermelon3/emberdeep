Shader "Emberdeep/V2/Particles"
{
    Properties
    {
        _DstBlend("Destination blend", Float)=10
        _Style("Mist 0 / Flame 1 / Glow 2 / Water 3 / Strand 4 / Ripple 5", Float)=0
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
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _DstBlend, _Style, _Opacity, _FoamAmount, _EdgeSoftness, _Highlight;
            half4 _Tint;
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
                float pixelWidth=fwidth(r2);
                float strandWidth=fwidth(input.uv.x*6.28318);
                float glintPhase=input.world.y*71+input.world.z*53-EmberdeepTime()*8;
                float glintFilter=1-smoothstep(.75,3.14,fwidth(glintPhase));
                float soft=saturate(1-r2);
                float alpha=soft*soft;
                half3 color=input.color.rgb;
                // World-space phase is identical for both eyes. No camera-depth sampling,
                // refraction or view-dependent noise is needed for the tabletop effects.
                float phase=EmberdeepTime()*6.2+input.world.x*19+input.world.z*13;
                if(_Style>4.5)
                {
                    // Contact ripple: one crisp ring and a fainter inner one on a flat billboard.
                    float r=sqrt(r2);
                    float ringWidth=max(fwidth(r),.02);
                    float ring=smoothstep(.70-ringWidth,.70+ringWidth,r)*(1-smoothstep(.90-ringWidth,.90+ringWidth,r));
                    float inner=smoothstep(.36-ringWidth,.36+ringWidth,r)*(1-smoothstep(.47-ringWidth,.47+ringWidth,r));
                    alpha=saturate(ring+inner*.45);
                    color=lerp(color,half3(.92,1,.98),ring*.35);
                }
                else if(_Style>3.5)
                {
                    // Closed four-sided mesh strands; the seam repeats at u=0/1.
                    float roundLight=.5+.5*cos(input.uv.x*6.28318-.7);
                    float narrow=lerp(.16,pow(saturate(roundLight),12),1-smoothstep(.4,2,strandWidth));
                    float glint=.82+.18*sin(glintPhase)*glintFilter;
                    // Darker teal flanks and a pale core band: a painted ribbon of water.
                    color=lerp(color*.52,color*1.08,smoothstep(.15,.75,roundLight))+half3(.42,.52,.52)*narrow*_Highlight*glint;
                    alpha=.74+.18*roundLight;
                }
                else if(_Style>2.5)
                {
                    // Painted droplet: deeper rim, clear body and one offset glint,
                    // including a cannon particle with zero velocity.
                    alpha=1-smoothstep(1-max(_EdgeSoftness*.55,pixelWidth),1,r2);
                    float bead=sqrt(saturate(1-r2));
                    float highlight=pow(saturate(1-dot(p-float2(-.26,.3),p-float2(-.26,.3))*3.4),3);
                    float rim=1-smoothstep(.30,.62,bead);
                    color=lerp(color*1.06,color*half3(.30,.52,.62),rim)+half3(.70,.78,.80)*highlight*_Highlight;
                    color=lerp(color,half3(.88,.96,.94),_FoamAmount*.65);
                    alpha*=.72+.26*bead;
                }
                else if(_Style>1.5)
                {
                    alpha=soft*soft*soft;
                    color*=.85+.15*soft;
                }
                else if(_Style>.5)
                {
                    // The accepted soft flame: a squared alpha field with a graded contour and small
                    // wind distortion (a thresholded silhouette read as an opaque triangular spike).
                    // Only the colour is new: a cream core, amber body and red-orange fringe blend
                    // smoothly across that same field. Vertex green is the particle's remaining heat.
                    float height=saturate(input.uv.y);
                    float flamePhase=EmberdeepTime()*9+input.world.x*23+input.world.z*19;
                    float bend=sin(p.y*6-flamePhase)*.13*height+sin(p.y*13-flamePhase*1.4)*.045;
                    float x=p.x+bend;
                    float width=lerp(.9,.18,height*height);
                    float core=saturate(1-abs(x)/width);
                    alpha=core*core*smoothstep(-1,-.7,p.y)*(1-smoothstep(.55,1,p.y));
                    alpha*=.8+.2*sin(p.y*15+x*7-flamePhase);
                    float heat=saturate(input.color.g*1.15);
                    float ramp=core*(1.1-height*.5)*lerp(.6,1,heat);
                    half3 layered=lerp(half3(.95,.21,.03),half3(1,.55,.07),smoothstep(.10,.50,ramp));
                    layered=lerp(layered,half3(1,.86,.34),smoothstep(.45,.80,ramp));
                    layered=lerp(layered,half3(1,.97,.80),smoothstep(.78,1,ramp)*heat);
                    color=lerp(color,layered,.7)*lerp(.85,1.12,soft);
                }
                else
                {
                    // Cauliflower puff: five overlapping lobes with a lit crown and a shaded
                    // belly, instead of a uniform soft disc.
                    float puff=1-length(p)/.58;
                    for(int lobe=0;lobe<5;lobe++)
                    {
                        float angle=lobe*1.2566+.4;
                        float2 centre=float2(cos(angle),sin(angle))*.44;
                        puff=max(puff,1-length(p-centre)/(.40+.05*sin(lobe*2.1)));
                    }
                    float puffEdge=max(fwidth(puff)*1.5,.24);
                    alpha=smoothstep(0,puffEdge,puff);
                    float lit=smoothstep(-.45,.55,p.y*.7-p.x*.35+puff*.35);
                    color*=lerp(.70,1.10,lit);
                }
                color=EmberdeepGrade(color,input.world,.3); // the frozen fire keeps most of its colour
                return half4(color,input.color.a*alpha*_Opacity);
            }
            ENDHLSL
        }
    }
}
