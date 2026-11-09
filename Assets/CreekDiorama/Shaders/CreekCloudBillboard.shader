Shader "Creek Diorama/Cloud Billboard"
{
 Properties {
 _MainTex("Realistic cloud RGBA",2D)="white"{}
 _Tint("Atmospheric tint",Color)=(1,1,1,1)
 _Opacity("Opacity",Range(0,1))=.85
 _Drift("Drift metres",Range(0,.3))=.035
 _Mirror("Mirror",Float)=0
 _NoiseTex("Peripheral billow noise",2D)="gray"{}
 _WispTex("Fine wisp noise",2D)="gray"{}
 _EdgeWarp("Edge turbulence",Range(0,.08))=.026
 _WispAmount("Peripheral vapor",Range(0,1))=.55
 _NoiseSpeed("Noise drift",Range(0,.1))=.012
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" "DisableBatching"="True"}
 Pass {
 Tags {"LightMode"="UniversalForward"}
 Blend SrcAlpha OneMinusSrcAlpha
 ZWrite Off Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
 TEXTURE2D(_WispTex); SAMPLER(sampler_WispTex);
 CBUFFER_START(UnityPerMaterial)
 float4 _MainTex_ST; half4 _Tint; float _Opacity,_Drift,_Mirror;
 float _EdgeWarp,_WispAmount,_NoiseSpeed;
 CBUFFER_END
 struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i) {
 V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 float3 c=TransformObjectToWorld(float3(0,0,0));
 float2 size=float2(length(TransformObjectToWorldDir(float3(1,0,0),false)),length(TransformObjectToWorldDir(float3(0,1,0),false)));
 float3 w=c+UNITY_MATRIX_V[0].xyz*(i.positionOS.x*size.x+sin(_Time.y*.08+c.x)*_Drift)+UNITY_MATRIX_V[1].xyz*i.positionOS.y*size.y;
 o.positionCS=TransformWorldToHClip(w);o.uv=i.uv;o.uv.x=lerp(o.uv.x,1-o.uv.x,_Mirror);return o;
 }
 half4 frag(V i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 float2 flow=float2(_Time.y*_NoiseSpeed,_Time.y*_NoiseSpeed*.31);
 float n=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,i.uv*3+flow).r;
 float n2=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,i.uv*3-float2(.37,.13)-flow*.7).r;
 float fine=SAMPLE_TEXTURE2D(_WispTex,sampler_WispTex,i.uv*7-flow*1.8).r;
 half4 original=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
 float edge=1-smoothstep(.25,.82,original.a);
 float2 uv=i.uv+float2(n-.5,n2-.5)*_EdgeWarp*edge;
 half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
 // Expand the existing silhouette locally: vapor follows the cloud rather than a card-shaped mask.
 float reach=_EdgeWarp*.85;
 half4 left=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+float2(reach,0));
 half4 right=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-float2(reach,0));
 half4 up=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv+float2(0,reach));
 half4 down=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv-float2(0,reach));
 float dilation=max(max(left.a,right.a),max(up.a,down.a));
 float wisps=saturate(dilation-c.a)*smoothstep(.25,.68,n*.65+fine*.65)*_WispAmount;
 half3 vapor=(left.rgb*left.a+right.rgb*right.a+up.rgb*up.a+down.rgb*down.a)/max(left.a+right.a+up.a+down.a,.001);
 c.rgb=lerp(c.rgb,vapor,saturate(wisps/max(c.a+wisps,.001)));
 c.a=c.a*lerp(1,smoothstep(.04,.5,n*.65+fine*.5),edge*.7)+wisps;
 float2 cardFade=smoothstep(0,.035,i.uv)*smoothstep(0,.035,1-i.uv);
 c.a*=cardFade.x*cardFade.y;
 c.rgb*=_Tint.rgb;c.a*=_Opacity*_Tint.a;clip(c.a-.002);return c;
 }
 ENDHLSL
 }
 }
}
