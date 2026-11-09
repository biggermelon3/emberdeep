Shader "Creek Diorama/Living Water"
{
    Properties
    {
        _DeepColor("Deep lagoon", Color)=(.025,.30,.42,1)
        _ShallowColor("Turquoise shallows", Color)=(.10,.68,.68,1)
        _FoamColor("Ivory foam", Color)=(.83,.98,.94,1)
        _FlowSpeed("Current speed", Range(0,2))=.35
        _RippleScale("Ripple density", Range(5,100))=38
        _ShoreWidth("Shore foam width (metres)", Range(.001,.1))=.007
        _DepthRange("Color depth (metres)", Range(.01,.2))=.07
        _UseDepth("Camera depth and opaque textures enabled", Float)=1
        [Toggle] _WorldSpace("Continuous flow across modular tiles", Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "CreekNoise.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor, _ShallowColor, _FoamColor;
            float _FlowSpeed, _RippleScale, _ShoreWidth, _DepthRange, _UseDepth, _WorldSpace;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 local:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=v.positionOS.xyz;
                float3 samplePosition=lerp(p,TransformObjectToWorld(p),_WorldSpace);
                p.y+=.0012*sin(samplePosition.x*26+_Time.y*_FlowSpeed*2)*sin(samplePosition.z*31-_Time.y*_FlowSpeed);
                o.positionWS=TransformObjectToWorld(p); o.positionCS=TransformWorldToHClip(o.positionWS);o.local=p;return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t=_Time.y*_FlowSpeed;
                float2 p=lerp(i.local.xz,i.positionWS.xz,_WorldSpace);
                float n=CreekNoise(p*12+float2(t*.12,-t*.10));
                float a=p.x*_RippleScale+p.y*17+t*2+n*2;
                float b=p.y*_RippleScale*1.31-p.x*12-t*1.5;
                float3 normalOS=normalize(float3(-cos(a)*.12,1,-cos(b)*.12));
                float3 N=TransformObjectToWorldNormal(normalOS), Vw=GetWorldSpaceNormalizeViewDir(i.positionWS);
                float depth=.06, edge=0;
                float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
                if(_UseDepth>.5)
                {
                    float raw=SampleSceneDepth(screenUV);
                    #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                    #endif
                    float3 behind=ComputeWorldSpacePosition(screenUV,raw,UNITY_MATRIX_I_VP);
                    depth=max(0,i.positionWS.y-behind.y);
                    edge=1-smoothstep(_ShoreWidth*.12,_ShoreWidth*(.7+n*.7),depth);
                }
                float basin=saturate(depth/_DepthRange)*.65+CreekNoise(p*3.5)*.35;
                half3 col=lerp(_ShallowColor.rgb,_DeepColor.rgb,saturate(basin));
                // Soft moving caustic filaments beneath the surface.
                float ridge=sin(a*.72+sin(b*.45))+sin(b*.85+sin(a*.38));
                float caustic=CreekBand(ridge,.075)*(.3+.7*n);
                col+=half3(.035,.14,.12)*caustic;
                float fresnel=pow(1-saturate(dot(N,Vw)),4);
                col=lerp(col,half3(.22,.53,.66),fresnel*.65);
                Light sun=GetMainLight();
                float spec=pow(saturate(dot(N,normalize(sun.direction+Vw))),180);
                col+=sun.color*spec*.55;
                float glint=CreekBand(sin(a)+sin(b),.035)*smoothstep(.69,.85,CreekNoise(p*29+float2(t*.2,0)));
                col=lerp(col,_FoamColor.rgb,glint*.52);
                float foam= edge*(.5+.5*smoothstep(.30,.6,CreekNoise(p*110+float2(t*.6,t*.9))));
                col=lerp(col,_FoamColor.rgb,saturate(foam));
                if(_UseDepth>.5) col=lerp(col,SampleSceneColor(screenUV+N.xz*.0015),.06*(1-foam));
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
