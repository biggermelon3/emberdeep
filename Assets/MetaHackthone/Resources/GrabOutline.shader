Shader "PhysicalAssembly/GrabOutline" {
Properties { _Color("Color",Color)=(1,.4,0,1) _Width("Width",Float)=.002 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" } Pass {
Cull Front ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float4 _Color; float _Width;
struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct V {float4 positionCS:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz+a.normalOS*_Width);return o;}
half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return _Color;}
ENDHLSL
} } }
