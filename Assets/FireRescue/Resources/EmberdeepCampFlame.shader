Shader "Emberdeep/V2 Camp Flame"
{
    Properties { _BaseColor("Flame", Color)=(1,.62,.12,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; half height:TEXCOORD0; half rim:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=TransformObjectToWorld(v.positionOS.xyz);
                // Authored flame meshes carry their height in vertex alpha: tips lick and lean, bases stay put.
                half h=saturate(v.color.a);
                float t=_Time.y*5.5;
                p.x+=(sin(t+p.y*14)+.5*sin(t*1.7+p.z*23))*.035*h*h;
                p.z+=(cos(t*1.2+p.y*11)+.5*sin(t*2.1+p.x*19))*.030*h*h;
                p.y+=sin(t*1.9+p.x*17+p.z*13)*.03*h*h;
                o.height=h;
                half3 n=TransformObjectToWorldNormal(v.normalOS);
                o.rim=1-abs(dot(normalize(n),GetWorldSpaceNormalizeViewDir(p)));
                o.positionCS=TransformWorldToHClip(p); return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Cream core, amber body and a red-orange edge: three flat tones like the concept sheet.
                half heat=saturate(1-i.height*.9-i.rim*.55);
                half3 color=lerp(half3(.93,.20,.03),half3(1,.55,.06),smoothstep(.12,.34,heat));
                color=lerp(color,half3(1,.86,.36),smoothstep(.46,.66,heat));
                return half4(color*_BaseColor.a,1);
            }
            ENDHLSL
        }
    }
}
