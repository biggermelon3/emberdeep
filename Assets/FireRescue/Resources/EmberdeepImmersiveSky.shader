Shader "Emberdeep/Rising Forest Sky"
{
    Properties { _Reveal("Bottom to top reveal",Range(0,1))=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Background" }
        Pass
        {
            Name "ViewRelativeSky"
            Tags { "LightMode"="UniversalForward" }
            Cull Front ZWrite Off ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ColorMask RGBA
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Reveal,_SkyTime;float4 _MaskForward,_MaskRight,_RevealRange,_MaskAxis,_MaskCos;
            CBUFFER_END
            float puff(float2 p,float2 c,float2 radius){return length((p-c)/radius)-1;}
            struct A { float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=-GetWorldSpaceNormalizeViewDir(i.world);
                float elevation=asin(clamp(d.y,-1,1));
                float coverage=smoothstep(_MaskCos.x,_MaskCos.y,dot(d,_MaskAxis.xyz));
                coverage*=1-smoothstep(1.22,1.36,abs(elevation));
                float edge=lerp(_RevealRange.x-.15,_RevealRange.y+.15,smoothstep(0,1,_Reveal));
                float alpha=coverage*(1-smoothstep(edge-.1,edge+.1,elevation));
                clip(alpha-.001);
                float azimuth=atan2(d.x,d.z);
                half3 color=lerp(half3(.98,.90,.74),half3(.42,.72,.92),smoothstep(-.10,.42,elevation));
                // A low warm sun glow sits behind the forest on the player's left.
                color+=half3(.20,.12,.02)*exp(-8*((azimuth+.9)*(azimuth+.9)*.35+(elevation-.02)*(elevation-.02)*3));
                // Broad painted hills connect the sky palette to the miniature.
                float hills=-.20+.055*sin(azimuth*3+.7)+.026*sin(azimuth*7);
                float aa=max(fwidth(elevation)*1.5,.003);
                float farHills=-.11+.045*sin(azimuth*2.3+2.1)+.02*sin(azimuth*5.1);
                color=lerp(color,half3(.70,.82,.74),1-smoothstep(farHills-aa,farHills+aa,elevation));
                color=lerp(color,half3(.47,.68,.50),1-smoothstep(hills-aa,hills+aa,elevation));
                // The nearest ridge carries a soft conifer tree line.
                float nearHills=-.38+.06*sin(azimuth*4-1.5)+.014*abs(sin(azimuth*38))*smoothstep(.2,.8,sin(azimuth*5+1));
                color=lerp(color,half3(.30,.52,.33),1-smoothstep(nearHills-aa,nearHills+aa,elevation));
                // Six world-oriented cloud groups, each a union of rounded lobes.
                // Distance derivatives soften every contour at the headset pixel footprint.
                float cloud=10,cloudY=0;
                [unroll] for(int k=0;k<6;k++)
                {
                    float centre=k*1.0472-2.6+_SkyTime*(.006+.0015*sin(k*2.1));
                    float x=atan2(sin(azimuth-centre),cos(azimuth-centre));
                    float y=elevation-(.25+.13*sin(k*2.4)+.008*sin(_SkyTime*.18+k));
                    float2 p=float2(x,y);
                    float c=puff(p,float2(0,0),float2(.19,.065));
                    c=min(c,puff(p,float2(-.11,.025),float2(.10,.075)));
                    c=min(c,puff(p,float2(-.025,.065),float2(.10,.10)));
                    c=min(c,puff(p,float2(.08,.04),float2(.095,.08)));
                    c=min(c,puff(p,float2(.16,.002),float2(.07,.05)));
                    if(c<cloud){cloud=c;cloudY=y;}
                }
                float cloudAA=max(fwidth(cloud),.015);
                half3 cloudColor=lerp(half3(.93,.82,.72),half3(1,.98,.90),smoothstep(-.035,.065,cloudY));
                color=lerp(color,cloudColor,1-smoothstep(-cloudAA,cloudAA,cloud));
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
