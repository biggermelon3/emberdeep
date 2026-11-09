Shader "Creek Diorama/Silk Waterfall"
{
    Properties
    {
        _WaterColor("Clear falling water",Color)=(.08,.58,.68,1)
        _FoamColor("Aerated whitewater",Color)=(.8,.98,1,1)
        _FlowSpeed("Downward speed",Range(0,3))=.85
        _FoamAmount("Whitewater",Range(0,1))=.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CreekNoise.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _WaterColor, _FoamColor;
            float _FlowSpeed, _FoamAmount;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p=i.positionOS.xyz;
                p.x+=sin(i.uv.y*25-_Time.y*5+i.uv.x*12)*.0015*sin(i.uv.y*PI);
                o.positionCS=TransformObjectToHClip(p);o.uv=i.uv;return o;
            }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv=i.uv;float t=_Time.y*_FlowSpeed;
                float n=CreekNoise(float2(uv.x*18,uv.y*7-t*3));
                float packets=CreekNoise(float2(uv.x*24,uv.y*22-t*8));
                float silk=pow(saturate(.5+.5*sin(uv.x*92+n*6+sin(uv.y*17-t*5)*.7)),5)*(.15+.85*smoothstep(.25,.75,packets));
                float streak=CreekNoise(float2(uv.x*36,uv.y*9-t*4));
                float ribbons=smoothstep(.45,.85,streak)*.65+silk*.5;
                float broken=CreekNoise(float2(uv.x*13,uv.y*30-t*9));
                float crest=1-smoothstep(.04,.18,uv.y);
                float foot=smoothstep(.65,.98,uv.y)*(.45+broken*.55);
                float white=saturate(ribbons*_FoamAmount+crest*.28+foot*.72+smoothstep(.65,.85,packets)*.14);
                half3 col=lerp(_WaterColor.rgb,_FoamColor.rgb,white);
                col+=silk*.12;
                float edge=smoothstep(0,.06,uv.x)*smoothstep(0,.06,1-uv.x);
                float alpha=edge*lerp(.88,.99,white)*(1-smoothstep(.93,1,uv.y));
                return half4(col,alpha);
            }
            ENDHLSL
        }
    }
}
