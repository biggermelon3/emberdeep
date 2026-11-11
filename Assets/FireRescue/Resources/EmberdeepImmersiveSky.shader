Shader "Emberdeep/Rising Forest Sky"
{
    Properties { _Reveal("Bottom to top reveal",Range(0,1))=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Background" }
        Pass
        {
            Name "RisingForestSky"
            Tags { "LightMode"="UniversalForward" }
            Cull Front ZWrite Off ZTest LEqual
            // Alpha accumulates as coverage, not alpha squared. A fully revealed
            // sky must be opaque to the compositor's passthrough underlay.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ColorMask RGBA
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Reveal;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float3 direction:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A input)
            {
                V output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.direction=input.positionOS.xyz;return output;
            }
            half4 frag(V input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 d=normalize(input.direction);
                float az=atan2(d.x,d.z);
                half3 horizon=half3(.28,.24,.18),zenith=half3(.035,.105,.16);
                half3 color=lerp(horizon,zenith,smoothstep(-.04,.68,d.y));
                float sunlight=saturate(dot(d,normalize(float3(-.48,.24,.84))));
                color+=half3(.28,.115,.025)*pow(sunlight,24);
                color=lerp(color,half3(.95,.65,.31),smoothstep(.99915,.99965,sunlight)*.85);
                // Broad, low-contrast layers frame the miniature without adding
                // expensive fog, moving peripheral scenery or background lights.
                float ridge=.008+sin(az*3+.7)*.035+sin(az*7-1.2)*.014;
                color=lerp(color,half3(.085,.155,.17),1-smoothstep(ridge-.004,ridge+.004,d.y));
                float farTrees=-.045+sin(az*4)*.012+(1-abs(frac(az*17)*2-1))*.055;
                color=lerp(color,half3(.035,.09,.095),1-smoothstep(farTrees-.002,farTrees+.002,d.y));
                float nearTrees=-.11+sin(az*2+.3)*.018+(1-abs(frac(az*25+.27)*2-1))*.07;
                half3 ground=lerp(half3(.018,.037,.041),half3(.025,.065,.067),smoothstep(-.9,-.05,d.y));
                color=lerp(color,ground,1-smoothstep(nearTrees-.002,nearTrees+.002,d.y));
                float edge=lerp(-.08,1.08,smoothstep(0,1,_Reveal));
                float alpha=1-smoothstep(edge-.08,edge+.08,d.y*.5+.5);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
