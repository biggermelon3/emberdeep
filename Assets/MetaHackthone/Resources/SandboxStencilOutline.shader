Shader "PhysicalAssembly/SandboxStencilOutline" {
Properties { _Color("Color",Color)=(1,.72,.02,1) _Width("Width",Float)=.002 }
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" }
HLSLINCLUDE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _Color; float _Width;
CBUFFER_END
struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float3 smooth:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct V {float4 positionCS:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
V expand(A a,float width){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 n=dot(a.smooth,a.smooth)>.01?a.smooth:a.normalOS;o.positionCS=TransformWorldToHClip(TransformObjectToWorld(a.positionOS.xyz)+TransformObjectToWorldNormal(n)*width);return o;}
V mask(A a){return expand(a,0);}
V edge(A a){return expand(a,_Width);}
V halo(A a){return expand(a,_Width*2.5);}
half4 solid(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return _Color;}
half4 glow(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return half4(_Color.rgb,.16);}
ENDHLSL
Pass {
Name "Mask" Cull Back ZWrite Off ZTest LEqual ColorMask 0
Stencil {Ref 4 ReadMask 4 WriteMask 4 Comp Always Pass Replace}
HLSLPROGRAM
#pragma vertex mask
#pragma fragment solid
#pragma multi_compile_instancing
ENDHLSL
}
Pass {
Name "Halo" Cull Front ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
Stencil {Ref 4 ReadMask 4 WriteMask 0 Comp NotEqual Pass Keep}
HLSLPROGRAM
#pragma vertex halo
#pragma fragment glow
#pragma multi_compile_instancing
ENDHLSL
}
Pass {
Name "Edge" Cull Front ZWrite Off ZTest LEqual
Stencil {Ref 4 ReadMask 4 WriteMask 0 Comp NotEqual Pass Keep}
HLSLPROGRAM
#pragma vertex edge
#pragma fragment solid
#pragma multi_compile_instancing
ENDHLSL
}
Pass {
Name "Release user stencil bit" Cull Back ZWrite Off ZTest LEqual ColorMask 0
Stencil {Ref 0 ReadMask 4 WriteMask 4 Comp Always Pass Replace}
HLSLPROGRAM
#pragma vertex mask
#pragma fragment solid
#pragma multi_compile_instancing
ENDHLSL
}
} }
