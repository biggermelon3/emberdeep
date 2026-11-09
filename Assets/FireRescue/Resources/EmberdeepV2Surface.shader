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
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor, _WetColor, _EmissionColor;
            half _Wetness, _Scorch, _Heat, _Roughness, _Wind, _GroundDetail;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; half3 normal:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=TransformObjectToWorld(v.positionOS.xyz);
                // Millimetre-scale visual motion only; gameplay anchors never move.
                p.x+=sin(_Time.y*1.6+p.z*9+p.x*3)*.0015*_Wind*saturate(v.positionOS.y);
                o.world=p; o.normal=TransformObjectToWorldNormal(v.normalOS);
                o.positionCS=TransformWorldToHClip(p); return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half3 n=normalize(i.normal), v=GetWorldSpaceNormalizeViewDir(i.world);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half light=smoothstep(-.35,.85,dot(n,sun.direction));
                half sky=saturate(n.y*.5+.5);
                half3 pigment=lerp(_BaseColor.rgb,half3(.075,.085,.08),_Scorch*.88);
                float moss=sin(i.world.x*29+sin(i.world.z*23))*sin(i.world.z*31+i.world.x*7);
                pigment*=1+_GroundDetail*(moss*.12-.045);
                pigment=lerp(pigment,pigment*.68+_WetColor.rgb*.12,_Wetness);
                half3 color=pigment*(half3(.22,.3,.33)+sun.color*light*sun.shadowAttenuation*.68)*( .8+.2*sky );
                half fresnel=pow(1-saturate(dot(n,v)),3);
                half3 h=normalize(sun.direction+v);
                half spec=pow(saturate(dot(n,h)),lerp(12,70,1-_Roughness));
                color+=half3(.55,.85,.89)*(spec*.32+fresnel*.13)*_Wetness;
                color+=half3(1,.18,.015)*_Heat*.14*(.8+.2*sin(_Time.y*5+i.world.z*8));
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
