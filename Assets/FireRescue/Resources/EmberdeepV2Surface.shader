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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _WetColor, _EmissionColor;
            half _Wetness, _Scorch, _Heat, _Roughness, _Wind, _GroundDetail;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; half2 baked:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=TransformObjectToWorld(v.positionOS.xyz);
                // Millimetre-scale visual motion only; gameplay anchors never move.
                // Authored meshes carry a sway weight in vertex alpha (0 on trunks, stone and ground).
                half sway=_Wind*v.color.a;
                p.x+=sin(_Time.y*1.6+p.z*9+p.x*3)*.0016*sway;
                p.z+=sin(_Time.y*1.1+p.x*7)*.0008*sway;
                o.world=p; o.normal=TransformObjectToWorldNormal(v.normalOS);
                // Baked ambient occlusion and height tone from Blender; plain primitives read as 1.
                o.baked=half2(v.color.r,v.color.a);
                o.positionCS=TransformWorldToHClip(p); return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.normal), v=GetWorldSpaceNormalizeViewDir(i.world);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half sky=saturate(n.y*.5+.5);
                half baked=saturate(i.baked.x);
                // Heat dries foliage to amber-brown from the lowest boughs upward (baked height in alpha).
                half dry=smoothstep(i.baked.y-.30,i.baked.y+.12,_Heat*1.25)*step(.001,_Heat);
                half3 pigment=lerp(_BaseColor.rgb,half3(.40,.21,.06)+_BaseColor.rgb*.18,dry*.82);
                pigment=lerp(pigment,half3(.075,.085,.08),_Scorch*.88);
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
                pigment=lerp(pigment,pigment*.68+_WetColor.rgb*.12,_Wetness);
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
                color+=sun.color*spec*(1-_Roughness)*.16*(1-_Scorch);
                color+=half3(.55,.85,.89)*(spec*.32+fresnel*.13)*_Wetness;
                // Firelight from below: undersides glow and flicker while the tree burns.
                half flicker=.78+.22*sin(_Time.y*6+i.world.z*40+i.world.x*31);
                color+=half3(1,.30,.03)*_Heat*(.07+.30*saturate(.55-n.y*.6))*flicker;
                color+=_EmissionColor.rgb;
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
