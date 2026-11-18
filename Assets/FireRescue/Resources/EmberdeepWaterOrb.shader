Shader "Emberdeep/Water Orb"
{
    Properties
    {
        _Charge("Charge",Range(0,1))=1
        _Tint("Tint",Color)=(.10,.62,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Charge;
            float4 _Tint;
            CBUFFER_END
            struct A {float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 local:TEXCOORD1;float3 world:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a)
            {
                V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // A unit sphere that breathes and ripples; stronger as it fills.
                float t=_Time.y;
                float wobble=sin(a.p.y*14+t*7)*.035+sin(a.p.x*11-t*5.3)*.03+sin(a.p.z*9+t*4.1)*.025;
                float3 p=a.p.xyz+a.n*wobble*(.6+.4*_Charge);
                o.p=TransformObjectToHClip(p);o.n=TransformObjectToWorldNormal(a.n);o.local=a.p.xyz;o.world=TransformObjectToWorld(p);
                return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n=normalize(i.n);
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float rim=pow(1-saturate(dot(n,view)),2.2);
                // Bands spiral around the vertical axis so the water reads as spinning.
                float angle=atan2(i.local.z,i.local.x+.0001);
                float band=smoothstep(.55,1,sin(angle*3+i.local.y*9-_Time.y*6));
                float sparkle=smoothstep(.93,1,sin(angle*7+_Time.y*9)*sin(i.local.y*23-_Time.y*4));
                float3 color=lerp(_Tint.rgb*.45,_Tint.rgb,.35+.65*rim);
                color+=float3(.75,.95,1)*(band*.35+rim*.45)+sparkle*.5;
                float alpha=saturate(.42+rim*.5+band*.25);
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
