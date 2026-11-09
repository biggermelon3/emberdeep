"""Original low-poly environment. Run with Blender 4.3 --background --python this_file.
No downloaded assets. Blender source, FBX environment and reusable prop kit are generated.
"""
import bpy, math, random, os, json
from mathutils import Vector

random.seed(83)
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../..'))
SOURCE = os.path.join(ROOT, 'ArtSource/CreekDiorama')
EXPORT = os.path.join(ROOT, 'Assets/CreekDiorama/Models')
PREVIEW = os.path.join(ROOT, 'Docs/CreekDiorama')
for folder in (SOURCE, EXPORT, PREVIEW): os.makedirs(folder, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for data in list(bpy.data.collections):
    if data.name != 'Collection': bpy.data.collections.remove(data)
base_collection = bpy.data.collections.get('Collection')
base_collection.name = '00 Studio (not exported)'
collections = {}
for name in ['01 Display base', '02 Rock terraces', '03 Meadow caps', '04 Timber bridge', '05 Pines', '06 Undergrowth', '07 River', '08 Waterfalls', '09 Trail signs']:
    c = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(c); collections[name] = c
active = collections['01 Display base']
mats = {}
def mat(name, color, rough=.7, metallic=0, emission=0):
    m = bpy.data.materials.new(name); m.diffuse_color = (*color, 1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Roughness'].default_value=rough; p.inputs['Metallic'].default_value=metallic
    if emission: p.inputs['Emission Color'].default_value=(*color,1); p.inputs['Emission Strength'].default_value=emission
    mats[name]=m; return m
slate=mat('Basalt enamel',(.095,.135,.17),.36,.35)
edge=mat('Blue steel edge',(.20,.27,.29),.38,.55)
brass=mat('Satin copper',(.47,.25,.095),.32,.65)
dark=mat('Deep shadow',(.035,.056,.064))
amber=mat('Amber lamps',(1,.34,.035),.3,0,3)
woods=[mat('Honey timber %02d'%i,c,.56) for i,c in enumerate([(.64,.31,.09),(.78,.42,.14),(.89,.53,.22),(.72,.35,.105),(.82,.46,.18)])]
endgrain=mat('Timber end grain',(.38,.17,.055))
rope=mat('Natural hemp',(.57,.40,.19))
stone=[mat('Slate rock %02d'%i,c) for i,c in enumerate([(.34,.36,.36),(.44,.43,.39),(.52,.49,.42),(.41,.44,.43),(.56,.53,.46),(.37,.39,.42)])]
grass=[mat('Moss %02d'%i,c) for i,c in enumerate([(.34,.49,.075),(.47,.61,.11),(.56,.65,.15),(.26,.40,.07)])]
pine=[mat('Pine needles %02d'%i,c) for i,c in enumerate([(.075,.23,.12),(.13,.33,.16),(.23,.42,.16),(.32,.48,.16)])]
bark=mat('Tree bark',(.25,.13,.055))
flowers=[mat('Buttercup gold',(1,.67,.055)),mat('Cream petals',(.96,.87,.57))]
water=[mat('River turquoise %02d'%i,c,.21,.2) for i,c in enumerate([(.025,.40,.50),(.025,.48,.56),(.045,.54,.60),(.065,.45,.55),(.07,.58,.63)])]
foam=mat('Whitewater',(.63,.94,.92),.35,0,.12)
blue=mat('Start cobalt',(.025,.21,.61),.55)
red=mat('Goal vermilion',(.75,.075,.025),.55)
white=mat('Warm lettering',(.99,.92,.71),.5)

def link(obj,name,material=None):
    obj.name=name
    for c in list(obj.users_collection): c.objects.unlink(obj)
    active.objects.link(obj)
    if material: obj.data.materials.append(material)
    return obj
def cube(name,loc,size,material,bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o=link(bpy.context.object,name,material)
    o.dimensions=size; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        b=o.modifiers.new('Hand softened corners','BEVEL'); b.width=bevel; b.segments=1
        n=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL')
    return o
def mesh(name,verts,faces,materials,indices=None):
    d=bpy.data.meshes.new(name); d.from_pydata(verts,[],faces); d.update()
    o=bpy.data.objects.new(name,d); active.objects.link(o)
    for m in materials: d.materials.append(m)
    if indices:
        for p,i in zip(d.polygons,indices):p.material_index=i
    return o
def cylinder(name,loc,r,depth,material,vertices=10):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=loc)
    return link(bpy.context.object,name,material)
def beam(name,a,b,width,material,depth=None):
    a,b=Vector(a),Vector(b); d=b-a
    o=cube(name,(a+b)/2,(width,depth or width,d.length),material,.015)
    o.rotation_euler=d.to_track_quat('Z','Y').to_euler(); return o
def line(name,points,r,material):
    d=bpy.data.curves.new(name,'CURVE'); d.dimensions='3D'; d.resolution_u=1
    d.bevel_depth=r; d.bevel_resolution=0; d.resolution_u=1
    s=d.splines.new('POLY');s.points.add(len(points)-1)
    for p,v in zip(s.points,points):p.co=(*v,1)
    o=bpy.data.objects.new(name,d); active.objects.link(o);d.materials.append(material); return o
def rock(name,loc,size):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=loc)
    o=link(bpy.context.object,name);o.scale=size;o.rotation_euler=(random.uniform(-.25,.25),random.uniform(-.2,.2),random.random()*6.28)
    for m in stone:o.data.materials.append(m)
    for p in o.data.polygons:p.material_index=random.randrange(len(stone))
    return o
def polygon_prism(name,outline,low,high,materials):
    n=len(outline);v=[(x,y,low) for x,y in outline]+[(x,y,high) for x,y in outline]
    f=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,v,f,materials,[0,0]+[random.randrange(len(materials)) for _ in range(n)])

# Eight chamfered corners and an inset tub give the miniature a real manufactured edge.
outline=[(-6.65,-3.7),(6.65,-3.7),(7.15,-3.2),(7.15,3.2),(6.65,3.7),(-6.65,3.7),(-7.15,3.2),(-7.15,-3.2)]
polygon_prism('Octagonal plinth',outline,.08,.70,[slate])
polygon_prism('Copper belt',[(x*1.005,y*1.005) for x,y in outline],.22,.38,[brass])
polygon_prism('Raised steel rim',outline,.69,.83,[edge])
polygon_prism('River bed inset',[(x*.975,y*.955) for x,y in outline],.80,.86,[dark])
for i in range(8):
    a=Vector((*outline[i],.86));b=Vector((*outline[(i+1)%8],.86));beam('Rim cap',a,b,.10,edge,.11)
for x in [-5.8,-2.9,0,2.9,5.8]:
    cube('Recessed lamp socket',(x,-3.724,.49),(1.07,.05,.24),dark,.035)
    cube('Warm amber strip',(x,-3.758,.49),(.79,.025,.09),amber,.025)
    for dx in [-.59,.59]:
        cube('Plinth reinforcement',(x+dx,-3.72,.46),(.15,.13,.62),edge,.025)
        for z in [.25,.67]:
            o=cylinder('Copper rivet',(x+dx,-3.805,z),.028,.02,brass,8);o.rotation_euler.x=math.pi/2

# Island masses are terraced polygonal columns, with individually fractured cliff faces.
active=collections['02 Rock terraces']
left=[(-6.8,-2.5),(-5.65,-2.95),(-4.65,-2.65),(-3.9,-1.7),(-3.85,.8),(-4.6,1.55),(-4.9,2.85),(-6.6,3.1),(-6.95,1.8)]
right=[(3.95,-1.3),(4.2,-2.45),(5.65,-2.8),(6.8,-2.4),(6.95,2.5),(6.1,3.05),(4.9,2.8),(4.25,1.5)]
def terrain(name,poly,top):
    active_old=active
    polygon_prism(name,poly,.86,top,stone)
    for a,b in zip(poly,poly[1:]+poly[:1]):
        dist=(Vector(a)-Vector(b)).length
        for j in range(max(1,int(dist/.55))):
            t=(j+.4)/max(1,int(dist/.55));x=a[0]*(1-t)+b[0]*t;y=a[1]*(1-t)+b[1]*t
            if abs(x-5.25)<.57 and y<-2.22: continue # open notch for the main waterfall
            for k in range(3):
                z=.98+(top-.9)*(k+.45)/3
                rock('Fractured cliff',(x+random.uniform(-.09,.09),y+random.uniform(-.09,.09),z),(.42,.35,(top-.85)/5))
    return top
terrain('West highland',left,3.30);terrain('East highland',right,2.98)
terraces=[(-5.1,-2.55,1.55,.9,.55),(-6.45,-2.60,2.03,.65,.55),(-4.05,1.7,1.83,.80,.90),(4.5,-2.55,1.45,.8,.63),(6.1,-2.8,1.83,.78,.43),(3.95,2.2,1.75,.75,.9),(-2.8,2.85,1.1,.85,.38),(1.9,3.0,1.12,1.3,.35)]
caps=[]
for x,y,z,sx,sy in terraces:
    pts=[(x+math.cos(i*math.tau/7)*sx,y+math.sin(i*math.tau/7)*sy) for i in range(7)]
    terrain('Stepped ledge',pts,z);caps.append((pts,z))
active=collections['03 Meadow caps']
def cap(name,poly,z):
    polygon_prism(name,poly,z-.015,z+.09,grass[:2])
    # Short overhangs visually separate the green sod from vertical rock strata.
    for a,b in zip(poly,poly[1:]+poly[:1]):
        n=max(1,int((Vector(a)-Vector(b)).length/.24))
        for j in range(n):
            t=(j+.5)/n;x=a[0]*(1-t)+b[0]*t;y=a[1]*(1-t)+b[1]*t
            mesh('Sod fringe',[(x-.10,y,z+.02),(x+.10,y,z+.02),(x+.02,y,z-random.uniform(.04,.18))],[(0,1,2)],[random.choice(grass)])
cap('West meadow',left,3.30);cap('East meadow',right,2.98)
for pts,z in caps:cap('Ledge moss',pts,z)

# Curve-shaped plank deck with separate underside beams, trestles, joinery and rope.
active=collections['04 Timber bridge']
def deck(x):
    # Flat landings sit above the sod; the suspended span joins both banks continuously.
    t=max(0,min(1,(x+3.95)/7.9))
    return 1.95+1.45*(2*t-1)**2-.32*t
def slope(x): return (deck(x+.01)-deck(x-.01))/.02
count=52
for i in range(count):
    x=-5.25+(i+.5)*10.5/count; z=deck(x)
    o=cube('Deck plank %02d'%i,(x,.15,z),(10.5/count-.012,1.68,.16),woods[i%5],.018)
    o.rotation_euler.y=-math.atan(slope(x))
    for y in [-.53,.83]:
        cylinder('Forged nail',(x,y,z+.09),.016,.012,dark,6)
    if i%3==0:
        line('Wood grain',[(x-.035,-.59,z+.086),(x+.015,-.15,z+.087),(x-.018,.7,z+.087)],.006,endgrain)
for y in [-.48,.78]:
    for i in range(20):
        a=-5.25+i*10.5/20;b=-5.25+(i+1)*10.5/20
        beam('Continuous underside stringer',(a,y,deck(a)-.17),(b,y,deck(b)-.17),.17,woods[0],.22)
for x in [-3.6,-1.45,1.25,3.5]:
    z=deck(x)
    for y in [-.45,.75]:
        cube('Stone pier footing',(x,y,1.02),(.52,.55,.29),stone[2],.045)
        beam('Trestle upright',(x,y,1.13),(x,y,z-.19),.19,woods[0])
        beam('Diagonal knee',(x,y,z-.66),(x+.53,y,z-.17),.12,woods[1])
        beam('Diagonal knee',(x,y,z-.66),(x-.53,y,z-.17),.12,woods[1])
    cube('Cross girder',(x,.15,z-.22),(.24,2.0,.18),woods[0],.02)
for y in [-.77,1.07]:
    for i in range(13):
        x=-5.1+i*.85;z=deck(x)
        beam('Rope post',(x,y,z-.10),(x,y,z+.52),.075,woods[2])
        cylinder('Post cap',(x,y,z+.54),.066,.07,brass,8)
        if i<12:
            points=[(x+t*.85,y,deck(x+t*.85)+.43-.10*math.sin(t*math.pi)) for t in [j/8 for j in range(9)]]
            line('Sagging hemp rail',points,.024,rope)

# A deliberately chunky, three-tier fir silhouette rather than smooth cones.
active=collections['05 Pines']
def tree(x,y,z,h):
    before=set(active.objects)
    trunk=cylinder('Pine trunk',(x,y,z+h*.36),h*.055,h*.72,bark,7)
    for tier in range(3):
        radius=h*(.31-tier*.065); base=z+h*(.18+tier*.22); tip=z+h*(.69+tier*.16)
        verts=[]
        for i in range(9):
            a=i*math.tau/9; r=radius*random.uniform(.88,1.10)
            verts.append((x+math.cos(a)*r,y+math.sin(a)*r,base+random.uniform(-.045,.045)*h))
        verts.extend([(x+.035*h,y,tip),(x,y,base+.025*h)])
        faces=[(i,(i+1)%9,9) for i in range(9)]+[(10,(i+1)%9,i) for i in range(9)]
        mesh('Faceted pine crown',verts,faces,pine,[(i+tier)%4 for i in range(18)])
    root=bpy.data.objects.new('Pine tree',None);active.objects.link(root)
    for obj in set(active.objects)-before-{root}:obj.parent=root
positions=[(-6.1,2.2,3.39,2.25),(-5.2,2.5,3.39,1.45),(-6.25,.9,3.39,1.65),(-6.3,-1.45,3.39,1.50),(-4.45,-1.45,3.39,1.0),(-5.5,-2.35,3.39,.85),(-4.05,1.85,1.92,1.65),(-6.45,-2.55,2.12,1.1),(5.7,2.2,3.07,2.05),(6.45,.9,3.07,1.5),(4.8,1.85,3.07,1.7),(6.2,-1.75,3.07,1.2),(4.2,-1.65,3.07,.85),(4.25,-2.55,1.54,1.0),(3.8,2.15,1.84,1.35),(-2.8,2.85,1.19,1.25),(1.9,2.9,1.21,1.3)]
for p in positions:tree(*p)

active=collections['06 Undergrowth']
def sprig(x,y,z,s=.25):
    verts=[];faces=[]
    for i in range(5):
        a=i*2.4;dx=math.cos(a)*s;dy=math.sin(a)*s;k=len(verts)
        verts.extend([(x-dy*.16,y+dx*.16,z),(x+dy*.16,y-dx*.16,z),(x+dx*.7,y+dy*.7,z+s*random.uniform(.6,1.3))]);faces.append((k,k+1,k+2))
    mesh('Meadow grass tuft',verts,faces,grass,[i%4 for i in range(5)])
for pts,z in [(left,3.39),(right,3.07)]+[(p,z+.09) for p,z in caps]:
    for a,b in zip(pts,pts[1:]+pts[:1]):
        for j in range(3):
            t=(j+.3)/3;x=a[0]*(1-t)+b[0]*t;y=a[1]*(1-t)+b[1]*t
            sprig(x,y,z,random.uniform(.13,.27))
            if j==1:
                for k in range(3):
                    xx=x+random.uniform(-.13,.13);yy=y+random.uniform(-.1,.1)
                    beam('Wildflower stem',(xx,yy,z),(xx,yy,z+.13+k*.035),.012,grass[3])
                    o=cylinder('Wildflower',(xx,yy,z+.15+k*.035),.035,.02,flowers[k%2],5)
for x,y,s in [(-2.2,-1.9,.66),(.7,-2.1,.52),(-.7,1.8,.35),(2.4,.4,.40),(-3.4,-2.9,.28),(3.1,-2.7,.25)]:
    rock('River boulder',(x,y,1.02),(s,s*.76,s*.72))
    for i in range(3): rock('Boulder satellite',(x+random.uniform(-s,s),y+random.uniform(-s,s),.99),(.16,.18,.13))

active=collections['07 River']
# Triangulated opaque water exports cleanly; no external textures required.
verts=[];nx=66;ny=34
for j in range(ny+1):
    for i in range(nx+1):
        x=-6.94+i*13.88/nx;y=-3.50+j*7/ny
        z=.94+.013*math.sin(x*3+y*4)+random.uniform(-.008,.008)
        verts.append((x,y,z))
faces=[]
for j in range(ny):
    for i in range(nx):
        a=j*(nx+1)+i;faces.extend([(a,a+1,a+nx+2),(a,a+nx+2,a+nx+1)])
mesh('Faceted turquoise river',verts,faces,water,[random.choices(range(5),[5,4,2,3,1])[0] for f in faces])
for i in range(135):
    x=random.uniform(-3.5,3.5);y=random.uniform(-3.30,3.25);length=random.uniform(.055,.28)
    line('Current glint',[(x,y,.97),(x+length*.45,y+.025,.974),(x+length,y,.97)],random.uniform(.006,.012),foam if i%3==0 else water[4])
def ripple(x,y,r,z=.98):
    for offset in [0,2.7]:
        line('Broken foam ring',[(x+math.cos(a)*r,y+math.sin(a)*r*.65,z) for a in [offset+i*.15 for i in range(14)]],.017,foam)
for x,y,r in [(-2.2,-1.9,.73),(.7,-2.1,.65),(-.7,1.8,.45),(2.4,.4,.5)]:ripple(x,y,r)

active=collections['08 Waterfalls']
def waterfall(x,y,z,width):
    # Flow travels toward the viewer and drops down the front of the cliff.
    rows=[(y+.35,z+.035),(y,z+.035),(y-.12,z-.10),(y-.18,z-.40),(y-.20,1.25),(y-.30,.99),(y-.43,.965)]
    vertices=[]
    for j,(yy,zz) in enumerate(rows):
        for i in range(9): vertices.append((x-width/2+width*i/8,yy+.02*math.sin(i*3),zz+.012*math.sin(i*2)))
    fs=[]
    for j in range(6):
        for i in range(8):a=j*9+i;fs.append((a,a+1,a+10,a+9))
    mesh('Waterfall curtain',vertices,fs,water+[foam],[i%5 if i%8>1 else 5 for i in range(48)])
    for i in range(7):
        xx=x-width*.43+i*width*.14
        line('Falling whitewater',[(xx,yy-.012,zz+.02) for yy,zz in rows],.013 if i%2 else .022,foam)
    for i in range(20):
        a=random.random()*math.tau;r=random.uniform(.1,width*.8)
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=random.uniform(.025,.06),location=(x+math.cos(a)*r,y-.30+math.sin(a)*r*.30,1.0+random.random()*.22))
        link(bpy.context.object,'Waterfall splash',foam)
    ripple(x,y-.30,width*.60,1.01);ripple(x,y-.35,width*.8,1.0)
waterfall(5.25,-3.02,2.98,.66)
waterfall(-4.18,1.35,1.83,.43)

active=collections['09 Trail signs']
def text_obj(name,body,loc,size,material):
    d=bpy.data.curves.new(name,'FONT');d.body=body;d.align_x='CENTER';d.align_y='CENTER';d.size=size;d.extrude=.002
    o=bpy.data.objects.new(name,d);active.objects.link(o);d.materials.append(material);o.location=loc;o.rotation_euler=(math.pi/2,0,0);return o
def banner(x,y,z,title,material):
    for dx in [-.68,.68]:
        cylinder('Trail banner pole',(x+dx,y,z+.62),.045,1.24,woods[0],8)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=8,ring_count=4,radius=.075,location=(x+dx,y,z+1.26));link(bpy.context.object,'Brass finial',brass)
    cube('Cloth '+title,(x,y,z+.89),(1.30,.035,.49),material,.01)
    text_obj(title+' lettering',title,(x,y-.026,z+.90),.27,white)
banner(-5.35,.98,3.39,'START',blue)
banner(5.25,.90,3.07,'GOAL',red)
active=collections['01 Display base']
cube('Maker plate',(0,-3.789,.49),(1.60,.04,.35),slate,.025)
text_obj('Maker mark','C R E E K  /  0 1',(0,-3.818,.49),.105,white)

# Presentation studio is separate from the asset export.
active=base_collection
floor=mat('Studio warm graphite',(.074,.091,.105),.88)
cube('Studio surface',(0,0,-.15),(200,200,.25),floor)
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.world.color=(.16,.16,.16)
scene.view_settings.view_transform='AgX'
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
def area(name,pos,energy,color,size,target=(0,0,1)):
    d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.color=color;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);active.objects.link(o);o.location=pos;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
area('Warm window',(-6,-5,13),2100,(1,.79,.52),8)
area('Cool sky',(3,5,11),2700,(.59,.78,1),7)
area('Soft frontal fill',(1,-8,7),800,(.82,.91,1),9)
d=bpy.data.cameras.new('Presentation camera');camera=bpy.data.objects.new('Presentation camera',d);active.objects.link(camera);scene.camera=camera
d.type='ORTHO';d.ortho_scale=18.4;d.lens=50
def view(position,target,path,scale=18.4):
    camera.location=position;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();d.ortho_scale=scale
    scene.render.filepath=path;bpy.ops.render.render(write_still=True)

# Apply appearance modifiers to exported duplicates only. Source remains editable.
bpy.ops.object.select_all(action='DESELECT')
asset_objects=[o for c in collections.values() for o in c.objects]
for o in asset_objects:o.select_set(True)
bpy.context.view_layer.objects.active=asset_objects[0]
# Keep each group as its own named hierarchy root in the FBX.
for name,c in collections.items():
    empty=bpy.data.objects.new(name,None);c.objects.link(empty)
    for o in list(c.objects):
        if o!=empty and not o.parent:o.parent=empty
    empty.select_set(True)
# Curve and text objects become meshes in the interchange export.
bpy.ops.object.duplicate()
duplicates=list(bpy.context.selected_objects)
bpy.ops.object.convert(target='MESH')
for o in duplicates:o.select_set(True) # conversion deselects Empty hierarchy roots
bpy.ops.export_scene.fbx(filepath=os.path.join(EXPORT,'CreekDiorama.fbx'),use_selection=True,object_types={'MESH','EMPTY'},global_scale=.2,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
bpy.ops.object.delete(use_global=False)

# Palette data lets Unity create deterministic native URP materials.
with open(os.path.join(SOURCE,'palette.json'),'w') as f:
    json.dump({'materials':[{'name':m.name,'color':list(m.diffuse_color),'metallic':m.node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value,'roughness':m.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value} for m in mats.values() if m!=floor]},f,indent=2)
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=.2
camera.location=(11,-18,13);camera.rotation_euler=(Vector((0,0,1.5))-camera.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SOURCE,'CreekDiorama.blend'))
view((10,-19,14),(0,0,1.65),os.path.join(PREVIEW,'CreekDiorama-Hero.png'))
view((-11,-18,12),(0,0,1.7),os.path.join(PREVIEW,'CreekDiorama-Reverse.png'))
view((9,-12,8),(4.1,-.5,1.9),os.path.join(PREVIEW,'CreekDiorama-Detail.png'),7.8)
print('CREEK_DI0RAMA_COMPLETE '+str(len(asset_objects))+' original objects')
