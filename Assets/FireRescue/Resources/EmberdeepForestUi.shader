Shader "Emberdeep/Forest Sign"
{
    Properties
    {
        _BaseColor("Forest green",Color)=(.09,.19,.14,1)
        _EdgeColor("Brass edge",Color)=(.48,.38,.21,1)
        _Size("Size in metres",Vector)=(1,1,0,0)
        _Radius("Corner radius",Float)=.02
        _Button("Button styling",Float)=0
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
            half4 _BaseColor,_EdgeColor;
            float4 _Size;
            float _Radius,_Button;
            CBUFFER_END
            struct A {float4 p:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 p:SV_POSITION;float3 local:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.p=TransformObjectToHClip(a.p.xyz);o.local=a.p.xyz;return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Distance to the rounded outline in metres, so trims keep one width on any panel.
                float2 p=i.local.xy*_Size.xy;
                float2 q=abs(p)-(_Size.xy*.5-_Radius);
                float inside=-(length(max(q,0))+min(max(q.x,q.y),0)-_Radius);
                float aa=max(fwidth(inside),.00015);
                float up=i.local.y+.5;
                float3 face=_BaseColor.rgb*(.90+.20*up);
                float3 brass=_EdgeColor.rgb*(.86+.30*up);
                float3 col;
                if(_Button>.5)
                {
                    // Pill: brass lip, a dark groove, a green face lit from above.
                    float lip=1-smoothstep(.0030-aa,.0030+aa,inside);
                    float groove=smoothstep(.0030-aa,.0030+aa,inside)*(1-smoothstep(.0042-aa,.0042+aa,inside));
                    float sheen=smoothstep(.58,.92,up)*smoothstep(.0042,.0100,inside)*.10;
                    col=lerp(face+sheen,brass,lip);
                    col=lerp(col,_BaseColor.rgb*.45,groove);
                }
                else
                {
                    // Board: painted planks, a soft shadow under the carved frame and a brass hairline.
                    float plank=i.local.y*_Size.y*150+sin(p.x*37)*1.2;
                    float grain=sin(plank)*.035*(1-smoothstep(.6,2.2,fwidth(plank)));
                    float shade=smoothstep(.0,.045,inside);
                    float hairline=smoothstep(.0150-aa,.0150+aa,inside)*(1-smoothstep(.0166-aa,.0166+aa,inside));
                    col=face*(1+grain)*lerp(.58,1,shade);
                    col=lerp(col,brass*.92,hairline*.85);
                }
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
