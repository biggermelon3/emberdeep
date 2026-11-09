"""Independent art kit. Unity coordinates: +Z forward, 0.2 m placement grid.
Run Blender --background --python this_file. Does not touch gameplay scenes.
"""
import bpy, math, os, json, random
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
palette=json.load(open(os.path.join(ROOT,'ArtSource/CreekDiorama/palette.json')))
mats={}
for e in palette['materials']:
    m=bpy.data.materials.new(e['name']);m.diffuse_color=tuple(e['color']);m.use_nodes=True
    n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=tuple(e['color']);n.inputs['Roughness'].default_value=e['roughness'];n.inputs['Metallic'].default_value=e['metallic'];mats[e['name']]=m
wood=[mats['Honey timber %02d'%i] for i in range(5)]
roots=[];root=None;anchors={};random.seed(17)
def point(p):return (-p[0],-p[2],p[1])
def module(name):
    global root
    root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root);roots.append(root);anchors[name]={};return root
def attach(o,name,mat):
    o.name=name;o.parent=root
    if mat:o.data.materials.append(mat)
    return o
def box(name,p,size,mat,bevel=.004):
    bpy.ops.mesh.primitive_cube_add(size=1,location=point(p));o=attach(bpy.context.object,name,mat);o.dimensions=(size[0],size[2],size[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Soft timber edges','BEVEL');mod.width=bevel;mod.segments=1
        o.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
    return o
def beam(name,a,b,w,mat):
    av,bv=Vector(point(a)),Vector(point(b));o=box(name,((a[0]+b[0])/2,(a[1]+b[1])/2,(a[2]+b[2])/2),(w,(bv-av).length,w),mat,w*.12)
    o.rotation_euler=(bv-av).to_track_quat('Z','Y').to_euler();return o
def anchor(name,p):anchors[root.name][name]=p
def deck(length=.4,rise=0,width=.4,steps=0):
    count=steps or round(length/.05)
    for i in range(count):
        z=(i+.5)*length/count
        y=(i+1)*rise/count if steps else rise*z/length
        o=box('Deck plank %02d'%i,(0,y-.018,z),(width,.036,length/count-.002),wood[i%5])
        if not steps:o.rotation_euler.x=-math.atan2(rise,length)
        for x in [-width*.42,width*.42]:box('Copper nail',(x,y+.001,z),(.006,.003,.006),mats['Satin copper'],.001)
    for x in [-width*.38,width*.38]:beam('Underside stringer',(x,-.045,0),(x,rise-.045,length),.025,wood[0])
    anchor('Entry',(0,0,0));anchor('Exit',(0,rise,length))
for length in [.4,.8]:module('Deck_Straight_%03d'%round(length*100));deck(length)
module('Deck_Ramp_080_Rise020');deck(.8,.2)
module('Deck_Stairs_080_Rise020');deck(.8,.2,steps=8)
module('Stair_Tread_040');deck(.1)
module('Deck_Turn90_080')
for i in range(16):box('Turn plank',(0,-.018,(i+.5)*.05),(.4,.036,.048),wood[i%5])
for i in range(8):box('Exit plank',(.2+(i+.5)*.05,-.018,.6),(.048,.036,.4),wood[i%5])
anchor('Entry',(0,0,0));anchor('Exit',(.6,0,.6));anchors[root.name]['ExitYaw']=90
for height in [.2,.4,.6]:
    module('Support_Pier_%03d'%round(height*100))
    for x in [-.14,.14]:
        box('Stone foot',(x,.025,0),(.095,.05,.10),mats['Slate rock 02'],.012)
        box('Timber post',(x,height*.5,0),(.035,height,.035),wood[0])
    beam('Cross brace',(-.14,.055,0),(.14,height-.055,0),.021,wood[2])
    box('Cross head',(0,height-.015,0),(.4,.03,.055),wood[1]);anchor('Foot',(0,0,0));anchor('Deck',(0,height,0))
module('Railing_040')
for z in [0,.4]:box('Railing post',(0,.09,z),(.018,.18,.018),wood[0])
for y in [.08,.16]:beam('Rope rail',(0,y,0),(0,y,.4),.008,mats['Natural hemp'])
anchor('Entry',(0,0,0));anchor('Exit',(0,0,.4))
module('Landing_Platform_080');deck(.8,width=.8)
for x in [-.37,.37]:box('Copper trim',(x,-.025,.4),(.018,.045,.8),mats['Satin copper'])
anchor('Center',(0,0,.4))
def sign(name,text,color):
    module(name)
    for x in [-.24,.24]:
        box('Timber post',(x,.22,0),(.028,.44,.028),wood[0])
        box('Copper shoe',(x,.014,0),(.05,.028,.05),mats['Satin copper'])
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.025,location=point((x,.46,0)));attach(bpy.context.object,'Golden finial',mats['Buttercup gold'])
    box('Banner',(0,.36,0),(.48,.14,.012),mats[color],.006)
    for side in [-1,1]:
        c=bpy.data.curves.new(text,'FONT');c.body=text;c.align_x='CENTER';c.align_y='CENTER';c.size=.09;c.extrude=.0004;c.offset=.0008
        o=bpy.data.objects.new(text,c);bpy.context.collection.objects.link(o);attach(o,text,mats['Warm lettering']);o.location=point((0,.36,side*.008));o.rotation_euler=(math.pi/2,0,math.pi if side==-1 else 0)
    anchor('Gate',(0,0,0));anchor('Banner',(0,.36,0))
sign('Marker_Start','START','Start cobalt');sign('Marker_Goal','GOAL','Goal vermilion')
module('Portal_Frame')
for x in [-.24,.24]:
    box('Frame upright',(x,.22,0),(.045,.44,.05),wood[1]);box('Copper base',(x,.025,0),(.075,.05,.075),mats['Satin copper'])
box('Frame lintel',(0,.44,0),(.55,.05,.05),wood[2]);box('Blue inset',(0,.44,-.029),(.40,.022,.008),mats['Start cobalt'],.002)
anchor('Threshold',(0,0,0));anchor('EffectCenter',(0,.22,0))
module('Water_Tile_080')
v=[point(p) for p in [(-.4,0,0),(.4,0,0),(.4,0,.8),(-.4,0,.8)]]
d=bpy.data.meshes.new('Water plane');d.from_pydata(v,[],[(0,1,2,3)]);o=bpy.data.objects.new('Water plane',d);bpy.context.collection.objects.link(o);attach(o,'Water plane',mats['River turquoise 00']);anchor('Entry',(0,0,0));anchor('Exit',(0,0,.8))
module('Waterfall_020x040')
verts=[];faces=[];rows=[(-.07,.4),(0,.4),(.035,.37),(.045,.28),(.05,.14),(.075,.025),(.13,0)]
for z,y in rows:
    for i in range(13):verts.append(point((-.1+i/12*.2,y,z)))
for j in range(6):
    for i in range(12):a=j*13+i;faces.append((a,a+1,a+14,a+13))
d=bpy.data.meshes.new('Continuous waterfall');d.from_pydata(verts,[],faces);d.uv_layers.new()
for p in d.polygons:
    for li in p.loop_indices:
        vi=d.loops[li].vertex_index;d.uv_layers.active.data[li].uv=(vi%13/12,vi//13/6)
o=bpy.data.objects.new('Waterfall curtain',d);bpy.context.collection.objects.link(o);attach(o,'Waterfall curtain',mats['River turquoise 00']);anchor('Lip',(0,.4,0));anchor('Impact',(0,0,.1))
module('Cliff_Terrace_040')
for layer in range(3):
    for i in range(3):
        p=((i-1)*.13,.06+layer*.12,.2+random.uniform(-.025,.025))
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=point(p));o=attach(bpy.context.object,'Faceted cliff',mats['Slate rock %02d'%((i+layer)%6)]);o.scale=(.13,.18,.09)
box('Moss cap',(0,.36,.2),(.41,.018,.37),mats['Moss 03'],.035);anchor('Foot',(0,0,.2));anchor('Top',(0,.37,.2))
# Modules stay at the same local origin in FBX; display arrangement belongs only to the .blend source.
for o in list(bpy.context.selected_objects):o.select_set(False)
for r in roots:
    r.select_set(True)
    for o in r.children:o.select_set(True)
bpy.context.view_layer.objects.active=roots[0]
bpy.ops.object.duplicate();copies=list(bpy.context.selected_objects)
bpy.context.view_layer.objects.active=next(o for o in copies if o.type=='MESH');bpy.ops.object.convert(target='MESH')
for o in copies:o.select_set(True)
path=os.path.join(ROOT,'Assets/CreekDiorama/Models/CreekModularKit.fbx')
bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
bpy.ops.object.delete(use_global=False)
for i,r in enumerate(roots):r.location=point(((i%5)*1.1,0,(i//5)*1.3))
bpy.context.scene.unit_settings.system='METRIC'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT,'ArtSource/CreekDiorama/CreekModularKit.blend'))
with open(os.path.join(ROOT,'ArtSource/CreekDiorama/module_anchors.json'),'w') as f:json.dump(anchors,f,indent=2)
print('MODULAR_KIT_READY',len(roots))
