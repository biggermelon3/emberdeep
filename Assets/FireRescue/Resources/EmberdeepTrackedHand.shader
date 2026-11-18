Shader "Emberdeep/Tracked Hand Interior Occlusion"
{
    Properties
    {
        _HandColor("Suede back",Color)=(.20,.33,.11,1)
        _PalmColor("Leather palm",Color)=(.30,.165,.085,1)
        _PatchColor("Cream panel",Color)=(.90,.82,.64,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "TrackedHand"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _HandColor,_PalmColor,_PatchColor;
            float4x4 _HandClipWorldToLocal[12];
            float4 _HandClipHalfSize[12];
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;half3 mask:TEXCOORD3;UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A input)
            {
                V output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                output.normal=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;
                // Glove panels are baked per vertex with alpha 0. A mesh without them
                // reads white (alpha 1) and stays plain suede.
                output.mask=input.color.rgb*(1-input.color.a);
                output.positionCS=TransformWorldToHClip(output.world);return output;
            }
            half4 frag(V input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                [unroll] for(int i=0;i<12;i++)
                {
                    if(_HandClipHalfSize[i].w>.5)
                    {
                        float3 p=abs(mul(_HandClipWorldToLocal[i],float4(input.world,1)).xyz)-_HandClipHalfSize[i].xyz;
                        clip(max(p.x,max(p.y,p.z)));
                    }
                }
                half3 n=normalize(input.normal),view=GetWorldSpaceNormalizeViewDir(input.world);
                // R palm leather, G fingertip caps, B cream panel behind the crystal.
                half palm=smoothstep(.40,.60,input.mask.r);
                half cap=smoothstep(.40,.60,input.mask.g);
                half panel=smoothstep(.38,.58,input.mask.b)*(1-palm);
                half3 pigment=lerp(_HandColor.rgb,_PalmColor.rgb,palm);
                pigment=lerp(pigment,_PatchColor.rgb,panel);
                pigment=lerp(pigment,lerp(_PalmColor.rgb*1.18,_HandColor.rgb*1.25,palm),cap*.92);
                // Sewn seams: a dark line where two panels meet, kept wider than a pixel.
                half seamPalm=1-smoothstep(.5,1.6,abs(input.mask.r-.5)/max(fwidth(input.mask.r)*2.2,.0001));
                half seamPanel=(1-smoothstep(.5,1.6,abs(input.mask.b-.48)/max(fwidth(input.mask.b)*2.2,.0001)))*(1-palm);
                half seamCap=1-smoothstep(.5,1.6,abs(input.mask.g-.5)/max(fwidth(input.mask.g)*2.2,.0001));
                pigment*=1-.42*saturate(seamPalm+seamPanel+seamCap);
                // Same soft key and sky/ground ambient as the diorama, so gloves sit in its light.
                Light sun=GetMainLight();
                half3 key=dot(sun.color,half3(.33,.34,.33))>.01?sun.direction:normalize(half3(.3,.8,-.4));
                half3 keyColor=dot(sun.color,half3(.33,.34,.33))>.01?sun.color:half3(1,.93,.82);
                half wrap=saturate(dot(n,key)*.56+.44);
                half sky=saturate(n.y*.5+.5);
                half3 ambient=lerp(half3(.27,.23,.18),half3(.35,.43,.46),sky);
                half3 color=pigment*(ambient+keyColor*half3(1.04,.95,.80)*wrap*wrap*.84);
                // Suede nap: a pale fuzzy rim, strongest on the green back.
                half fresnel=pow(1-saturate(dot(n,view)),2.6);
                color+=lerp(half3(.50,.60,.30),half3(.42,.30,.20),palm)*fresnel*.22*(1-panel*.5);
                float2 weaveUV=input.uv*210;
                float fine=1-smoothstep(.3,1.5,max(fwidth(weaveUV.x),fwidth(weaveUV.y)));
                color*=1+sin(weaveUV.x*6.283)*sin(weaveUV.y*6.283)*.045*fine*(1-palm);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
