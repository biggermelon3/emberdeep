"""Original modeled foliage, stratified rocks and dressing. Blender 4.3 background.
All geometry and seamless surface maps are generated here; no external textures.
Logical coordinates are Unity metres (X, Y up, Z). Source remains fully editable.
"""
import bpy, math, random, os, json
import numpy as np
from mathutils import Vector
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
SOURCE=os.path.join(ROOT,'ArtSource/CreekDiorama')
EXPORT=os.path.join(ROOT,'Assets/CreekDiorama/Models')
TEXTURES=os.path.join(ROOT,'Assets/CreekDiorama/Textures')
os.makedirs(TEXTURES,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(119)
materials={}; roots=[]; root=None
palette=[]
def material(name,color,rough=.8,metal=0):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
    materials[name]=m;palette.append(dict(name=name,color=[*color,1],roughness=rough,metallic=metal));return m
stone=material('Botanical limestone',(.34,.32,.27))
soil=material('Botanical earth',(.075,.057,.022))
leaf=[material('Botanical leaf shadow',(.025,.10,.018)),material('Botanical leaf green',(.075,.24,.025)),material('Botanical leaf sun',(.20,.37,.035))]
moss=material('Botanical moss',(.20,.32,.033))
bark=material('Botanical bark',(.13,.066,.025))
petal=material('Botanical ivory',(.90,.88,.67))
gold=material('Botanical pollen',(.95,.49,.025))
purple=material('Botanical lavender',(.35,.13,.60))
wood=material('Botanical oak',(.36,.16,.045),.65)
iron=material('Botanical bronze',(.16,.11,.055),.42,.6)
lamp=material('Botanical lantern glow',(1,.48,.045),.3)

# Periodic, restrained material detail. Blender noise is evaluated into repeatable maps.
def texture(name,kind):
    n=512; y,x=np.mgrid[0:n,0:n].astype(np.float32)/n
    h=np.zeros_like(x);rng=np.random.default_rng(65)
    for freq,amp in [(4,.035),(12,.027),(32,.018),(80,.009),(160,.004)]:
        grid=rng.uniform(-1,1,(freq,freq));xx=x*freq;yy=y*freq;ix=xx.astype(int);iy=yy.astype(int);u=xx-ix;v=yy-iy;u=u*u*(3-2*u);v=v*v*(3-2*v)
        h+=amp*((1-v)*((1-u)*grid[iy,ix]+u*grid[iy,(ix+1)%freq])+v*((1-u)*grid[(iy+1)%freq,ix]+u*grid[(iy+1)%freq,(ix+1)%freq]))
    if kind=='wood':h=.035*np.sin(math.tau*(x*15+.09*np.sin(y*math.tau*2)))+.014*np.sin(math.tau*(x*49+y))+h*.3
    rgba=np.ones((n,n,4),np.float32);tone=np.clip(.93+h,.75,1)
    for c in range(3):rgba[:,:,c]=tone
    image=bpy.data.images.new(name,width=n,height=n,alpha=True);image.pixels.foreach_set(rgba.ravel());image.filepath_raw=os.path.join(TEXTURES,name+'.png');image.file_format='PNG';image.save()
    dx=(np.roll(h,-1,1)-np.roll(h,1,1))*2;dy=(np.roll(h,-1,0)-np.roll(h,1,0))*2
    normal=np.stack((-dx,-dy,np.ones_like(h)),axis=2);normal/=np.linalg.norm(normal,axis=2)[:,:,None]
    rgba[:,:,:3]=normal*.5+.5
    im=bpy.data.images.new(name+' Normal',width=n,height=n,alpha=True);im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(rgba.ravel());im.filepath_raw=os.path.join(TEXTURES,name+' Normal.png');im.file_format='PNG';im.save()
    return image
rocktex=texture('Limestone surface','stone');woodtex=texture('Oak grain','wood')
for mat,tex in [(stone,rocktex),(wood,woodtex),(bark,woodtex)]:
    nodes=mat.node_tree.nodes;t=nodes.new('ShaderNodeTexImage');t.image=tex
    mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[1].default_value=mat.diffuse_color
    mat.node_tree.links.new(t.outputs['Color'],mix.inputs[2]);mat.node_tree.links.new(mix.outputs[0],nodes.get('Principled BSDF').inputs['Base Color'])
def pt(p):return (-p[0],-p[2],p[1])
def module(name):
    global root
    root=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(root);roots.append(root)
def mesh(name,vertices,faces,mats,indices=None):
    # X reflection changes handedness: reverse winding to preserve outward normals.
    d=bpy.data.meshes.new(name);d.from_pydata([pt(p) for p in vertices],[],[tuple(reversed(f)) for f in faces]);d.update()
    o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.parent=root
    for m in mats:d.materials.append(m)
    if indices:
        for face,i in zip(d.polygons,indices):face.material_index=i
    return o
def box(name,p,size,mat,bevel=.005):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pt(p));o=bpy.context.object;o.name=name;o.parent=root;o.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(mat)
    if bevel:
        m=o.modifiers.new('Worn edges','BEVEL');m.width=bevel;m.segments=2
        o.modifiers.new('Broad plane normals','WEIGHTED_NORMAL')
    return o
def branch(a,b,r,mat=bark,verts=7):
    a,b=Vector(pt(a)),Vector(pt(b));v=b-a
    bpy.ops.mesh.primitive_cone_add(vertices=verts,radius1=r,radius2=r*.65,depth=v.length,location=(a+b)/2)
    o=bpy.context.object;o.parent=root;o.name='Stem';o.rotation_euler=v.to_track_quat('Z','Y').to_euler();o.data.materials.append(mat);return o

class Leaves:
    def __init__(self):self.v=[];self.f=[];self.mi=[]
    def add(self,p,d,length,width,index=None):
        p,d=Vector(p),Vector(d).normalized();side=d.cross(Vector((0,1,0)))
        if side.length<.01:side=Vector((1,0,0))
        side.normalize();up=side.cross(d).normalized();k=len(self.v)
        self.v.extend([p,p+d*length*.25-side*width*.43,p+d*length*.64-side*width*.47,p+d*length,p+d*length*.64+side*width*.47,p+d*length*.25+side*width*.43,p+d*length*.48+up*width*.12])
        self.f.extend([(k+6,k+(i+1)%6,k+i) for i in range(6)])
        c=index if index is not None else random.choices([0,1,2],[1,5,3])[0];self.mi.extend([c]*6)
    def finish(self,name='Modeled leaves',mats=leaf):return mesh(name,self.v,self.f,mats,self.mi)

# Sculpted slab-like rocks: broad faces, chipped corners, subtle UV stone relief.
def fractured_rock(size,top_taper=.92,bottom_taper=1):
    outline=[(-.8,-1),(.63,-1),(1,-.61),(1,.63),(.61,1),(-.68,1),(-1,.58),(-1,-.65)]
    v=[];f=[]
    for ring in range(3):
        shiftx=random.uniform(-.07,.07);shiftz=random.uniform(-.05,.05)
        for j,(x,z) in enumerate(outline):
            taper=(top_taper if ring==2 else bottom_taper if ring==0 else 1)+random.uniform(-.10,.07)
            y=0 if ring==0 else (size[1]*(.48+random.uniform(-.10,.10)) if ring==1 else size[1]*(.94+random.uniform(-.04,.06)))
            v.append(((x*taper+shiftx)*size[0]*.5,y,(z*taper+shiftz)*size[2]*.5))
    for ring in range(2):
        for j in range(8):f.append((ring*8+j,ring*8+(j+1)%8,(ring+1)*8+(j+1)%8,(ring+1)*8+j))
    f.append(tuple(range(16,24)));f.append(tuple(reversed(range(8))))
    o=mesh('Fractured broad rock planes',v,[tuple(reversed(face)) for face in f],[stone])
    mod=o.modifiers.new('Subtle chipped edge','BEVEL');mod.width=.003;mod.segments=1
    o.modifiers.new('Cliff face normals','WEIGHTED_NORMAL');return o
for idx,size in enumerate([(.30,.34,.23),(.25,.24,.28),(.34,.16,.24)]):
    module('Rock_Stratum_'+chr(65+idx));fractured_rock(size)
module('Rock_River_Boulder')
fractured_rock((.25,.14,.19),.55,.8)
module('Rock_Pebble_Cluster')
for i in range(7):
    s=random.uniform(.018,.045);o=box('Pebble',(random.uniform(-.07,.07),s*.4,random.uniform(-.05,.05)),(s,s*.65,s*.8),stone,s*.2);o.rotation_euler.z=random.random()*6.28

module('Grass_Soft_Tuft');l=Leaves()
for i in range(27):
    a=random.random()*math.tau;r=random.random()*.047;p=(math.cos(a)*r,0,math.sin(a)*r);length=random.uniform(.025,.068)
    l.add(p,(math.cos(a)*.55,1,math.sin(a)*.55),length,random.uniform(.003,.006))
l.finish('Curved meadow blades')
module('Fern_Rosette');l=Leaves()
for j in range(9):
    a=j*math.tau/9;d=Vector((math.cos(a),0,math.sin(a)))
    for i in range(8):
        t=i/8;pos=d*(.015+t*.095)+Vector((0,.025+math.sin(t*math.pi)*.045,0))
        for side in [-1,1]:
            sideways=Vector((-d.z,0,d.x))*side
            l.add(pos,sideways+d*.35+Vector((0,.15,0)),.031*(1-t*.7),.012*(1-t*.5),1 if j%3 else 2)
l.finish('Paired fern leaflets')
module('Shrub_Broadleaf');l=Leaves()
def canopy(p,size,mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=10,ring_count=6,radius=1,location=pt(p));o=bpy.context.object;o.parent=root;o.name='Rounded leafy volume';o.scale=(size[0],size[2],size[1]);o.data.materials.append(mat)
    for v in o.data.vertices:v.co*=random.uniform(.91,1.06)
    for face in o.data.polygons:face.use_smooth=True
for j in range(7):
    a=j*2.4;canopy((math.cos(a)*.055,.09+random.random()*.045,math.sin(a)*.055),(.06,.05,.06),leaf[1 if j%3 else 2])
for j in range(12):
    a=j*2.4;r=random.uniform(.025,.09);top=(math.cos(a)*r,random.uniform(.09,.18),math.sin(a)*r);branch((0,0,0),top,.0025)
    for i in range(10):
        p=Vector(top)+Vector((random.uniform(-.045,.045),random.uniform(-.035,.035),random.uniform(-.045,.045)))
        l.add(p,(random.uniform(-1,1),random.uniform(.15,.7),random.uniform(-1,1)),random.uniform(.024,.04),.019)
l.finish('Dense broadleaf canopy')
module('Ivy_Cascade');l=Leaves()
for strand in range(5):
    x=(strand-2)*.032;length=random.uniform(.20,.40);previous=(x,.008,0)
    for i in range(11):
        t=i/10;p=(x+math.sin(t*7+strand)*.012,-t*length,.014+math.sin(t*5)*.01)
        branch(previous,p,.0009,leaf[0],5);previous=p
        l.add(p,((-1 if i%2 else 1)*.8,-.35,.3),random.uniform(.032,.045),.032,random.choice([1,2]))
l.finish('Trailing ivy leaves')
module('Moss_Overhang');l=Leaves()
for i in range(85):
    x=random.uniform(-.15,.15);z=random.uniform(-.045,.045);y=random.uniform(-.009,.009)
    l.add((x,y,z),(random.uniform(-1,1),random.uniform(.1,.6),random.uniform(-1,1)),random.uniform(.012,.027),.009,random.choice([1,2]))
for i in range(35):l.add((random.uniform(-.15,.15),random.uniform(-.035,0),.04),(random.uniform(-.4,.4),-.9,.3),.025,.008,1)
l.finish('Soft mossy lip')

def flower_cluster(lupin=False):
    module('Flowers_Lupin' if lupin else 'Flowers_Daisies');l=Leaves();petals=Leaves()
    for j in range(7 if lupin else 11):
        a=j*2.4;r=random.uniform(.01,.07);x,z=math.cos(a)*r,math.sin(a)*r;h=random.uniform(.12,.22) if lupin else random.uniform(.035,.10)
        branch((x,0,z),(x,h,z),.0013,leaf[0],5)
        for i in range(4):l.add((x,h*.2+i*.012,z),(math.cos(i*2.4),.4,math.sin(i*2.4)),.026,.01)
        if lupin:
            for i in range(9):
                for k in range(4):
                    a=k*1.57+i*.4;petals.add((x,h*.55+i*h*.045,z),(math.cos(a),.4,math.sin(a)),.014*(1-i*.06),.011,0)
        else:
            for k in range(7):
                a=k*math.tau/7;petals.add((x,h,z),(math.cos(a),.1,math.sin(a)),.013,.007,0)
            box('Golden flower heart',(x,h+.001,z),(.007,.003,.007),gold,.002)
    l.finish();petals.finish('Flower petals',[purple if lupin else petal])
flower_cluster();flower_cluster(True)

module('Pine_Branching');branch((0,0,0),(0,.71,0),.023);l=Leaves()
for tier in range(7):
    h=.17+tier*.087;radius=.19*(1-tier*.115)
    verts=[];faces=[];indices=[];n=36
    for ring in range(3):
        for j in range(n):
            a=j*math.tau/n+tier*.55;lobed=1+(.12 if j%4==0 else -.04);r=radius*lobed*[.16,.62,1][ring]
            verts.append((math.cos(a)*r,h+[.13,.065,-.035][ring]+(.012 if j%4 else -.014),math.sin(a)*r))
    for ring in range(2):
        for j in range(n):faces.append((ring*n+j,ring*n+(j+1)%n,(ring+1)*n+(j+1)%n,(ring+1)*n+j));indices.append(1 if ring==0 else (2 if j%9<3 else 1))
    faces.append(tuple(range(2*n,3*n)));indices.append(0)
    o=mesh('Scalloped fir foliage volume',verts,faces,leaf,indices)
    for face in o.data.polygons:face.use_smooth=True
    for j in range(9):
        a=j*math.tau/9+tier*.55;d=Vector((math.cos(a),0,math.sin(a)));base=Vector((0,h,0));tip=base+d*radius+Vector((0,-.045,0));branch(base,tip,.004*(1-tier*.09))
        for k in range(3):
            t=.65+k*.12;p=base+(tip-base)*t
            for side in [-1,1]:
                across=Vector((-d.z,0,d.x))*side
                l.add(p,d*.65+across*.8+Vector((0,.2,0)),(.07-.033*t)*(1-tier*.07),.034*(1-tier*.08),[0,1,1,2][(j+tier)%4])
        l.add(tip,d+Vector((0,-.4,0)),.055*(1-tier*.08),.024,1)
l.add((0,.79,0),(0,1,0),.09,.035,2);l.finish('Layered fir branches')
module('Tree_Round_Canopy');branch((0,0,0),(.025,.47,0),.025);l=Leaves()
for j in range(8):
    a=j*2.4;r=random.uniform(.09,.16);end=(math.cos(a)*r,.42+random.random()*.17,math.sin(a)*r);branch((.01,.22,0),end,.009)
    canopy(end,(.095,.075,.095),leaf[1 if j%3 else 2])
    for k in range(45):
        a=random.random()*6.28;v=random.uniform(-1,1);rr=random.uniform(.09,.12);s=math.sqrt(1-v*v)
        p=Vector(end)+Vector((math.cos(a)*rr*s, v*rr*.7,math.sin(a)*rr*s))
        l.add(p,(math.cos(a),v+.35,math.sin(a)),.034,.025,random.choices([0,1,2],[1,3,5])[0])
l.finish('Sunlit rounded crown')

module('Prop_Lantern')
branch((0,0,0),(0,.33,0),.009,wood);branch((0,.32,0),(.065,.32,0),.007,wood)
box('Warm glass',(.065,.245,0),(.037,.062,.037),lamp,.003)
for y in [.208,.28]:box('Lantern cap',(.065,y,0),(.057,.012,.057),iron,.005)
for x in [.043,.087]:
    for z in [-.022,.022]:branch((x,.21,z),(x,.28,z),.002,iron,5)
branch((.065,.28,0),(.065,.32,0),.002,iron,5)
module('Prop_Barrel')
for j in range(14):
    a=j*math.tau/14;x,z=math.cos(a)*.059,math.sin(a)*.059;o=box('Oak stave',(x,.078,z),(.026,.15,.019),wood,.003);o.rotation_euler.z=-a+math.pi/2
for y in [.025,.125]:
    v=[];f=[]
    for j in range(28):
        a=j*math.tau/28
        for yy in [y-.007,y+.007]:v.append((math.cos(a)*.071,yy,math.sin(a)*.071))
    for j in range(28):f.append((j*2,((j+1)%28)*2,((j+1)%28)*2+1,j*2+1))
    mesh('Forged barrel hoop',v,f,[iron])
for i in range(5):box('Lid plank',((i-2)*.022,.157,0),(.02,.008,math.sqrt(max(.001,.06**2-((i-2)*.022)**2))*2),wood,.002)
module('Prop_Crate');box('Oak crate',(0,.065,0),(.13,.13,.13),wood,.006)
for x in [-.058,.058]:
    for z in [-.069,.069]:box('Corner batten',(x,.065,z),(.014,.13,.012),wood,.002)
for z in [-.072,.072]:branch((-.055,.012,z),(.055,.118,z),.009,wood,4)
module('Water_Lily_Pads');l=Leaves()
for j in range(5):
    a=j*2.4;p=(math.cos(a)*.05,.001,math.sin(a)*.05)
    l.add(p,(math.cos(a),.01,math.sin(a)),.045,.046,1)
l.finish('Floating leaves')

# Convert and join by module, preserving material slots and reusable local origins.
for r in roots:
    bpy.ops.object.select_all(action='DESELECT')
    for child in list(r.children):child.select_set(True)
    bpy.context.view_layer.objects.active=r.children[0];bpy.ops.object.convert(target='MESH');bpy.ops.object.join()
    o=bpy.context.object;o.name=r.name+' mesh'
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    # Smart projection supplies stable material coordinates for the stone and wood maps.
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.15192,island_margin=.015);bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT,'CreekBotanicalKit.fbx'),use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
for i,r in enumerate(roots):r.location=pt(((i%5)*.9,0,(i//5)*1.0))
bpy.context.scene.unit_settings.system='METRIC'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'CreekBotanicalKit.blend'))
with open(os.path.join(SOURCE,'botanical_palette.json'),'w') as f:json.dump({'materials':palette,'modules':[r.name for r in roots]},f,indent=2)
print('BOTANICAL_KIT_READY',len(roots))
