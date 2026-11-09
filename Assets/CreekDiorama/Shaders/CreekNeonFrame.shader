Shader "Creek Diorama/Neon Frame" {
 Properties { [HDR] _Color("Emission",Color)=(.1,3,4,1) _Radius("Core radius metres",Float)=.0025 }
 SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30"}
 Pass {Blend One One ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _Color;float _Radius;
 CBUFFER_END
 struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 positionCS:SV_POSITION;float4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 s=float3(length(TransformObjectToWorldDir(float3(1,0,0),false)),length(TransformObjectToWorldDir(float3(0,1,0),false)),length(TransformObjectToWorldDir(float3(0,0,1),false)));float3 p=i.positionOS.xyz+i.normalOS*_Radius*lerp(1,1.7,i.color.r)/max(s,.0001);o.positionCS=TransformObjectToHClip(p);o.color=i.color;return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return half4(lerp(_Color.rgb,half3(5,7,8),i.color.r),1);}
 ENDHLSL
 }
 }
}
