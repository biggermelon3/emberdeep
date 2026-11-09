Shader "Creek Diorama/Waterfall Impact"
{
    Properties
    {
        _FoamColor("Splash color",Color)=(.8,.98,.95,1)
        _Speed("Ripple speed",Range(0,2))=.6
        _WaterBounds("Local water bounds",Vector)=(-10,-10,10,10)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CreekNoise.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _FoamColor;float _Speed;float4 _WaterBounds;
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 local:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V Vert(A i) { V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.local=i.positionOS.xz;return o; }
            half4 Frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 p=i.uv*2-1;float r=length(p),t=_Time.y*_Speed;
                float n=CreekNoise(p*10+float2(t,t*.8));
                float rings=CreekBand(sin((r-t)*24+n*.7),.12);
                float broken=smoothstep(.25,.55,CreekNoise(p*17-t*.6));
                float core=(1-smoothstep(.05,.48,r))*(.45+.55*n);
                float alpha=saturate(core+rings*broken*.65)*(1-smoothstep(.65,1,r));
                float2 inside=min(i.local-_WaterBounds.xy,_WaterBounds.zw-i.local);
                alpha*=smoothstep(0,.018,min(inside.x,inside.y));
                return half4(_FoamColor.rgb,alpha*.88);
            }
            ENDHLSL
        }
    }
}
