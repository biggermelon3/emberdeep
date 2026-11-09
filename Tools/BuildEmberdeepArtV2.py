"""Original Emberdeep V2 modular kit, generated with Blender 4.3.

Run: blender --background --python Tools/BuildEmberdeepArtV2.py
All authored dimensions below are Unity-style (X right, Y up, Z depth), in metres.
Only this new kit is written; existing FireRescue art and scene files are untouched.
"""
import bpy, bmesh, math, random, json, os, hashlib
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/EmberdeepV2'
EXPORT = ROOT / 'Assets/FireRescue/ArtV2/Models'
for p in [SOURCE, EXPORT]: p.mkdir(parents=True, exist_ok=True)
random.seed(29031)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections): bpy.data.collections.remove(c)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1
scene.render.engine = 'CYCLES'; scene.cycles.samples = 48; scene.cycles.use_denoising = True
scene.render.resolution_x = 1600; scene.render.resolution_y = 1200; scene.render.resolution_percentage = 100
scene.world.color = (.12, .16, .18)
scene.view_settings.view_transform = 'AgX'

# Blender -Y becomes positive Unity Z with this FBX export convention.
def B(p): return (p[0], -p[2], p[1])
palette = {
 'Moss':(.255,.405,.105), 'MossLight':(.42,.54,.15), 'Soil':(.36,.20,.095),
 'Slate':(.23,.285,.29), 'Stone':(.40,.43,.38), 'Path':(.58,.39,.20),
 'Bark':(.28,.135,.052), 'BarkLight':(.40,.225,.09),
 'Cedar':(.115,.29,.12), 'CedarLight':(.29,.43,.14),
 'Leaf':(.31,.46,.13), 'LeafLight':(.52,.61,.20),
 'Fern':(.34,.50,.11), 'Wood':(.54,.28,.095), 'WoodLight':(.72,.43,.18),
 'Terracotta':(.60,.20,.075), 'TerracottaLight':(.78,.32,.12),
 'Brass':(.64,.40,.11), 'EnamelRed':(.70,.065,.028),
 'Teal':(.025,.34,.32), 'Cyan':(.055,.64,.69), 'Rubber':(.025,.055,.06),
 'WindowGlow':(1,.65,.15), 'CreekWater':(.035,.40,.45), 'Foam':(.50,.79,.70),
}
mats = {}
for name, color in palette.items():
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Roughness'].default_value=.62
    if name in ['Brass','Teal','EnamelRed','Cyan']:
        bs.inputs['Metallic'].default_value=.38 if name=='Brass' else .13
        bs.inputs['Roughness'].default_value=.32
    if name=='CreekWater':bs.inputs['Roughness'].default_value=.20;bs.inputs['Metallic'].default_value=.16
    if name=='WindowGlow':bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=.55
    mats[name]=m

class Buffer:
    def __init__(self,name): self.name=name;self.v=[];self.f=[];self.mi=[];self.materials=[]
    def add(self,verts,faces,mat):
        if mat not in self.materials:self.materials.append(mat)
        idx=self.materials.index(mat);off=len(self.v);self.v.extend(verts)
        self.f.extend([tuple(off+i for i in f) for f in faces]);self.mi.extend([idx]*len(faces))
    def build(self,collection):
        me=bpy.data.meshes.new(self.name);me.from_pydata([B(p) for p in self.v],[],self.f);me.update()
        obj=bpy.data.objects.new(self.name,me);collection.objects.link(obj)
        for name in self.materials:me.materials.append(mats[name])
        for poly,i in zip(me.polygons,self.mi):poly.material_index=i
        # Primitives use different construction windings; consistently orient closed shells.
        bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        if self.name in ['Moss','CreekSurface','Roof']:
            for face in bm.faces:
                if face.normal.z < -.2:face.normal_flip()
        bm.to_mesh(me);bm.free();me.update()
        color=me.color_attributes.new(name='ArtTint',type='FLOAT_COLOR',domain='CORNER')
        for poly in me.polygons:
            tone=.94+random.random()*.10
            for li in poly.loop_indices:color.data[li].color=(tone,tone,tone,1)
        return obj

assets={}; groups={}; current=None
def asset(name):
    global current
    current=name;groups[name]={};assets[name]=[]
def g(name):
    if name not in groups[current]:groups[current][name]=Buffer(name)
    return groups[current][name]
def mesh(group,verts,faces,mat):g(group).add(verts,faces,mat)
def box(group,pos,size,mat,yaw=0):
    x,y,z=pos;hx,hy,hz=[s/2 for s in size];c=math.cos(yaw);s=math.sin(yaw)
    verts=[]
    for px,py,pz in [(-hx,-hy,-hz),(hx,-hy,-hz),(hx,-hy,hz),(-hx,-hy,hz),(-hx,hy,-hz),(hx,hy,-hz),(hx,hy,hz),(-hx,hy,hz)]:
        verts.append((x+px*c+pz*s,y+py,z-px*s+pz*c))
    mesh(group,verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat)
def beam(group,a,b,r,mat,n=8,r2=None):
    a,b=Vector(a),Vector(b);delta=b-a;up=delta.normalized()
    side=up.cross(Vector((0,0,1)))
    if side.length<.01:side=up.cross(Vector((1,0,0)))
    side.normalize();across=up.cross(side).normalized();r2=r if r2 is None else r2
    verts=[]
    for pt,rad in [(a,r),(b,r2)]:
        for i in range(n):verts.append(tuple(pt+rad*(math.cos(i*math.tau/n)*side+math.sin(i*math.tau/n)*across)))
    faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh(group,verts,faces,mat)
def prism(group,outline,y0,y1,mat,topscale=1):
    cx=sum(x for x,z in outline)/len(outline);cz=sum(z for x,z in outline)/len(outline);n=len(outline)
    verts=[(x,y0,z) for x,z in outline]+[(cx+(x-cx)*topscale,y1,cz+(z-cz)*topscale) for x,z in outline]
    mesh(group,verts,[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat)
def rock(group,pos,size,mat='Stone',seed=0):
    rng=random.Random(seed);x,y,z=pos;sx,sy,sz=size;n=7
    verts=[]
    for h,scale in [(0,.78),(.38,1),(.85,.63),(1,.22)]:
        for i in range(n):
            a=i*math.tau/n;rr=scale*(.88+rng.random()*.24)
            verts.append((x+math.cos(a)*sx*.5*rr,y+h*sy,z+math.sin(a)*sz*.5*rr))
    faces=[tuple(reversed(range(n))),tuple(range(3*n,4*n))]
    for layer in range(3):
        for i in range(n):faces.append((layer*n+i,layer*n+(i+1)%n,(layer+1)*n+(i+1)%n,(layer+1)*n+i))
    mesh(group,verts,faces,mat)
def leaf(group,a,b,width,mat,bend=.012):
    a,b=Vector(a),Vector(b);delta=b-a;side=delta.cross(Vector((0,1,0)))
    if side.length<.001:side=Vector((1,0,0))
    side.normalize();mid=a+delta*.48;rise=Vector((0,bend,0))
    verts=[tuple(a),tuple(mid+side*width),tuple(b),tuple(mid-side*width),tuple(mid+rise),tuple(mid-rise*.2)]
    mesh(group,verts,[(0,1,4),(1,2,4),(2,3,4),(3,0,4),(0,5,1),(1,5,2),(2,5,3),(3,5,0)],mat)
def fern(group,pos,scale=1,mat='Fern',seed=0):
    rng=random.Random(seed);p=Vector(pos)
    beam(group,p,p+Vector((0,scale*.055,0)),scale*.007,mat,6)
    for frond in range(7):
        angle=frond*math.tau/7+rng.random()*.22;direction=Vector((math.cos(angle),0,math.sin(angle)))
        length=scale*(.19+rng.random()*.07)
        points=[p+direction*length*t+Vector((0,scale*(.055+math.sin(t*2.2)*.17),0)) for t in [0,.25,.5,.75,1]]
        for i in range(4):beam(group,points[i],points[i+1],scale*.003,mat,4)
        side=Vector((-direction.z,0,direction.x))
        for i in range(1,6):
            t=i/6;stem=p+direction*length*t+Vector((0,scale*(.055+math.sin(t*2.2)*.17),0))
            for sign in [-1,1]:leaf(group,stem,stem+direction*scale*.04+side*sign*scale*.065*(1-t*.65),scale*.016*(1-t*.6),mat,scale*.007)
        leaf(group,points[-2],points[-1]+direction*scale*.025,scale*.019,mat,scale*.007)
def tuft(group,pos,scale=1,seed=0):
    rng=random.Random(seed);p=Vector(pos)
    for i in range(5):
        angle=i*math.tau/5;tip=p+Vector((math.cos(angle)*scale*.06,scale*(.08+rng.random()*.10),math.sin(angle)*scale*.06))
        leaf(group,p,tip,scale*.012,'MossLight',scale*.006)

# Terrain: quiet gameplay interior, crafted peripheral silhouette, back creek.
asset('EDV2_Terrain')
outline=[]
for i in range(64):
    a=i*math.tau/64;ca=math.cos(a);sa=math.sin(a)
    # Rounded rectangular footprint; coherent edge irregularity, never beyond authored bounds.
    x=5.42*math.copysign(abs(ca)**.52,ca);z=6+6.42*math.copysign(abs(sa)**.52,sa)
    jitter=1-.012*(1+math.sin(i*2.31));outline.append((x*jitter,6+(z-6)*jitter))
prism('Soil',outline,-.52,-.055,'Soil',1.005)
prism('Slate',[(x*.985,6+(z-6)*.985) for x,z in outline],-.64,-.48,'Slate',1.018)
# Clip the coherent outline at each bank, then fan each separate star-convex cap.
# This produces one surface per bank with no coplanar overlapping filler polygons.
def bank_cap(limit,front):
    clipped=[]
    for i,a in enumerate(outline):
        b=outline[(i+1)%len(outline)];inside_a=(a[1]<=limit) if front else (a[1]>=limit)
        inside_b=(b[1]<=limit) if front else (b[1]>=limit)
        if inside_a:clipped.append(a)
        if inside_a!=inside_b:
            t=(limit-a[1])/(b[1]-a[1]);clipped.append((a[0]+(b[0]-a[0])*t,limit))
    center=(sum(p[0] for p in clipped)/len(clipped),0,sum(p[1] for p in clipped)/len(clipped))
    for i,a in enumerate(clipped):
        b=clipped[(i+1)%len(clipped)]
        mesh('Moss',[center,(a[0],0,a[1]),(b[0],0,b[1])],[(0,2,1)],'Moss')
bank_cap(10.4,True);bank_cap(11.4,False)
for i in range(64):
    x,z=outline[i];nx,nz=outline[(i+1)%64];cx=(x+nx)/2;cz=(z+nz)/2
    rock('Slate',(cx*.99,-.55,6+(cz-6)*.99),(.28,.31,.24),'Slate',i+42)
    if cz>1.6 and i%2==0:tuft('RimGrowth',(cx*.96,.002,6+(cz-6)*.97),.70,i)
# Winding low footpath remains below the existing nozzle and pickup space.
for i in range(17):
    x=-4.5+i*.55;z=1.13+math.sin(i*.45)*.16
    rock('Path',(x,.002,z),(.49,.018,.32),'Path',80+i)
for side in [-1,1]:
    for i in range(12):
        z=2+i*.75;x=side*(4.83+.07*math.sin(i*2))
        if i%3==0:rock('BorderRocks',(x,0,z),(.36,.22,.43),'Stone',100+i+(side+1)*10)
        if i%2==0:fern('RimGrowth',(x-side*.08,.006,z+.24),.75,seed=200+i)
for i in range(20):
    x=-4.2+i*.42;z=10.9+.035*math.sin(i*.8);width=.50
    nx=-4.2+(i+1)*.42;nz=10.9+.035*math.sin((i+1)*.8);nw=.50
    mesh('CreekSurface',[(x,.008,z-width),(nx,.008,nz-nw),(nx,.008,nz+nw),(x,.008,z+width)],[(0,1,2,3)],'CreekWater')
    if i%3==0:
        rock('BorderRocks',(x,0,z+width+.15),(.33,.19,.25),'Stone',300+i)
        rock('BorderRocks',(x+.19,0,z-width-.12),(.27,.13,.21),'Stone',350+i)
    if i%4==0:
        for j in range(2):
            beam('CreekFoam',(x,.014,z-.1+j*.13),(x+.21,.014,z-.075+j*.13),.007,'Foam',4)
for x,z in [(-3.4,11.65),(-2.5,11.9),(2.5,11.9),(3.4,11.65)]:fern('RimGrowth',(x,.01,z),1.1,seed=int(x*10)%50)

# Cedar: sculpted, drooping layered boughs; separate trunk/canopy tint contracts.
asset('EDV2_Cedar')
beam('Trunk',(0,0,0),(.015,1.61,.01),.082,'Bark',10,.022)
for i in range(7):
    a=i*math.tau/7;beam('Trunk',(0,.17,0),(math.cos(a)*.16,.018,math.sin(a)*.16),.038,'Bark',5,.012)
for tier in range(7):
    bottom=.35+tier*.205;r=.43*(1-tier/8.0);n=24;verts=[]
    for ring in range(3):
        for i in range(n):
            a=i*math.tau/n+tier*.28;wave=(i%3==0)
            if ring==0:rad=r*(1 if wave else .83);y=bottom-(.04 if wave else 0)
            elif ring==1:rad=r*.67;y=bottom+.105
            else:rad=r*.10;y=bottom+.30
            verts.append((math.cos(a)*rad,y,math.sin(a)*rad))
    faces=[]
    for ring in range(2):
        for i in range(n):faces.append((ring*n+i,ring*n+(i+1)%n,(ring+1)*n+(i+1)%n,(ring+1)*n+i))
    faces.extend([tuple(reversed(range(n))),tuple(range(n*2,n*3))])
    mesh('Canopy',verts,faces,'Cedar' if tier%2==0 else 'CedarLight')
    for i in range(8):
        a=i*math.tau/8+tier*.28
        leaf('Canopy',(math.cos(a)*r*.25,bottom+.15,math.sin(a)*r*.25),(math.cos(a)*r,bottom-.038,math.sin(a)*r),r*.11,'CedarLight',.017)

# Broadleaf: forked timber and chunky leaf fans read at miniature distance.
asset('EDV2_Broadleaf')
beam('Trunk',(0,0,0),(.025,1.20,0),.086,'Bark',10,.048)
for i in range(5):
    a=i*math.tau/5;beam('Trunk',(0,.15,0),(math.cos(a)*.16,.008,math.sin(a)*.16),.035,'Bark',5,.013)
for i in range(9):
    a=i*2.399;h=.84+i*.058;x=math.cos(a)*(.20+i%2*.06);z=math.sin(a)*(.20+i%2*.06)
    beam('Trunk',(0,h-.25,0),(x,h+.20,z),.038,'Bark',7,.014)
    rock('Canopy',(x-.01,h+.09,z),(.46,.36,.44),'Leaf' if i%2==0 else 'LeafLight',500+i)
    for j in range(7):
        aa=a+j*math.tau/7;start=Vector((x,h+.35,z));end=start+Vector((math.cos(aa)*.21,-.08,math.sin(aa)*.21))
        leaf('Canopy',start,end,.075,'LeafLight' if j%3==0 else 'Leaf',.018)
rock('Canopy',(0,1.42,0),(.42,.35,.40),'LeafLight',550)

# Cabin: individually layered planks, warm framed glazing and curved clay tile strips.
asset('EDV2_Cabin')
box('Foundation',(0,.065,0),(1.03,.13,.87),'Slate')
for side in [-1,1]:
    for i in range(5):rock('Foundation',(side*.47,.02,-.34+i*.17),(.15,.16,.15),'Stone',600+i)
for i in range(9):
    x=-.40+i*.10
    box('Walls',(x,.46,.35),(.094,.68,.062),'Wood' if i%3 else 'WoodLight')
    if abs(x+.16)>.13:box('Walls',(x,.46,-.35),(.094,.68,.062),'Wood' if i%3 else 'WoodLight')
for side in [-1,1]:
    for i in range(7):box('Walls',(side*.435,.46,-.30+i*.10),(.06,.68,.093),'Wood' if i%2 else 'WoodLight')
    box('Timber',(side*.45,.47,-.385),(.07,.75,.07),'BarkLight')
    box('Timber',(side*.45,.47,.385),(.07,.75,.07),'BarkLight')
    box('Timber',(side*.45,.18,0),(.075,.06,.84),'WoodLight')
    box('Timber',(side*.45,.78,0),(.075,.07,.84),'WoodLight')
mesh('Walls',[(-.44,.80,-.35),(.44,.80,-.35),(0,1.08,-.35),(-.44,.80,.35),(.44,.80,.35),(0,1.08,.35)],[(0,1,2),(5,4,3)],'Wood')
box('Door',(-.16,.37,-.389),(.235,.48,.025),'Teal')
for x in [-.30,-.02]:box('Timber',(x,.39,-.405),(.035,.53,.04),'WoodLight')
box('Timber',(-.16,.655,-.405),(.315,.04,.04),'WoodLight')
beam('Hardware',(-.075,.38,-.423),(-.075,.38,-.44),.014,'Brass',8)
for front in [True,False]:
    x,z=(.235,-.395) if front else (-.45,.08)
    if front:
        box('Window',(x,.52,z),(.23,.27,.015),'WindowGlow')
        for dx in [-.13,0,.13]:box('Timber',(x+dx,.52,z-.02),(.023,.31,.04),'WoodLight')
        for dy in [-.15,0,.15]:box('Timber',(x,.52+dy,z-.02),(.28,.022,.04),'WoodLight')
    else:
        box('Window',(x,.52,z),(.02,.27,.23),'WindowGlow')
        for dz in [-.13,0,.13]:box('Timber',(x-.02,.52,z+dz),(.04,.31,.023),'WoodLight')
        for dy in [-.15,0,.15]:box('Timber',(x-.02,.52+dy,z),(.04,.022,.28),'WoodLight')
# Each tile has a convex transverse profile and visible overlaps in the roof slope.
for side in [-1,1]:
    for row in range(4):
        for col in range(8):
            x0=side*(row*.133);x1=side*((row+1)*.133+.020);z0=-.49+col*.135;n=5;verts=[]
            for xx in [x0,x1]:
                for j in range(n):
                    zz=z0+j*.135/(n-1);yy=1.11-abs(xx)*.57+math.sin(j*math.pi/(n-1))*.018
                    verts.append((xx,yy,zz))
            faces=[(j,j+1,n+j+1,n+j) if side==1 else (n+j,n+j+1,j+1,j) for j in range(n-1)]
            mesh('Roof',verts,faces,'TerracottaLight' if (row+col)%3==0 else 'Terracotta')
    beam('Timber',(side*.55,.79,-.52),(0,1.12,-.52),.034,'WoodLight',5)
    beam('Timber',(side*.55,.79,.60),(0,1.12,.60),.034,'WoodLight',5)
for i in range(8):beam('Roof',(0,1.125,-.50+i*.14),(0,1.125,-.36+i*.14),.040,'TerracottaLight',8)
for row in range(4):
    for col in range(2):rock('Chimney',(.20+col*.085, .89+row*.085,.19),(.10,.09,.18),'Stone',650+row*2+col)
box('Chimney',(.243,1.245,.19),(.24,.045,.24),'Slate')
box('Timber',(-.15,.08,-.49),(.43,.07,.22),'WoodLight')
box('Timber',(-.15,.03,-.62),(.48,.06,.16),'Wood')
for x in [-.41,.39]:
    beam('Timber',(x,.13,-.52),(x,.46,-.52),.025,'WoodLight',6)
beam('Timber',(.03,.39,-.52),(.40,.39,-.52),.018,'WoodLight',6)

asset('EDV2_Rock');rock('Stone',(0,0,0),(.62,.39,.48),'Slate',809)
rock('Moss',(-.04,.31,.01),(.31,.032,.25),'Moss',815)
asset('EDV2_Fern');fern('Fern',(0,0,0),1,seed=91)
asset('EDV2_Log')
beam('Bark',(-.39,.115,0),(.39,.115,0),.115,'Bark',12)
for side in [-1,1]:
    beam('Endgrain',(side*.39,.115,0),(side*.398,.115,0),.105,'WoodLight',12)
    for rr in [.03,.064,.092]:
        for i in range(12):
            a=i*math.tau/12;b=(i+1)*math.tau/12
            beam('Endgrain',(side*.403,.115+math.cos(a)*rr,math.sin(a)*rr),(side*.403,.115+math.cos(b)*rr,math.sin(b)*rr),.003,'BarkLight',4)
for i in range(7):
    a=i*math.pi/7
    beam('Bark',(-.35,.115+math.sin(a)*.116,math.cos(a)*.116),(.35,.115+math.sin(a)*.116,math.cos(a)*.116),.008,'BarkLight',4)
beam('Bark',(.08,.19,0),(.13,.29,.06),.030,'Bark',7,.015)

# Real-metre cosmetic fixtures have no interaction/collision logic baked in.
asset('EDV2_Hydrant')
beam('Body',(0,.018,0),(0,.185,0),.045,'EnamelRed',16)
beam('Body',(0,.008,0),(0,.028,0),.061,'EnamelRed',16)
beam('Body',(0,.178,0),(0,.192,0),.057,'EnamelRed',16)
beam('Body',(0,.192,0),(0,.225,0),.046,'EnamelRed',12,.027)
beam('Hardware',(0,.224,0),(0,.237,0),.013,'Brass',6)
for i in range(8):
    a=i*math.tau/8;beam('Hardware',(math.cos(a)*.051,.027,math.sin(a)*.051),(math.cos(a)*.051,.035,math.sin(a)*.051),.005,'Brass',6)
beam('Coupling',(0,.118,-.018),(0,.118,-.078),.029,'Brass',12)
beam('Coupling',(0,.118,-.071),(0,.118,-.091),.035,'Cyan',12)
beam('Coupling',(0,.118,-.092),(0,.118,-.094),.024,'Rubber',12)
for i in range(6):
    a=i*math.tau/6;box('Coupling',(math.cos(a)*.033,.118+math.sin(a)*.033,-.081),(.009,.009,.024),'Cyan')
beam('Hardware',(.035,.16,0),(.071,.16,0),.010,'Brass',8)
for i in range(12):
    a=i*math.tau/12;b=(i+1)*math.tau/12
    beam('Valve',(.074,.16+math.cos(a)*.024,math.sin(a)*.024),(.074,.16+math.cos(b)*.024,math.sin(b)*.024),.004,'Brass',6)
for i in range(3):
    a=i*math.tau/3;beam('Valve',(.074,.16,0),(.074,.16+math.cos(a)*.024,math.sin(a)*.024),.003,'Brass',5)

asset('EDV2_CreekPump')
rock('Base',(0,0,0),(.18,.045,.15),'Stone',920)
box('Body',(0,.065,0),(.067,.06,.055),'Teal')
beam('Body',(0,.055,0),(0,.15,0),.032,'Teal',12,.023)
beam('Hardware',(-.04,.13,0),(.04,.13,0),.012,'Brass',8)
beam('Handle',(-.027,.14,0),(-.027,.19,.015),.010,'Teal',8)
beam('Handle',(-.027,.19,.015),(-.027,.215,-.07),.009,'Teal',8)
beam('Handle',(-.055,.215,-.07),(.015,.215,-.07),.012,'Cyan',10)
beam('Pipe',(0,.09,-.02),(0,.09,-.07),.019,'Brass',10)
beam('Pipe',(0,.09,-.07),(0,.058,-.092),.019,'Brass',10)
beam('Strainer',(0,.008,-.092),(0,.014,-.092),.032,'Rubber',12)
beam('Strainer',(0,.044,-.092),(0,.052,-.092),.032,'Rubber',12)
for i in range(12):
    a=i*math.tau/12;x=math.cos(a)*.030;z=-.092+math.sin(a)*.030
    beam('Strainer',(x,.012,z),(x,.048,z),.0028,'Brass',4)

# Build single named mesh per semantic group, retaining clear tint/animation boundaries.
stats=[];allobjects=[]
for name,buffers in groups.items():
    if name!='EDV2_Terrain':
        floor_y=min(v[1] for buf in buffers.values() for v in buf.v)
        for buf in buffers.values():buf.v=[(x,y-floor_y,z) for x,y,z in buf.v]
    col=bpy.data.collections.new(name);scene.collection.children.link(col)
    parent=bpy.data.objects.new(name,None);col.objects.link(parent)
    objects=[]
    for semantic,buf in buffers.items():
        obj=buf.build(col);obj.parent=parent;objects.append(obj)
    assets[name]=objects;allobjects.extend(objects)
    bpy.ops.object.select_all(action='DESELECT')
    parent.select_set(True)
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    path=EXPORT/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},global_scale=1,
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
    verts=[v for buf in buffers.values() for v in buf.v]
    bounds={'min':[round(min(v[i] for v in verts),5) for i in range(3)],'max':[round(max(v[i] for v in verts),5) for i in range(3)]}
    triangles=sum(sum(len(f)-2 for f in buf.f) for buf in buffers.values())
    stats.append({'asset':name,'file':str(path.relative_to(ROOT)).replace('\\','/'),'pivot_unity':[0,0,0],
        'scale':[1,1,1],'bounds_unity_metres':bounds,'triangles':triangles,
        'meshes':[{'name':key,'triangles':sum(len(f)-2 for f in buf.f),'material_slots':buf.materials} for key,buf in buffers.items()]})
    col.hide_render=True;col.hide_viewport=True
total=sum(s['triangles'] for s in stats)
assert total<=40000, f'Kit triangle budget exceeded: {total}'

# Preview is an authored assembly only; no extra scenery is baked into exported terrain.
preview=bpy.data.collections.new('00 Preview assembly - not exported');scene.collection.children.link(preview)
def instance(assetname,position=(0,0,0),scale=1,yaw=0):
    root=bpy.data.objects.new(assetname+' preview',None);preview.objects.link(root);root.location=B(position);root.scale=(scale,scale,scale);root.rotation_euler.z=yaw
    for original in assets[assetname]:
        ob=bpy.data.objects.new(original.name+' preview',original.data);preview.objects.link(ob);ob.parent=root
    return root
instance('EDV2_Terrain')
for i,(x,z) in enumerate([(-3.8,3.2),(-2.0,3.5),(0,3.4),(2.1,3.6),(3.8,3.3),(-3.3,5.5),(-1,5.6),(1.4,5.8),(3.5,6),(-3.9,8),(-1.8,8.4),(.5,8.2),(2.7,8.5)]):
    instance('EDV2_Cedar' if i%3 else 'EDV2_Broadleaf',(x,0,z),1+(i%4)*.12,i*.7)
instance('EDV2_Cabin',(-4.25,0,4.25),1.25)
for i,(x,z) in enumerate([(-4.5,2.1),(4.4,2.0),(4.1,7.3),(-3.3,9.8),(2,11.9),(-1.7,11.9)]):
    instance('EDV2_Rock',(x,0,z),1,i*.8);instance('EDV2_Fern',(x+.28,0,z+.3),1.3)
instance('EDV2_Log',(-3.7,.02,2.3),1.2,.25)
instance('EDV2_Hydrant',(-1.1,0,1.2),5)
instance('EDV2_CreekPump',(1.3,0,1.25),5)
studio=bpy.data.collections.new('99 Studio - not exported');scene.collection.children.link(studio)
studio_mat=bpy.data.materials.new('Studio');studio_mat.diffuse_color=(.025,.07,.075,1);studio_mat.use_nodes=True
studio_mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.025,.07,.075,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.68));floor=bpy.context.object
for col in list(floor.users_collection):col.objects.unlink(floor)
studio.objects.link(floor);floor.name='Studio ground';floor.data.materials.append(studio_mat)
def light(name,position,energy,color,size,target=(0,.4,6)):
    data=bpy.data.lights.new(name,'AREA');data.energy=energy;data.color=color;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);studio.objects.link(ob);ob.location=B(position)
    ob.rotation_euler=(Vector(B(target))-ob.location).to_track_quat('-Z','Y').to_euler()
light('Warm afternoon',(-5,12,-3),2000,(1,.82,.58),7)
light('Cool creek bounce',(6,9,10),2200,(.52,.80,1),8)
light('Soft frontal fill',(2,6,-7),1000,(.81,.93,1),10)
cam_data=bpy.data.cameras.new('Art review camera');cam=bpy.data.objects.new('Art review camera',cam_data);studio.objects.link(cam);scene.camera=cam
cam_data.type='ORTHO';cam_data.ortho_scale=18.5;cam.location=B((12,14,-12));cam.rotation_euler=(Vector(B((0,.35,5.5)))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(SOURCE/'Emberdeep-ArtV2-Preview.png')
manifest={'kit':'Emberdeep V2 modular environment','revision':1,'generator':'Tools/BuildEmberdeepArtV2.py','seed':29031,
    'source':'ArtSource/EmberdeepV2/Emberdeep-ArtV2.blend','concept':'ArtSource/EmberdeepV2/Concepts/Emberdeep-ArtV2-Concept.png',
    'provenance':'Original procedural meshes authored for this project from its generated concept; no downloaded mesh or texture assets.',
    'coordinates':'Authored Unity X/Y/Z in metres; Blender stores X/-Z/Y, FBX exported -Z forward and Y up. Unity-imported bounds must match bounds_unity_metres.',
    'terrain_contract':'Front z<1.6 has only flat ground/path; dynamic tree lanes z3..10 are free of baked trees. CreekSurface is independently named at z approximately 10.9.',
    'performance':'Geometry budget only; not a measured headset performance result. No textures, alpha foliage or skinned meshes.',
    'total_unique_triangles':total,'assets':stats,
    'material_color_encoding':'Palette values are authored linear RGB in Blender diffuse_color and Principled Base Color. FBX importer color-space conversion may change displayed inspector values.',
    'materials':[{'name':n,'base_color_linear':list(c)+[1],'roughness':mats[n].node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value,'metallic':mats[n].node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value} for n,c in palette.items()]}
(SOURCE/'ArtV2-Manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Emberdeep-ArtV2.blend'))
bpy.ops.render.render(write_still=True)
print('EMBERDEEP_ART_V2_COMPLETE '+json.dumps({'triangles':total,'assets':len(stats),'source':str(SOURCE),'export':str(EXPORT)}))
