Shader "PhysicalAssembly/SandboxDissolve" {
Properties { _Dissolve("Dissolve",Range(0,1))=0 }
SubShader { Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" } Pass {
Cull Back ZWrite On
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float _Dissolve;
CBUFFER_END
struct A {float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};
struct V {float4 positionCS:SV_POSITION;float3 positionOS:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.positionOS=a.positionOS.xyz;return o;}
half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float n=frac(sin(dot(floor(i.positionOS*120),float3(12.9898,78.233,39.425)))*43758.5453);clip(n-_Dissolve);return half4(1,.65+.3*step(n-_Dissolve,.12),.02,1);}
ENDHLSL
} } }
