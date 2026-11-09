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
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_FoamColor; float _FlowSpeed,_WaveScale;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A a) { V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o; }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.world.xz*_WaveScale;float t=_Time.y*_FlowSpeed;
                float waves=sin(p.x*5+t*6+sin(p.y*3-t*2))*.5+.5;
                float cross=sin(p.y*11-p.x*2-t*5)*.5+.5;
                half crest=smoothstep(.86,.98,waves)*(.2+.8*cross);
                half caustic=pow(saturate(sin(p.x*7+p.y*5+t)+cos(p.y*8-t)),6)*.025;
                half rim=pow(1-saturate(dot(half3(0,1,0),GetWorldSpaceNormalizeViewDir(i.world))),3);
                return half4(lerp(_BaseColor.rgb,_FoamColor.rgb,crest*.5+rim*.18)+caustic,1);
            }
            ENDHLSL
        }
    }
}
