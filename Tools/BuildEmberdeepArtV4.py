"""Emberdeep Art V4 kit: rounded hand-crafted miniature forest, generated with Blender 4.3.

Run: blender --background --python Tools/BuildEmberdeepArtV4.py [-- --preview]

Replaces the faceted V2/V3 environment meshes with smooth, sculpted forms and bakes
ambient occlusion plus a height gradient into vertex colours, read by the Unity
surface shader. Asset ids, semantic mesh names and gameplay contracts are unchanged:
flat play area in front of z 1.6, creek centred near z 2.1, tree lanes z 3..10 free.
All dimensions are Unity-style (X right, Y up, Z depth). Forest props are in forest
units (the diorama is scaled by 0.08 at runtime); station props are in real metres.
"""
import bpy, math, random, json, sys, re
from pathlib import Path
from mathutils import Vector, Matrix, Euler, noise
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/EmberdeepV3'
EXPORT = ROOT / 'Assets/FireRescue/ArtV2/Models'
for p in [SOURCE, EXPORT]: p.mkdir(parents=True, exist_ok=True)
random.seed(40117)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for c in list(bpy.data.collections): bpy.data.collections.remove(c)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'; scene.unit_settings.scale_length = 1

# Unity X/Y/Z -> Blender. The FBX round trip mirrors X, so X is negated here (and face
# winding reversed at build time) to make authored left/right match the game exactly.
def B(p): return (-p[0], -p[2], p[1])

palette = {
 'Moss':(.25,.47,.12), 'MossLight':(.45,.63,.17), 'Soil':(.36,.20,.095),
 'Slate':(.25,.29,.30), 'Stone':(.46,.47,.42), 'Path':(.60,.50,.36),
 'Bark':(.30,.15,.06), 'BarkLight':(.44,.25,.10),
 'Cedar':(.10,.30,.14), 'CedarLight':(.24,.46,.15),
 'Leaf':(.27,.50,.12), 'LeafLight':(.50,.66,.18),
 'Fern':(.30,.55,.11), 'Wood':(.54,.28,.095), 'WoodLight':(.74,.47,.20),
 'Terracotta':(.60,.20,.075), 'TerracottaLight':(.80,.33,.12),
 'Brass':(.72,.50,.16), 'EnamelRed':(.70,.065,.028),
 'Teal':(.03,.33,.31), 'Cyan':(.06,.66,.70), 'Rubber':(.025,.055,.06),
 'WindowGlow':(1,.68,.18), 'CreekWater':(.035,.40,.45), 'Foam':(.60,.86,.78),
 'Cream':(.93,.86,.70), 'Petal':(.98,.90,.62), 'PetalPink':(.96,.56,.52), 'Mushroom':(.86,.30,.12),
 # Characters, gloves, interface and camp (Art V5 additions).
 'GloveGreen':(.20,.33,.11), 'Leather':(.33,.17,.075), 'Wool':(.97,.90,.74), 'WoolLight':(1,.97,.88),
 'Cocoa':(.36,.22,.15), 'CocoaLight':(.52,.36,.26), 'Hoof':(.07,.055,.05), 'EyeWhite':(1,.99,.96),
 'Gem':(.08,.74,.80), 'Glass':(.56,.86,.86), 'Canvas':(.95,.82,.56), 'CanvasDark':(.74,.54,.29),
 'Ember':(1,.42,.06), 'Flame':(1,.62,.12),
}
mats = {}
for name, color in palette.items():
    m = bpy.data.materials.new(name); m.diffuse_color = (*color, 1); m.use_nodes = True
    bs = m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Roughness'].default_value = .62
    if name in ['Brass', 'Teal', 'EnamelRed', 'Cyan', 'Glass', 'Gem', 'Hoof', 'EyeWhite']:
        bs.inputs['Metallic'].default_value = .38 if name == 'Brass' else .13; bs.inputs['Roughness'].default_value = .32
    if name in ['WindowGlow', 'Ember', 'Flame']:
        bs.inputs['Emission Color'].default_value = (*color, 1); bs.inputs['Emission Strength'].default_value = .8
    mats[name] = m

# ---------------------------------------------------------------- geometry buffers
class Buffer:
    def __init__(self, name): self.name = name; self.v = []; self.f = []; self.mi = []; self.materials = []
    def add(self, verts, faces, mat):
        if mat not in self.materials: self.materials.append(mat)
        idx = self.materials.index(mat); off = len(self.v); self.v.extend([tuple(p) for p in verts])
        self.f.extend([tuple(off + i for i in f) for f in faces]); self.mi.extend([idx] * len(faces))
assets = {}; groups = {}; current = None; options = {}
def asset(name, **opts):
    global current
    current = name; groups[name] = {}; options[name] = opts
def g(name):
    if name not in groups[current]: groups[current][name] = Buffer(name)
    return groups[current][name]
# Nested placement: every primitive below is emitted through mesh(), so a whole
# sub-assembly can be moved, turned or scaled with `with xf(...)`.
XF = [Matrix.Identity(4)]
class xf:
    def __init__(self, pos=(0, 0, 0), rot=(0, 0, 0), scale=1):
        s = scale if isinstance(scale, (tuple, list)) else (scale,) * 3
        self.m = Matrix.Translation(Vector(pos)) @ Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4() @ Matrix.Diagonal((s[0], s[1], s[2], 1))
    def __enter__(self): XF.append(XF[-1] @ self.m)
    def __exit__(self, *a): XF.pop()
def mesh(group, verts, faces, mat):
    if len(XF) > 1:
        m = XF[-1]; verts = [m @ Vector(p) for p in verts]
        if m.determinant() < 0: faces = [tuple(reversed(f)) for f in faces]
    g(group).add(verts, faces, mat)

# ---------------------------------------------------------------- rounded primitives
def _icosphere(level):
    t = (1 + 5 ** .5) / 2
    v = [Vector(p).normalized() for p in [(-1,t,0),(1,t,0),(-1,-t,0),(1,-t,0),(0,-1,t),(0,1,t),(0,-1,-t),(0,1,-t),(t,0,-1),(t,0,1),(-t,0,-1),(-t,0,1)]]
    f = [(0,11,5),(0,5,1),(0,1,7),(0,7,10),(0,10,11),(1,5,9),(5,11,4),(11,10,2),(10,7,6),(7,1,8),(3,9,4),(3,4,2),(3,2,6),(3,6,8),(3,8,9),(4,9,5),(2,4,11),(6,2,10),(8,6,7),(9,8,1)]
    for _ in range(level):
        cache = {}; nf = []
        def mid(a, b):
            key = (min(a, b), max(a, b))
            if key not in cache: v.append(((v[a] + v[b]) / 2).normalized()); cache[key] = len(v) - 1
            return cache[key]
        for a, b, c in f:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a); nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        f = nf
    return v, f
SPHERES = {level: _icosphere(level) for level in range(3)}

def blob(group, center, size, mat, seed=0, level=1, lump=.22, flat=None, freq=1.7):
    """Lumpy rounded mass. size is the full extent; flat clamps the underside (0..1 of height)."""
    v, f = SPHERES[level]; c = Vector(center); s = Vector(size) * .5; off = Vector((seed * 1.37, seed * .73, seed * 2.11))
    out = []
    for p in v:
        n = noise.noise(p * freq + off)
        q = p * (1 + lump * n)
        if flat is not None and q.y < -flat: q.y = -flat - (q.y + flat) * .12
        out.append((c.x + q.x * s.x, c.y + q.y * s.y, c.z + q.z * s.z))
    mesh(group, out, f, mat)

def tube(group, points, radii, mat, sides=8, cap=True):
    """Smooth swept tube through points; radii is one value or one per point."""
    pts = [Vector(p) for p in points]
    if not isinstance(radii, (list, tuple)): radii = [radii] * len(pts)
    verts = []; faces = []; ref = Vector((0, 1, 0))
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        side = d.cross(ref)
        if side.length < .05: side = d.cross(Vector((1, 0, 0)))
        side.normalize(); up = side.cross(d).normalized(); ref = up
        rx, ry = radii[i] if isinstance(radii[i], (list, tuple)) else (radii[i], radii[i])
        for k in range(sides):
            a = k * math.tau / sides; verts.append(p + rx * math.cos(a) * side + ry * math.sin(a) * up)
    for i in range(len(pts) - 1):
        for k in range(sides):
            a = i * sides + k; b = i * sides + (k + 1) % sides; faces.append((a, a + sides, b + sides, b))
    if cap:
        r0 = max(radii[0]) if isinstance(radii[0], (list, tuple)) else radii[0]; r1 = max(radii[-1]) if isinstance(radii[-1], (list, tuple)) else radii[-1]
        verts.append(pts[0] - (pts[1] - pts[0]).normalized() * r0 * .5); c0 = len(verts) - 1
        verts.append(pts[-1] + (pts[-1] - pts[-2]).normalized() * r1 * .5); c1 = len(verts) - 1
        last = (len(pts) - 1) * sides
        for k in range(sides):
            faces.append((c0, k, (k + 1) % sides)); faces.append((c1, last + (k + 1) % sides, last + k))
    mesh(group, verts, faces, mat)

def lathe(group, center, profile, mat, sides=16, wobble=0, seed=0):
    """Revolve (radius, height) pairs around Y. A zero radius closes the form with a pole."""
    c = Vector(center); verts = []; faces = []; rings = []
    for j, (r, y) in enumerate(profile):
        if r <= 1e-6: verts.append(c + Vector((0, y, 0))); rings.append([len(verts) - 1])
        else:
            ring = []
            for k in range(sides):
                a = k * math.tau / sides; rr = r * (1 + wobble * noise.noise(Vector((math.cos(a) * 1.3 + seed, y * 9, math.sin(a) * 1.3))))
                verts.append(c + Vector((math.cos(a) * rr, y, math.sin(a) * rr))); ring.append(len(verts) - 1)
            rings.append(ring)
    for j in range(len(rings) - 1):
        a, b = rings[j], rings[j + 1]
        for k in range(sides):
            k2 = (k + 1) % sides
            if len(a) == 1 and len(b) > 1: faces.append((a[0], b[k], b[k2]))
            elif len(b) == 1 and len(a) > 1: faces.append((a[k], b[0], a[k2]))
            elif len(a) > 1: faces.append((a[k], b[k], b[k2], a[k2]))
    mesh(group, verts, faces, mat)

def rbox(group, center, size, mat, bevel=.2, yaw=0, tilt=0):
    """Box with softened edges. bevel is a fraction of the smallest side."""
    c = Vector(center); h = Vector(size) * .5; b = min(h) * 2 * bevel
    if bevel <= 0:
        pts = [(-h.x,-h.y,-h.z),(h.x,-h.y,-h.z),(h.x,-h.y,h.z),(-h.x,-h.y,h.z),(-h.x,h.y,-h.z),(h.x,h.y,-h.z),(h.x,h.y,h.z),(-h.x,h.y,h.z)]
        faces = [(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)]
    else:
        pts = []; faces = []; index = {}
        def key(i, j, k): return index.setdefault((i, j, k), len(index))
        grid = lambda half: [-half, -half + b, half - b, half]
        gx, gy, gz = grid(h.x), grid(h.y), grid(h.z)
        def point(i, j, k):
            p = Vector((gx[i], gy[j], gz[k])); inner = Vector((max(-h.x + b, min(h.x - b, p.x)), max(-h.y + b, min(h.y - b, p.y)), max(-h.z + b, min(h.z - b, p.z))))
            d = p - inner
            return inner + (d.normalized() * b if d.length > 1e-9 else d)
        store = {}
        def vid(i, j, k):
            if (i, j, k) not in store: store[(i, j, k)] = len(pts); pts.append(point(i, j, k))
            return store[(i, j, k)]
        for a in range(3):
            for bb in range(3):
                faces += [(vid(a,bb,0),vid(a,bb+1,0),vid(a+1,bb+1,0),vid(a+1,bb,0)),(vid(a,bb,3),vid(a+1,bb,3),vid(a+1,bb+1,3),vid(a,bb+1,3)),
                          (vid(a,0,bb),vid(a+1,0,bb),vid(a+1,0,bb+1),vid(a,0,bb+1)),(vid(a,3,bb),vid(a,3,bb+1),vid(a+1,3,bb+1),vid(a+1,3,bb)),
                          (vid(0,a,bb),vid(0,a,bb+1),vid(0,a+1,bb+1),vid(0,a+1,bb)),(vid(3,a,bb),vid(3,a+1,bb),vid(3,a+1,bb+1),vid(3,a,bb+1))]
    cy, sy = math.cos(yaw), math.sin(yaw); ct, st = math.cos(tilt), math.sin(tilt); out = []
    for p in pts:
        p = Vector(p); y = p.y * ct - p.z * st; z = p.y * st + p.z * ct
        out.append((c.x + p.x * cy + z * sy, c.y + y, c.z - p.x * sy + z * cy))
    mesh(group, out, faces, mat)

def blade(group, base, tip, width, mat, bend=.3, segments=3):
    """Double-sided tapered leaf that arcs from base to tip."""
    a, b = Vector(base), Vector(tip); d = b - a; side = d.cross(Vector((0, 1, 0)))
    if side.length < 1e-6: side = Vector((1, 0, 0))
    side.normalize(); verts = []; faces = []
    for i in range(segments + 1):
        t = i / segments; p = a + d * t + Vector((0, math.sin(t * math.pi) * d.length * bend, 0)); w = width * math.sin((.12 + .88 * t) * math.pi) * (1 - t * .35)
        verts += [p - side * w, p + side * w]
    for i in range(segments): faces.append((i * 2, i * 2 + 1, i * 2 + 3, i * 2 + 2))
    mesh(group, verts, faces, mat)
    mesh(group, verts, [tuple(reversed(q)) for q in faces], mat)

def grass(group, pos, scale=1, seed=0, mat='MossLight', count=6):
    rng = random.Random(seed); p = Vector(pos)
    for i in range(count):
        a = i * math.tau / count + rng.random() * .6; reach = scale * (.05 + rng.random() * .07)
        blade(group, p, p + Vector((math.cos(a) * reach, scale * (.14 + rng.random() * .12), math.sin(a) * reach)), max(.016, scale * .024), mat if i % 3 else 'Moss', .12)

def flower(group, pos, scale=1, seed=0, mat='Petal'):
    rng = random.Random(seed); p = Vector(pos); h = scale * (.16 + rng.random() * .07); lean = Vector((rng.uniform(-.03, .03), 0, rng.uniform(-.03, .03))) * scale
    top = p + Vector((0, h, 0)) + lean
    tube(group, [p, p + Vector((0, h * .5, 0)) + lean * .3, top], scale * .008, 'Fern', 4, cap=False)
    for k in range(5):
        a = k * math.tau / 5 + seed; out = Vector((math.cos(a), .25, math.sin(a))) * scale * .055
        blade(group, top, top + out, scale * .026, mat, .1, 2)
    blob(group, top + Vector((0, scale * .012, 0)), (scale * .04,) * 3, 'WindowGlow', seed, 0, 0)
    blade(group, p, p + Vector((scale * .07, scale * .06, scale * .02)), scale * .02, 'Fern', .2, 2)

def mushroom(group, pos, scale=1, seed=0):
    p = Vector(pos)
    lathe(group, p, [(.022 * scale, 0), (.016 * scale, .05 * scale), (.014 * scale, .10 * scale)], 'Cream', 7)
    lathe(group, p + Vector((0, .095 * scale, 0)), [(.066 * scale, 0), (.060 * scale, .022 * scale), (.036 * scale, .05 * scale), (0, .062 * scale)], 'Mushroom', 9)
    for k in range(3):
        a = k * 2.1 + seed; blob(group, p + Vector((math.cos(a) * .034 * scale, .13 * scale, math.sin(a) * .034 * scale)), (.02 * scale,) * 3, 'Cream', k, 0, 0)

def fern(group, pos, scale=1, seed=0, mat='Fern'):
    rng = random.Random(seed); p = Vector(pos)
    for i in range(8):
        a = i * math.tau / 8 + rng.random() * .3; d = Vector((math.cos(a), 0, math.sin(a))); length = scale * (.20 + rng.random() * .09)
        blade(group, p, p + d * length + Vector((0, scale * .05, 0)), scale * .045, mat if i % 2 else 'LeafLight', .55, 4)
    # Two young fiddleheads curl up from the centre.
    for i in range(2):
        a = i * 2.6 + seed; d = Vector((math.cos(a), 0, math.sin(a))); pts = []
        for k in range(7):
            t = k / 6; ang = t * 4.4
            pts.append(p + Vector((0, scale * .16 * min(1, t * 1.6), 0)) + d * scale * (.02 + .03 * math.sin(ang) * (1 - t * .6)) + Vector((0, scale * .03 * (1 - math.cos(ang)) * (1 - t * .5), 0)))
        tube(group, pts, [scale * .008 * (1 - k / 9) for k in range(7)], 'LeafLight', 4)

def stone(group, pos, size, seed=0, mat='Stone', moss=True):
    blob(group, (pos[0], pos[1] + size[1] * .42, pos[2]), size, mat, seed, 1, .30, .45)
    if moss: blob(group if current == 'EDV2_Terrain' else 'Moss', (pos[0], pos[1] + size[1] * .80, pos[2]), (size[0] * .72, size[1] * .34, size[2] * .70), 'Moss', seed + 5, 1, .25, .2)

# ---------------------------------------------------------------- forest assets
asset('EDV2_Cedar', ao=.5)
# A leaning, flared trunk with visible roots.
tube('Trunk', [(0, 0, 0), (.012, .35, .006), (.03, .9, .01), (.02, 1.45, 0), (.0, 1.78, 0)], [.115, .085, .06, .035, .012], 'Bark', 9)
for i in range(6):
    a = i * math.tau / 6 + .4; d = Vector((math.cos(a), 0, math.sin(a)))
    tube('Trunk', [d * .05 + Vector((0, .2, 0)), d * .15 + Vector((0, .07, 0)), d * .25 + Vector((0, .0, 0))], [.045, .035, .014], 'Bark', 5)
# Five drooping, scalloped bough skirts; alternate tones give each tier its own shadow line.
tiers = 5
for tier in range(tiers):
    t = tier / (tiers - 1); bottom = .42 + tier * .27; R = .50 * (1 - t * .68); lobes = 7 - tier % 2; sides = lobes * 4; phase = tier * .9
    verts = []; faces = []
    for ring, (rs, ys) in enumerate([(1.0, 0), (.80, .10), (.46, .23), (.16, .36)]):
        for k in range(sides):
            a = k * math.tau / sides + phase; scallop = .5 + .5 * math.cos(lobes * (a - phase))
            r = R * rs * (1 + (.20 * scallop if ring < 2 else 0)); y = bottom + ys - (.085 * scallop if ring == 0 else 0)
            verts.append((math.cos(a) * r + .02 * t, y, math.sin(a) * r))
    for ring in range(3):
        for k in range(sides):
            a = ring * sides + k; b = ring * sides + (k + 1) % sides; faces.append((a, a + sides, b + sides, b))
    verts.append((.02 * t, bottom + .43, 0)); top = len(verts) - 1
    for k in range(sides): faces.append((3 * sides + k, top, 3 * sides + (k + 1) % sides))
    verts.append((.02 * t, bottom + .05, 0)); under = len(verts) - 1
    for k in range(sides): faces.append((k, (k + 1) % sides, under))
    mesh('Canopy', verts, faces, 'CedarLight' if tier % 2 else 'Cedar')
lathe('Canopy', (.02, 1.80, 0), [(.115, 0), (.065, .15), (0, .32)], 'CedarLight', 8)

asset('EDV2_Broadleaf', ao=.5)
tube('Trunk', [(0, 0, 0), (.02, .3, .0), (.07, .62, .02), (.05, .95, .04), (.0, 1.2, .03)], [.13, .10, .085, .065, .04], 'Bark', 9)
for i in range(6):
    a = i * math.tau / 6; d = Vector((math.cos(a), 0, math.sin(a)))
    tube('Trunk', [d * .06 + Vector((0, .2, 0)), d * .18 + Vector((0, .07, 0)), d * .29 + Vector((0, 0, 0))], [.05, .04, .015], 'Bark', 5)
crown = [((-.30, 1.18, .04), .58), ((.28, 1.22, -.06), .56), ((.02, 1.34, .30), .52), ((-.02, 1.30, -.30), .50), ((0, 1.62, 0), .62), ((-.20, 1.52, -.14), .44), ((.22, 1.54, .16), .44)]
for i, (c, s) in enumerate(crown):
    tube('Trunk', [(.05, .78 + i * .03, .03), ((c[0] * .55 + .03), c[1] - .26, c[2] * .55), (c[0] * .9, c[1] - .08, c[2] * .9)], [.045, .03, .012], 'Bark', 5)
    blob('Canopy', c, (s, s * .78, s), 'LeafLight' if c[1] > 1.45 else 'Leaf', 20 + i, 2, .26)
asset('EDV2_Fern', ao=.35); fern('Fern', (0, 0, 0), 1, 7)

asset('EDV2_Rock', ao=.5)
stone('Stone', (0, 0, 0), (.64, .44, .52), 3, 'Slate')
blob('Stone', (.24, .09, .16), (.26, .18, .24), 'Stone', 9, 1, .3, .4)

asset('EDV2_Log', ao=.5)
pts = [(-.40 + i * .08, .125 + .008 * math.sin(i * 1.3), .012 * math.sin(i * .9)) for i in range(11)]
tube('Bark', pts, [.118 + .012 * math.sin(i * 2.1) for i in range(11)], 'Bark', 12, cap=False)
for side in [-1, 1]:
    x = side * .40
    for r0, r1, m in [(.118, .095, 'BarkLight'), (.095, .062, 'WoodLight'), (.062, .030, 'BarkLight'), (.030, 0, 'WoodLight')]:
        verts = []; n = 12
        for r in [r0, r1]:
            for k in range(n):
                a = k * math.tau / n; verts.append((x + side * .004, .125 + math.cos(a) * r, math.sin(a) * r) if r > 0 else (x + side * .004, .125, 0))
        faces = [(k, (k + 1) % n, n + (k + 1) % n, n + k) if side == 1 else (k, n + k, n + (k + 1) % n, (k + 1) % n) for k in range(n)]
        mesh('Endgrain', verts, faces, m)
tube('Bark', [(.10, .21, .02), (.14, .30, .06), (.17, .36, .11)], [.034, .026, .016], 'Bark', 6)
for i in range(5): blob('Moss', (-.30 + i * .14, .245, .01 * math.sin(i)), (.17, .05, .13), 'Moss', 40 + i, 1, .3, .1)
mushroom('Moss', (.30, .23, .03), .7, 2)

# ---------------------------------------------------------------- terrain
asset('EDV2_Terrain', ao=.55, ray=.5)
N = 72; outline = []
for i in range(N):
    a = i * math.tau / N; ca = math.cos(a); sa = math.sin(a)
    x = 5.30 * math.copysign(abs(ca) ** .55, ca); z = 6 + 6.28 * math.copysign(abs(sa) ** .55, sa)
    j = 1 - .014 * (1 + math.sin(i * 2.31) + .6 * math.sin(i * .77)); outline.append((x * j, 6 + (z - 6) * j))
def ring(scale, y, drop=0):
    return [(x * scale, y - drop * (.5 + .5 * math.sin(i * 1.9)), 6 + (z - 6) * scale) for i, (x, z) in enumerate(outline)]
def band(group, a, b, mat):
    mesh(group, a + b, [(i, (i + 1) % N, N + (i + 1) % N, N + i) for i in range(N)], mat)
# Rounded grass lip, a soil layer and a stone footing give the island a crafted section.
lip = [ring(1.0, 0), ring(1.022, -.035), ring(1.018, -.11, .03)]
band('Moss', lip[0], lip[1], 'Moss'); band('Moss', lip[1], lip[2], 'Moss')
soil = [ring(.985, -.11, .03), ring(.975, -.30), ring(.99, -.46)]
band('Soil', soil[0], soil[1], 'Soil'); band('Soil', soil[1], soil[2], 'Soil')
base = [ring(1.01, -.46), ring(1.035, -.60), ring(.97, -.74)]
band('Slate', base[0], base[1], 'Slate'); band('Slate', base[1], base[2], 'Slate')
under = ring(.97, -.74); mesh('Slate', under + [(0, -.78, 6)], [(i, (i + 1) % N, N) for i in range(N)], 'Slate')
for i in range(0, N, 2):
    x, z = outline[i]; blob('Slate', (x * 1.0, -.56 - .05 * math.sin(i), 6 + (z - 6) * 1.0), (.62, .36, .5), 'Slate' if i % 4 else 'Stone', 300 + i, 1, .3)
# Meadow top: concentric rings so baked shading and tone can vary across the lawn.
def bank(limit, front):
    clipped = []
    for i, a in enumerate(outline):
        b = outline[(i + 1) % N]; ia = (a[1] <= limit) if front else (a[1] >= limit); ib = (b[1] <= limit) if front else (b[1] >= limit)
        if ia: clipped.append(a)
        if ia != ib: t = (limit - a[1]) / (b[1] - a[1]); clipped.append((a[0] + (b[0] - a[0]) * t, limit))
    cx = sum(p[0] for p in clipped) / len(clipped); cz = sum(p[1] for p in clipped) / len(clipped); n = len(clipped); scales = [1, .93, .78, .52, .26]
    verts = [(cx + (x - cx) * s, 0, cz + (z - cz) * s) for s in scales for x, z in clipped] + [(cx, 0, cz)]
    faces = [(r * n + i, (r + 1) * n + i, (r + 1) * n + (i + 1) % n, r * n + (i + 1) % n) for r in range(len(scales) - 1) for i in range(n)]
    faces += [((len(scales) - 1) * n + i, len(verts) - 1, (len(scales) - 1) * n + (i + 1) % n) for i in range(n)]
    mesh('Moss', verts, faces, 'Moss')
bank(1.58, True); bank(2.62, False)
# Sloped creek banks and a pebbled bed under the water sheet.
for edge, side in [(1.58, -1), (2.62, 1)]:
    verts = []; n = 26
    for i in range(n + 1):
        x = -5.2 + i * .4; w = 1 if abs(x) < 5.0 else 0
        verts += [(x, 0, edge), (x, -.055 * w, edge - side * .16)]
    mesh('Moss', verts, [(i * 2, i * 2 + 1, i * 2 + 3, i * 2 + 2) if side < 0 else (i * 2, i * 2 + 2, i * 2 + 3, i * 2 + 1) for i in range(n)], 'Soil')
mesh('Moss', [(-5.25, -.06, 1.70), (5.25, -.06, 1.70), (5.25, -.06, 2.50), (-5.25, -.06, 2.50)], [(0, 3, 2, 1)], 'Path')
rng = random.Random(7)
for i in range(26):
    x = -4.9 + i * .38 + rng.uniform(-.08, .08); z = 2.1 + rng.uniform(-.28, .28)
    blob('BorderRocks', (x, -.045, z), (rng.uniform(.10, .2), .05, rng.uniform(.09, .17)), 'Stone' if i % 3 else 'Slate', 500 + i, 0, .2)
mesh('CreekSurface', [(-5.22, .0, 1.62), (5.22, .0, 1.62), (5.22, .0, 2.58), (-5.22, .0, 2.58)], [(0, 3, 2, 1)], 'CreekWater')
# The water sheet sits just below the lawn so the banks read as real edges.
groups['EDV2_Terrain']['CreekSurface'].v = [(x, -.018, z) for x, y, z in groups['EDV2_Terrain']['CreekSurface'].v]
for i in range(14):
    x = -4.6 + i * .7; z = 2.1 + .22 * math.sin(i * 1.7)
    tube('CreekFoam', [(x, -.012, z), (x + .12, -.012, z + .02), (x + .24, -.012, z - .01)], [.004, .010, .004], 'Foam', 4)
# Bank stones hide the hard waterline; a few carry moss.
for i in range(15):
    x = -4.7 + i * .67
    stone('BorderRocks', (x + .1 * math.sin(i), -.03, 2.66 + .05 * math.sin(i * 2)), (.34, .22, .26), 600 + i, 'Stone', i % 2 == 0)
    stone('BorderRocks', (x + .3, -.03, 1.55 - .04 * math.sin(i * 3)), (.26, .16, .2), 640 + i, 'Slate', False)
# Stepping-stone path in the flat front apron.
for i in range(15):
    x = -4.2 + i * .6; z = 1.02 + math.sin(i * .5) * .14
    lathe('Path', (x, 0, z), [(.20, .0), (.22, .012), (.20, .028), (0, .032)], 'Path' if i % 3 else 'Stone', 8, .18, i)
# Perimeter dressing only: boulders, ferns, grass, flowers and mushrooms follow the
# island edge, outside the rescue lanes and clear of the station on the front-left apron.
def edge(i, inset): x, z = outline[i % N]; return (x * inset, 6 + (z - 6) * inset)
for i in range(N):
    x, z = edge(i, .935)
    if z < 2.95: continue
    kind = i % 6
    if kind == 0: stone('BorderRocks', (x, 0, z), (.44, .32, .42), 700 + i, 'Stone' if i % 12 else 'Slate')
    elif kind in (1, 4): fern('RimGrowth', (x, 0, z), .95 + .2 * (i % 3), 720 + i)
    elif kind == 2: grass('RimGrowth', (x, 0, z), 1.3, 740 + i); flower('RimGrowth', edge(i, .89) [0:1] + (0,) + edge(i, .89)[1:2], 1.15, i, 'Petal' if i % 4 else 'PetalPink')
    elif kind == 3: mushroom('RimGrowth', (x, 0, z), 1.35, i); grass('RimGrowth', edge(i, .90)[0:1] + (0,) + edge(i, .90)[1:2], 1.0, 760 + i, 'Moss', 4)
    else: grass('RimGrowth', (x, 0, z), 1.2, 780 + i); flower('RimGrowth', edge(i, .96)[0:1] + (0,) + edge(i, .96)[1:2], 1.0, i + 3, 'PetalPink' if i % 3 else 'Petal')
# Thin grass tufts just inside the rim keep the lawn from ending in a hard line.
for i in range(0, N, 2):
    x, z = edge(i, .86 + .03 * math.sin(i))
    if z > 2.95: grass('RimGrowth', (x, 0, z), .85, 900 + i, 'MossLight', 4)
# Low three-blade tufts break up the open lawn without hiding fires or sheep.
for i in range(44):
    x = rng.uniform(-4.3, 4.3); z = rng.uniform(3.0, 11.0)
    grass('RimGrowth', (x, 0, z), rng.uniform(.45, .65), 1000 + i, 'MossLight', 4)
# Front-right apron: a little meadow corner beside the stepping stones.
for i in range(6):
    x = .6 + i * .62; grass('RimGrowth', (x, 0, .36 + .08 * math.sin(i)), .85, 950 + i, 'MossLight', 4)
    if i % 2 == 0: flower('RimGrowth', (x + .3, 0, .30), 1.0, i + 40)
# Lantern posts: front-right corner and the back-left rim; glass uses the emissive window material.
for p, side in [(Vector((3.85, 0, .48)), 1), (Vector(edge(N * 3 // 8, .90)[0:1] + (0,) + edge(N * 3 // 8, .90)[1:2]), -1)]:
    tube('Lantern', [p, p + Vector((0, .55, 0)), p + Vector((0, 1.02, 0)), p + Vector((-side * .16, 1.16, 0)), p + Vector((-side * .30, 1.12, 0))], [.045, .036, .03, .026, .022], 'Bark', 7)
    lamp = p + Vector((-side * .30, .86, 0))
    tube('Lantern', [lamp + Vector((0, .25, 0)), lamp + Vector((0, .17, 0))], .008, 'Brass', 5)
    lathe('Lantern', lamp, [(.085, .145), (.075, .16), (0, .19)], 'Brass', 8)
    lathe('LanternGlow', lamp, [(.052, .02), (.068, .08), (.062, .145)], 'WindowGlow', 8)
    lathe('Lantern', lamp, [(0, -.015), (.062, .0), (.058, .02)], 'Brass', 8)
    stone('BorderRocks', (p.x + side * .10, 0, p.z + .14), (.30, .20, .28), 990 + side, 'Stone', True)

# ---------------------------------------------------------------- station props (real metres)
asset('EDV2_WellPump', ao=.6, ray=.06, floor=False)
# Flat fieldstone plinth under the whole station.
for i, (x, z, sx, sz) in enumerate([(-.13, .07, .15, .14), (-.02, .10, .12, .11), (-.07, -.02, .13, .10), (.07, .06, .12, .13), (-.20, -.03, .08, .09), (.10, -.05, .11, .09)]):
    blob('Base', (x, .008, z), (sx, .024, sz), 'Stone' if i % 2 else 'Slate', 60 + i, 1, .18)
C = Vector((-.13, 0, .065))
# Enamel pump column with a flared foot and a domed cap.
lathe('Body', C, [(.052, .016), (.056, .024), (.046, .040), (.036, .058), (.034, .150), (.040, .164), (.042, .178), (.036, .192), (.020, .204), (0, .208)], 'Teal', 18)
for y in [.044, .158]: lathe('Hardware', C, [(.038, y - .006), (.0415, y - .003), (.0415, y + .003), (.038, y + .006)], 'Brass', 18)
for k in range(8):
    a = k * math.tau / 8; blob('Hardware', C + Vector((math.cos(a) * .049, .03, math.sin(a) * .049)), (.008, .006, .008), 'Brass', k, 0, 0)
# Glass water gauge and a cream leaf badge face the player.
rbox('Gauge', C + Vector((0, .105, -.0345)), (.014, .056, .006), 'Cyan', .45)
rbox('Hardware', C + Vector((0, .105, -.0335)), (.020, .064, .004), 'Brass', .3)
blade('Hardware', C + Vector((.022, .078, -.031)), C + Vector((.022, .118, -.027)), .009, 'Cream', .05, 2)
# The spout sweeps over to the outlet above the basin.
tube('Spout', [C + Vector((.02, .135, -.01)), C + Vector((.07, .158, -.03)), Vector((-.02, .160, .01)), Vector((.0, .146, -.005)), Vector((.0, .126, -.014))], [.019, .019, .0185, .018, .0175], 'Teal', 10)
lathe('Hardware', (0, .118, -.014), [(.0185, .0), (.021, .003), (.021, .010), (.0185, .013)], 'Brass', 12)
# Forked hinge bracket that carries the lever pivot at y 0.20.
for side in [-1, 1]:
    rbox('Hardware', C + Vector((side * .014, .198, 0)), (.006, .034, .022), 'Brass', .3)
tube('Hardware', [C + Vector((-.02, .20, 0)), C + Vector((.02, .20, 0))], .006, 'Brass', 8)

asset('EDV2_PumpLever', ao=.4, ray=.04, floor=False)
# Origin at the hinge; the wooden handle reaches 0.22 m toward the player along -Z.
tube('Lever', [(0, 0, .035), (0, .004, .0), (0, .020, -.06), (0, .026, -.12), (0, .014, -.18), (0, 0, -.225)], [.007, .008, .0085, .009, .010, .0115], 'WoodLight', 10)
blob('Lever', (0, -.001, -.226), (.027, .027, .036), 'WoodLight', 4, 1, .05)
lathe('Lever', (0, 0, 0), [(0, -.013), (.012, -.012), (.012, .012), (0, .013)], 'Brass', 10)
for z in [-.03, -.165]:
    tube('Lever', [(0, .012 if z > -.1 else .018, z + .006), (0, .012 if z > -.1 else .018, z - .006)], .0125, 'Brass', 10)

asset('EDV2_Basin', ao=.65, ray=.05, floor=False)
# Carved stone altar bowl: foot, swelling body, thick rim and a real hollow for the water.
lathe('Stone', (0, 0, 0), [(.050, 0), (.056, .006), (.044, .014), (.046, .022), (.074, .040), (.080, .050), (.078, .056), (.068, .056), (.062, .046), (.040, .030), (0, .028)], 'Stone', 20, .04, 2)
lathe('Hardware', (0, 0, 0), [(.0795, .050), (.0825, .053), (.0795, .057), (.0765, .0575)], 'Brass', 20)
for k in range(5):
    a = k * 1.26 + .3; blob('Moss', (math.cos(a) * .058, .020, math.sin(a) * .058), (.03, .018, .026), 'Moss', 70 + k, 0, .2)
for k in range(4):
    a = k * math.tau / 4 + .8; blade('Hardware', (math.cos(a) * .0715, .030, math.sin(a) * .0715), (math.cos(a) * .0785, .046, math.sin(a) * .0785), .007, 'Cream', .02, 2)

asset('EDV2_Reservoir', ao=.55)
# Sprinkler tower in forest units: enamel tank on a splayed timber frame, tank centre at y 1.45.
for i, (x, z) in enumerate([(-.30, -.26), (.30, -.26), (.30, .26), (-.30, .26)]):
    blob('Footing', (x * 1.05, .03, z * 1.05), (.26, .09, .24), 'Stone' if i % 2 else 'Slate', 80 + i, 1, .2)
    tube('Legs', [(x, .04, z), (x * .82, .6, z * .82), (x * .66, 1.14, z * .66)], [.05, .042, .038], 'Wood', 6)
for a0, a1 in [((-.27, .5, -.235), (.27, .5, -.235)), ((-.27, .5, .235), (.27, .5, .235)), ((-.27, .5, -.235), (-.27, .5, .235)), ((.27, .5, -.235), (.27, .5, .235))]:
    tube('Legs', [a0, a1], .028, 'WoodLight', 5)
lathe('Legs', (0, 0, 0), [(.30, 1.10), (.38, 1.11), (.38, 1.16), (.30, 1.17)], 'WoodLight', 14)
lathe('Tank', (0, 0, 0), [(.29, 1.16), (.345, 1.21), (.352, 1.40), (.352, 1.62), (.33, 1.72), (.20, 1.80), (0, 1.83)], 'Teal', 18)
for y in [1.25, 1.58]: lathe('Hardware', (0, 0, 0), [(.350, y - .022), (.368, y - .010), (.368, y + .010), (.350, y + .022)], 'Brass', 18)
tube('Hardware', [(0, 1.82, 0), (0, 1.90, 0)], .03, 'Brass', 8)
blade('Hardware', (.16, 1.34, -.325), (.16, 1.50, -.31), .04, 'Cream', .04, 2)
tube('Hardware', [(.352, 1.34, 0), (.46, 1.34, 0), (.46, 1.22, 0)], .028, 'Brass', 7)

# ---------------------------------------------------------------- retained V2/V3 assets
# The creek pump keeps its authored definition; it is rebuilt through the same buffers
# so it also receives smoothing and baked shading.
def box(group, pos, size, mat, yaw=0): rbox(group, pos, size, mat, 0, yaw)
def beam(group, a, b, r, mat, n=8, r2=None): tube(group, [a, b], [r, r if r2 is None else r2], mat, n)
def rock(group, pos, size, mat='Stone', seed=0): blob(group, (pos[0], pos[1] + size[1] * .5, pos[2]), size, mat, seed, 1, .25, .5)
def leaf(group, a, b, width, mat, bend=.012): blade(group, a, b, width, mat, .08, 2)
legacy = (ROOT / 'Tools/BuildEmberdeepArtV2.py').read_text(encoding='utf-8')
def section(start, end): return legacy[legacy.index(start):legacy.index(end)]
for start, end, opts in [("asset('EDV2_CreekPump')", "# V3 refinement", dict(ao=.5, ray=.05, floor=False, recalc=True))]:
    code = section(start, end)
    for name in re.findall(r"asset\('(\w+)'\)", code): code = code.replace(f"asset('{name}')", f"asset('{name}', **{opts!r})")
    exec(code)

# ---------------------------------------------------------------- Art V5 additions
exec(compile((ROOT / 'Tools/EmberdeepArtV5Assets.py').read_text(encoding='utf-8'), 'EmberdeepArtV5Assets.py', 'exec'))

# ---------------------------------------------------------------- build, bake, export
def bake_and_build(name, buffers, col):
    opts = options[name]; strength = opts.get('ao', .5); floor = opts.get('floor', True)
    allv = [v for b in buffers.values() for v in b.v]
    lo = min(v[1] for v in allv); hi = max(v[1] for v in allv)
    if name != 'EDV2_Terrain' and floor:
        for b in buffers.values(): b.v = [(x, y - lo, z) for x, y, z in b.v]
        hi -= lo; lo = 0
    ray = opts.get('ray', max(.12, (hi - lo) * .28))
    objs = []
    for semantic, buf in buffers.items():
        me = bpy.data.meshes.new(semantic); me.from_pydata([B(p) for p in buf.v], [], [tuple(reversed(f)) for f in buf.f]); me.update()
        for m in buf.materials: me.materials.append(mats[m])
        for poly, i in zip(me.polygons, buf.mi): poly.material_index = i
        import bmesh
        bm = bmesh.new(); bm.from_mesh(me)
        if opts.get('recalc'): bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        # Open sheets that must face the sky regardless of construction winding.
        if (name == 'EDV2_Terrain' and semantic in ('Moss', 'CreekSurface')) or semantic == 'Roof':
            for face in bm.faces:
                if face.normal.z < -.3: face.normal_flip()
        bm.to_mesh(me); bm.free(); me.update()
        if semantic in opts.get('flat', ()): me.polygons.foreach_set('use_smooth', [False] * len(me.polygons))
        else: me.polygons.foreach_set('use_smooth', [True] * len(me.polygons)); me.set_sharp_from_angle(angle=math.radians(50))
        me.update()
        obj = bpy.data.objects.new(semantic, me); col.objects.link(obj); objs.append(obj)
    # One occluder tree per asset so neighbouring parts shade each other.
    verts = []; tris = []
    for obj in objs:
        off = len(verts); verts += [v.co.copy() for v in obj.data.vertices]
        for poly in obj.data.polygons:
            idx = [off + i for i in poly.vertices]
            for k in range(1, len(idx) - 1): tris.append((idx[0], idx[k], idx[k + 1]))
    if floor and name != 'EDV2_Terrain':
        off = len(verts); s = 50; verts += [Vector((-s, -s, 0)), Vector((s, -s, 0)), Vector((s, s, 0)), Vector((-s, s, 0))]; tris += [(off, off + 1, off + 2), (off, off + 2, off + 3)]
    tree = BVHTree.FromPolygons(verts, tris)
    rnd = random.Random(11); dirs = []
    while len(dirs) < 20:
        d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1)))
        if .2 < d.length <= 1: dirs.append(d.normalized())
    for obj in objs:
        me = obj.data; ao = []
        foliage = obj.name in ('Canopy', 'Fern', 'RimGrowth', 'Fronds', 'Growth', 'Flame')
        for v in me.vertices:
            n = v.normal; origin = v.co + n * (ray * .04); hit = 0; total = 0
            for d in dirs:
                w = d.dot(n)
                if w <= .05: continue
                total += w
                if tree.ray_cast(origin, d, ray)[0] is not None: hit += w
            occ = hit / total if total else 0
            h = (v.co.z - lo) / max(1e-6, hi - lo)
            tone = (1 - strength * occ) * (.90 + .16 * h if name != 'EDV2_Terrain' else 1)
            if name == 'EDV2_Terrain' and obj.name == 'Moss':
                tone *= .93 + .12 * noise.noise(Vector((v.co.x * .55, v.co.y * .55, 0)))
            # Terrain growth sways by its height above the lawn, not above the island's stone foot.
            sway = max(0, min(1, v.co.z / .35)) if name == 'EDV2_Terrain' else h
            ao.append((max(.25, min(1.15, tone)), sway if foliage else 0))
        color = me.color_attributes.new(name='ArtTint', type='FLOAT_COLOR', domain='CORNER')
        for loop in me.loops:
            t, sway = ao[loop.vertex_index]; color.data[loop.index].color = (t, t, t, sway)
    # Animated parts keep their own origin: geometry moves back by the pivot and the object carries it.
    for obj in objs:
        pivot = opts.get('pivots', {}).get(obj.name)
        if pivot is None: continue
        offset = Vector(B(pivot))
        for v in obj.data.vertices: v.co -= offset
        obj.location = offset; obj.data.update()
    return objs

stats = []
for name, buffers in groups.items():
    col = bpy.data.collections.new(name); scene.collection.children.link(col)
    parent = bpy.data.objects.new(name, None); col.objects.link(parent)
    objects = bake_and_build(name, buffers, col)
    for obj in objects: obj.parent = parent
    assets[name] = objects
    bpy.ops.object.select_all(action='DESELECT'); parent.select_set(True)
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    path = EXPORT / (name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH', 'EMPTY'}, global_scale=1,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type='OFF', colors_type='LINEAR', add_leaf_bones=False, bake_anim=False, path_mode='AUTO')
    tris = sum(len(p.vertices) - 2 for obj in objects for p in obj.data.polygons)
    allv = [v for b in buffers.values() for v in b.v]
    stats.append({'asset': name, 'file': str(path.relative_to(ROOT)).replace('\\', '/'), 'triangles': tris,
        'bounds_unity': {'min': [round(min(v[i] for v in allv), 4) for i in range(3)], 'max': [round(max(v[i] for v in allv), 4) for i in range(3)]},
        'meshes': [{'name': o.name, 'triangles': sum(len(p.vertices) - 2 for p in o.data.polygons), 'materials': [m.name for m in o.data.materials]} for o in objects]})
total = sum(s['triangles'] for s in stats)
manifest = {'kit': 'Emberdeep Art V5 rounded miniature forest, characters, gloves, interface and camp', 'generator': 'Tools/BuildEmberdeepArtV4.py', 'seed': 40117,
    'source': 'ArtSource/EmberdeepV3/Emberdeep-ArtV4.blend', 'concepts': 'ArtSource/EmberdeepV3/Concepts',
    'provenance': 'Original procedural meshes authored for this project; no downloaded mesh or texture assets.',
    'vertex_colour': 'ArtTint RGB = baked ambient occlusion x height tone (linear); A = foliage sway weight.',
    'contracts': 'Front z<1.6 flat; CreekSurface centred near z 2.1; tree lanes z 3..10 free of baked trees; station props in real metres.',
    'total_unique_triangles': total, 'assets': stats}
(SOURCE / 'ArtV4-Manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'Emberdeep-ArtV4.blend'))
print('EMBERDEEP_ART_V4_COMPLETE ' + json.dumps({'triangles': total, 'assets': {s['asset']: s['triangles'] for s in stats}}))
