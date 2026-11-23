Shader "Emberdeep/Mana Crystal"
{
    Properties
    {
        _Fill("Water charge",Range(0,1))=1
        _Tint("Water colour",Color)=(.06,.65,1,1)
        _Flow("Drain (-1) / fill (+1)",Range(-1,1))=0
        _Dull("Empty and dull",Range(0,1))=0
        _Pulse("Gulp flash",Range(0,1))=0
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
            float _Flow;
            float _Dull;
            float _Pulse;
            CBUFFER_END
            struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 local:TEXCOORD1;float3 world:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.local=a.p.xyz;o.world=TransformObjectToWorld(a.p.xyz);return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Opaque glass vessel (no transparency pass on Quest): the water level rises
                // from the wrist end to the knuckle end of the cut stone.
                float height=(i.local.z+.029)/.058;
                float moving=saturate(abs(_Flow));
                float wave=sin(i.local.x*230+_Time.y*(2+6*moving))*(.015+.03*moving)*step(.01,_Fill)*step(_Fill,.99);
                float filled=1-smoothstep(_Fill+wave-.018,_Fill+wave+.018,height);
                float3 n=normalize(i.n),view=GetWorldSpaceNormalizeViewDir(i.world);
                // Each flat facet takes one tone from how squarely it faces the eye.
                float facing=abs(dot(n,view));
                float rim=pow(1-facing,2.2);
                float sky=saturate(n.y*.5+.5);
                float3 deep=_Tint.rgb*.40+float3(0,.03,.05);
                float3 bright=lerp(_Tint.rgb,float3(1,1,1),.38);
                float3 water=lerp(deep,bright,saturate(pow(facing,1.7)*.72+sky*.30));
                // Draining: streaks run toward the wrist. Filling: bright bubbles rise.
                float streak=sin(i.local.x*520+i.local.y*300)*.5+.5;
                float run=frac(height*7+_Time.y*(_Flow<0?3.5:-2.5));
                float motion=smoothstep(.75,1,streak)*smoothstep(.6,.95,run)*moving;
                water+=float3(.6,1,1)*motion*.45;
                // Empty glass: pale and clear with bright edges; dull glass turns matte grey.
                float3 glass=lerp(float3(.42,.56,.60),float3(.80,.93,.96),rim)*(.55+.25*sky);
                float3 dull=float3(.24,.26,.27)*(.65+.35*facing);
                float3 empty=lerp(glass,dull,_Dull);
                float3 color=lerp(empty,water,filled);
                float crest=1-smoothstep(.01,.04,abs(height-_Fill-wave));
                color+=float3(.55,1,.95)*crest*.38*step(.02,_Fill)*step(_Fill,.98);
                color+=_Tint.rgb*step(.995,_Fill)*(.14+.07*sin(_Time.y*2));
                // A hard glint on whichever facet mirrors the key light, and a thin bright rim.
                float shine=1-.8*_Dull;
                float glint=pow(saturate(dot(n,normalize(normalize(float3(-.4,.8,-.3))+view))),24);
                color+=float3(1,1,.96)*glint*(.45+.35*filled)*shine;
                color+=float3(.55,.95,.92)*rim*.30*shine;
                color+=float3(.5,.95,1)*_Pulse*.55;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
