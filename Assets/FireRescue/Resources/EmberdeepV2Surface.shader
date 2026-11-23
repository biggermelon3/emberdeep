Shader "Emberdeep/V2 Sculpted Surface"
{
    Properties
    {
        _BaseColor("Pigment", Color)=(0.32,0.54,0.36,1)
        _WetColor("Wet tint", Color)=(0.13,0.36,0.36,1)
        _Wetness("Wetness", Range(0,1))=0
        _Scorch("Scorch", Range(0,1))=0
        _Heat("Heat", Range(0,1))=0
        _Roughness("Roughness", Range(0,1))=.7
        _Wind("Canopy motion", Range(0,1))=0
        _GroundDetail("Moss variation", Range(0,1))=0
        _EmissionColor("Emission", Color)=(0,0,0,0)
        _Fade("Dither fade (EMBERDEEP_DITHER_FADE only)", Range(0,1))=0
        _Ember("Forecast ember glow", Range(0,1))=0
        _VertexState("Per-vertex board state in UV1 (grass)", Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW
            // Only the runtime fade clones (EmberdeepTreeReadout) enable this, so every other
            // draw keeps early-Z; multi_compile so builds never strip it.
            #pragma multi_compile_local_fragment _ EMBERDEEP_DITHER_FADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _WetColor, _EmissionColor;
            half _Wetness, _Scorch, _Heat, _Roughness, _Wind, _GroundDetail, _Fade, _Ember, _VertexState;
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
            // UV1 = (heat, wetness, scorch, ember) per vertex, read only where _VertexState is 1: the
            // grass patches share one mesh and one draw. Every other mesh keeps the material values.
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float4 state:TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half2 baked:TEXCOORD2; half4 state:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                half4 state=saturate(v.state)*_VertexState;
                // Charred grass slumps to stubble (its mesh origin is the ground).
                v.positionOS.y*=1-.62*state.z;
                o.state=half4(max(_Heat,state.x),max(_Wetness,state.y),max(_Scorch,state.z),max(_Ember,state.w));
                float3 p=TransformObjectToWorld(v.positionOS.xyz);
                // Millimetre-scale visual motion only; gameplay anchors never move.
                // Authored meshes carry a sway weight in vertex alpha (0 on trunks, stone and ground).
                half sway=_Wind*v.color.a;
                p.x+=sin(EmberdeepTime()*1.6+p.z*9+p.x*3)*.0016*sway;
                p.z+=sin(EmberdeepTime()*1.1+p.x*7)*.0008*sway;
                o.world=p; o.normal=TransformObjectToWorldNormal(v.normalOS);
                // Baked ambient occlusion and height tone from Blender; plain primitives read as 1.
                o.baked=half2(v.color.r,v.color.a);
                o.positionCS=TransformWorldToHClip(p); return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #if defined(EMBERDEEP_DITHER_FADE)
                // 4x4 ordered dither in screen pixels: alpha-clip, no blending or sorting pass.
                uint2 px=uint2(i.positionCS.xy)&3;
                uint bayer=((px.x^px.y)&1)*8+(px.y&1)*4+(((px.x^px.y)>>1)&1)*2+((px.y>>1)&1);
                clip((bayer+.5)/16.0-_Fade);
                #endif
                half3 n=normalize(i.normal), v=GetWorldSpaceNormalizeViewDir(i.world);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half sky=saturate(n.y*.5+.5);
                half baked=saturate(i.baked.x);
                half heat=i.state.x, wetness=i.state.y, scorch=i.state.z, ember=i.state.w;
                // Heat dries foliage to amber-brown from the lowest boughs upward (baked height in alpha).
                half dry=smoothstep(i.baked.y-.30,i.baked.y+.12,heat*1.25)*step(.001,heat);
                half3 pigment=lerp(_BaseColor.rgb,half3(.40,.21,.06)+_BaseColor.rgb*.18,dry*.82);
                pigment=lerp(pigment,half3(.075,.085,.08),scorch*.88);
                // Meadow: broad sunlit and mossy patches plus fine flecks, all procedural and
                // faded out before they can shimmer at the headset's pixel footprint.
                float2 gp=i.world.xz*9.0,gi=floor(gp),gf=frac(gp);gf=gf*gf*(3-2*gf);
                float n1=lerp(lerp(Hash(gi),Hash(gi+float2(1,0)),gf.x),lerp(Hash(gi+float2(0,1)),Hash(gi+float2(1,1)),gf.x),gf.y);
                float2 hp=i.world.xz*23.0+7.3,hi=floor(hp),hf=frac(hp);hf=hf*hf*(3-2*hf);
                float n2=lerp(lerp(Hash(hi),Hash(hi+float2(1,0)),hf.x),lerp(Hash(hi+float2(0,1)),Hash(hi+float2(1,1)),hf.x),hf.y);
                half patch=smoothstep(.30,.72,n1*.65+n2*.35);
                half3 meadow=lerp(pigment*half3(.78,.93,.86),pigment*half3(1.18,1.11,.84),patch);
                float fleck=Hash(floor(i.world.xz*260.0))-.5;
                fleck*=1-smoothstep(.25,1.1,fwidth(i.world.x*260.0)+fwidth(i.world.z*260.0));
                pigment=lerp(pigment,meadow*(1+fleck*.11),_GroundDetail*saturate(n.y*2-.6));
                // Soaked (§4.3): clearly darker and cooler, with a tight glossy highlight below.
                pigment=lerp(pigment,pigment*.50+_WetColor.rgb*.10,wetness);
                // Occluded creases turn deeper and slightly cooler instead of merely grey.
                pigment*=lerp(half3(.52,.60,.74),half3(1,1,1),baked)*(.55+.45*baked);
                // Wrapped warm key light over a cool sky / warm ground ambient: soft, toy-like volumes.
                half wrap=saturate(dot(n,sun.direction)*.56+.44);
                half direct=wrap*wrap*lerp(.42,1,sun.shadowAttenuation);
                half3 ambient=lerp(half3(.25,.21,.16),half3(.33,.42,.46),sky);
                half3 color=pigment*(ambient+sun.color*half3(1.04,.95,.80)*direct*.86);
                half fresnel=pow(1-saturate(dot(n,v)),3);
                // A faint sky rim lifts silhouettes off the background without any post effect.
                color+=pigment*half3(.60,.78,.80)*fresnel*.34*sky*baked;
                half3 h=normalize(sun.direction+v);
                float normalVariance=dot(ddx(n),ddx(n))+dot(ddy(n),ddy(n));
                half spec=pow(saturate(dot(n,h)),lerp(12,70,1-saturate(_Roughness+normalVariance*8)));
                // Broad dry highlight separates enamel/brass from rough wood.
                color+=sun.color*spec*(1-_Roughness)*.16*(1-scorch);
                half wetSpec=pow(saturate(dot(n,h)),lerp(56,24,saturate(normalVariance*8)));
                color+=half3(.62,.88,.92)*(spec*.30+wetSpec*.55+fresnel*.20)*wetness;
                // Firelight from below: undersides glow and flicker while the tree burns.
                half flicker=.78+.22*sin(EmberdeepTime()*6+i.world.z*40+i.world.x*31);
                color+=half3(1,.30,.03)*heat*(.07+.30*saturate(.55-n.y*.6))*flicker;
                // Forecast (§4.4): the next fuel to catch smoulders, pulsing from below.
                color+=half3(1,.34,.05)*ember*(.06+.20*saturate(.6-n.y*.5))*(.65+.35*flicker);
                color+=_EmissionColor.rgb;
                color=EmberdeepGrade(color,i.world,1);
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 ShadowVert(A v):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 world=TransformObjectToWorld(v.positionOS.xyz),normal=TransformObjectToWorldNormal(v.normalOS);
                float4 p=TransformWorldToHClip(ApplyShadowBias(world,normal,_LightDirection));
                return ApplyShadowClamping(p);
            }
            half4 ShadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
    }
}
