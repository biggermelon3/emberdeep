Shader "Emberdeep/Mana Crystal"
{
    Properties
    {
        _Fill("Water charge",Range(0,1))=1
        _Tint("Water colour",Color)=(.06,.65,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Fill;
            float4 _Tint;
            CBUFFER_END
            struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 local:TEXCOORD1;float3 world:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.local=a.p.xyz;o.world=TransformObjectToWorld(a.p.xyz);return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Charge rises from the wrist end to the knuckle end of the cut stone.
                float height=(i.local.z+.029)/.058;
                float wave=sin(i.local.x*230+_Time.y*2)*.015*step(.01,_Fill)*step(_Fill,.99);
                float filled=1-smoothstep(_Fill+wave-.018,_Fill+wave+.018,height);
                float3 n=normalize(i.n),view=GetWorldSpaceNormalizeViewDir(i.world);
                // Each flat facet takes one tone from how squarely it faces the eye.
                float facing=abs(dot(n,view));
                float sky=saturate(n.y*.5+.5);
                float3 deep=_Tint.rgb*.40+float3(0,.03,.05);
                float3 bright=lerp(_Tint.rgb,float3(1,1,1),.38);
                float3 water=lerp(deep,bright,saturate(pow(facing,1.7)*.72+sky*.30));
                float3 empty=float3(.31,.36,.38)*(.50+.50*facing)+float3(.05,.05,.04)*sky;
                float3 color=lerp(empty,water,filled);
                float crest=1-smoothstep(.01,.04,abs(height-_Fill-wave));
                color+=float3(.55,1,.95)*crest*.30*step(.02,_Fill)*step(_Fill,.98);
                color+=_Tint.rgb*step(.995,_Fill)*(.14+.07*sin(_Time.y*2));
                // A hard glint on whichever facet mirrors the key light, and a thin bright rim.
                float glint=pow(saturate(dot(n,normalize(normalize(float3(-.4,.8,-.3))+view))),24);
                color+=float3(1,1,.96)*glint*(.30+.45*filled);
                color+=float3(.55,.95,.92)*pow(1-facing,3)*.20;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
