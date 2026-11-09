Shader "Creek Diorama/Assembly Bounds"
{
    Properties
    {
        _Tint("Glass tint", Color) = (.035,.43,.57,1)
        [HDR] _EdgeColor("Cyan edge", Color) = (.08,1.7,2.2,1)
        [HDR] _CornerColor("Bright corner", Color) = (.65,2.8,3.1,1)
        _FaceOpacity("Side transparency", Range(0,.25)) = .055
        _FloorOpacity("Floor transparency", Range(0,.3)) = .07
        _EdgeWidth("Edge width (metres)", Range(.001,.03)) = .004
        _GlowWidth("Soft inner glow (metres)", Range(.002,.1)) = .025
        _CornerLength("Corner bracket length (metres)", Range(.01,.3)) = .065
        _GridSpacing("Floor grid spacing (metres)", Float) = .1
        _GridWidth("Grid width (metres)", Range(.0005,.005)) = .001
        _GridOpacity("Grid opacity", Range(0,1)) = .22
        _BevelRadius("Rounded corners (metres)",Range(.001,.1))=.025
        _EmblemScale("Direction emblem scale",Range(.1,1))=.52
        _SurfaceMode("0 bounds, 1 tabletop, 2 combined",Float)=2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+20" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint, _EdgeColor, _CornerColor;
            float _FaceOpacity,_FloorOpacity,_EdgeWidth,_GlowWidth,_CornerLength;
            float _GridSpacing,_GridWidth,_GridOpacity;
            float _BevelRadius;
            float _EmblemScale,_SurfaceMode;
            CBUFFER_END
            struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float3 local:TEXCOORD0; float3 scale:TEXCOORD1; float3 normalOS:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A i)
            {
                V o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local=i.positionOS.xyz;o.normalOS=i.normalOS;
                o.scale=float3(length(TransformObjectToWorldDir(float3(1,0,0),false)),length(TransformObjectToWorldDir(float3(0,1,0),false)),length(TransformObjectToWorldDir(float3(0,0,1),false)));
                float r=min(_BevelRadius,min(o.scale.x,min(o.scale.y,o.scale.z))*.2);
                float3 physical=i.positionOS.xyz*o.scale;
                float3 inner=clamp(physical,-o.scale*.5+r,o.scale*.5-r);
                float3 rounded=inner+normalize(physical-inner)*r;
                o.positionCS=TransformObjectToHClip(rounded/max(o.scale,.00001));
                return o;
            }
            half4 frag(V i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 d=max(0,.5-abs(i.local))*i.scale;
                // Second-smallest distance selects all twelve edges of a unit cube.
                float edge=min(max(d.x,d.y),min(max(d.x,d.z),max(d.y,d.z)));
                float corner=max(d.x,max(d.y,d.z));
                float bracket=1-smoothstep(_CornerLength,_CornerLength+_EdgeWidth*2,corner);
                float aa=max(fwidth(edge),.0003);
                float width=_EdgeWidth*lerp(1,3.2,bracket);
                float edgeMask=1-smoothstep(width-aa,width+aa,edge);
                float glow=exp2(-edge/max(_GlowWidth,.0001)*3)*.45;
                float floorMask=step(i.normalOS.y,-.5);
                if(_SurfaceMode<.5 && floorMask>.5) discard;
                if(_SurfaceMode>.5 && _SurfaceMode<1.5 && floorMask<.5) discard;
                float sideMask=1-abs(i.normalOS.y);
                float2 p=i.local.xz*i.scale.xz/max(_GridSpacing,.001);
                float2 gridDist=abs(frac(p+.5)-.5)*max(_GridSpacing,.001);
                float2 gridAA=max(fwidth(p)*max(_GridSpacing,.001),.0002);
                float2 lines=1-smoothstep(_GridWidth-gridAA,_GridWidth+gridAA,gridDist);
                float grid=max(lines.x,lines.y)*floorMask*_GridOpacity;
                float2 meters=i.local.xz*i.scale.xz;
                float radius=min(i.scale.x,i.scale.z)*.29*_EmblemScale;
                float radial=length(meters);
                float ringAA=max(fwidth(radial),.0005);
                float ring=1-smoothstep(.005,.005+ringAA,abs(radial-radius));
                float angle=atan2(meters.y,meters.x);
                float broken=step(.22,frac(angle*3.1831+_Time.y*.035));
                ring+= (1-smoothstep(.005,.005+ringAA,abs(radial-radius-.036*_EmblemScale)))*broken;
                ring+= (1-smoothstep(.002,.002+ringAA,abs(radial-radius+.025*_EmblemScale)))*.35;
                float halo=exp2(-abs(radial-radius)*95)*.16;
                float2 arrow=meters/max(radius,.01);
                arrow.y=-arrow.y;
                float shaft=step(abs(arrow.x),.11)*step(-.38,arrow.y)*step(arrow.y,.18);
                float tip=step(.16,arrow.y)*step(arrow.y,.57)*step(abs(arrow.x),(.57-arrow.y)*.9);
                float emblem=(ring+halo+max(shaft,tip)*.8)*floorMask;
                float scan=pow(saturate(.5+.5*sin(i.local.y*10-_Time.y*1.2)),18)*.015*sideMask;
                float face=sideMask*_FaceOpacity+floorMask*_FloorOpacity+scan;
                float alpha=saturate(face+edgeMask*.88+glow+grid);
                alpha=saturate(alpha+emblem*.8);
                half3 rgb=_Tint.rgb*face+lerp(_EdgeColor.rgb,_CornerColor.rgb,bracket)*(edgeMask*.85+glow)+_EdgeColor.rgb*(grid*.25+emblem);
                return half4(rgb,alpha);
            }
            ENDHLSL
        }
    }
}
