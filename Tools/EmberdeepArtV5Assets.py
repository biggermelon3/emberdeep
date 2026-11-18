"""Emberdeep Art V5 additions, executed inside Tools/BuildEmberdeepArtV4.py.

Gloves and crystal, sheep, cabin, wheel and button water stations, interface frames
and the camp set. Authored in Unity-style coordinates (X right, Y up, Z depth) with
right-hand-rule outward winding, exactly like the forest kit above.
"""

# ---------------------------------------------------------------- extra primitives
def _signed_volume(verts, faces):
    total = 0
    for f in faces:
        a = Vector(verts[f[0]])
        for k in range(1, len(f) - 1): total += a.dot(Vector(verts[f[k]]).cross(Vector(verts[f[k + 1]])))
    return total

def loft(group, rings, mat, cap=True):
    """Skin a list of equal-length closed rings; fan caps close both ends. Winding is solved."""
    n = len(rings[0]); verts = [tuple(p) for ring in rings for p in ring]; faces = []
    for j in range(len(rings) - 1):
        for k in range(n):
            a = j * n + k; b = j * n + (k + 1) % n; faces.append((a, b, b + n, a + n))
    caps = []
    for ring, base, flip in [(rings[0], 0, True), (rings[-1], (len(rings) - 1) * n, False)]:
        c = Vector((0, 0, 0))
        for p in ring: c += Vector(p)
        verts.append(tuple(c / n)); ci = len(verts) - 1
        for k in range(n): caps.append((ci, base + (k + 1) % n, base + k) if flip else (ci, base + k, base + (k + 1) % n))
    if _signed_volume(verts, faces + caps) < 0:
        faces = [tuple(reversed(f)) for f in faces]; caps = [tuple(reversed(f)) for f in caps]
    mesh(group, verts, faces + (caps if cap else []), mat)

def hoop(group, points, radius, mat, sides=6):
    """Closed tube through a loop of points."""
    pts = [Vector(p) for p in points]; n = len(pts); verts = []; faces = []
    c = Vector((0, 0, 0))
    for p in pts: c += p
    c /= n; normal = Vector((0, 0, 0))
    for i in range(n): normal += (pts[i] - c).cross(pts[(i + 1) % n] - c)
    normal.normalize()
    for i, p in enumerate(pts):
        d = (pts[(i + 1) % n] - pts[i - 1]).normalized(); out = normal.cross(d).normalized()
        for k in range(sides):
            a = k * math.tau / sides; verts.append(p + radius * (math.cos(a) * out + math.sin(a) * normal))
    for i in range(n):
        for k in range(sides):
            a = i * sides + k; b = i * sides + (k + 1) % sides; a2 = ((i + 1) % n) * sides + k; b2 = ((i + 1) % n) * sides + (k + 1) % sides
            faces.append((a, b, b2, a2))
    if _signed_volume(verts, faces) < 0: faces = [tuple(reversed(f)) for f in faces]
    mesh(group, verts, faces, mat)

def circle(radius, count, y=0, rz=None, phase=0):
    return [(math.cos(k * math.tau / count + phase) * radius, y, math.sin(k * math.tau / count + phase) * (radius if rz is None else rz)) for k in range(count)]

def extrude(group, outline, depth, mat, z=0, bevel=.78):
    """Star-shaped XY outline extruded along Z; the -Z face (toward the viewer) is softened."""
    cx = sum(p[0] for p in outline) / len(outline); cy = sum(p[1] for p in outline) / len(outline)
    def ring(scale, zz): return [(cx + (x - cx) * scale, cy + (y - cy) * scale, zz) for x, y in outline]
    loft(group, [ring(bevel, z - depth * .5), ring(1, z - depth * .2), ring(1, z + depth * .5)], mat)

def sheet(group, grid, mat):
    """Double-sided quad sheet from a grid of points (rows of equal length)."""
    rows = len(grid); cols = len(grid[0]); verts = [tuple(p) for row in grid for p in row]; faces = []
    for r in range(rows - 1):
        for c in range(cols - 1):
            a = r * cols + c; faces.append((a, a + 1, a + cols + 1, a + cols))
    mesh(group, verts, faces, mat); mesh(group, verts, [tuple(reversed(f)) for f in faces], mat)

def board(pos=(0, 0, 0)):
    """Author flat ornaments in XZ (z = up on the board); they land in XY facing the viewer at -Z."""
    return xf(pos, (-90, 0, 0))

def lantern(group, glow, hook, s=1.0):
    """Hanging lantern below `hook`; the glass uses the emissive window material."""
    t = Vector(hook); body = t + Vector((0, -.205 * s, 0))
    tube(group, [t, t + Vector((0, -.04 * s, 0))], .006 * s, 'Brass', 5)
    lathe(group, body, [(0, -.012 * s), (.050 * s, 0), (.046 * s, .016 * s)], 'Brass', 8)
    lathe(glow, body, [(.040 * s, .016 * s), (.054 * s, .07 * s), (.048 * s, .125 * s)], 'WindowGlow', 8)
    lathe(group, body, [(.062 * s, .122 * s), (.050 * s, .142 * s), (0, .170 * s)], 'Brass', 8)
    for k in range(4):
        a = k * math.tau / 4 + .78; d = Vector((math.cos(a), 0, math.sin(a)))
        tube(group, [body + d * .047 * s + Vector((0, .016 * s, 0)), body + d * .057 * s + Vector((0, .07 * s, 0)), body + d * .051 * s + Vector((0, .124 * s, 0))], .004 * s, 'Brass', 4, cap=False)

def leaf_pair(group, mat, reach=.05, width=.012, spread=38):
    """Two leaves fanning from the origin in the XZ plane (use inside board())."""
    for side in [-1, 1]:
        a = math.radians(90 - side * spread)
        blade(group, (0, 0, 0), (math.cos(a) * reach, 0, math.sin(a) * reach), width, mat, -.12, 3)

FLAME = [(0, 0), (.42, .12), (.60, .42), (.46, .72), (.30, .95), (.34, 1.25), (.10, 1.62), (-.06, 1.30), (-.30, 1.05), (-.52, .70), (-.58, .38), (-.40, .10)]
HEART = []
for k in range(24):
    t = k * math.tau / 24
    HEART.append((16 * math.sin(t) ** 3, 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t) + 2.5))
DROP = [(0, -.5), (.30, -.36), (.40, -.08), (.30, .22), (.12, .52), (0, .82), (-.12, .52), (-.30, .22), (-.40, -.08), (-.30, -.36)]

# ---------------------------------------------------------------- guardian gloves
asset('EDV2_GloveCuff', ao=.45, ray=.03, floor=False)
# Palm frame: +Z toward the fingers, +Y out of the palm, X across the wrist; origin at the wrist joint.
tube('Sleeve', [(0, .001, -.046), (0, .001, -.030), (0, .001, -.008), (0, .0022, .016)], [(.0385, .0315), (.0385, .0315), (.0350, .0275), (.0296, .0226)], 'GloveGreen', 20, cap=False)
tube('Strap', [(0, .001, -.034), (0, .001, -.032), (0, .001, -.014), (0, .001, -.012)], [(.0385, .0315), (.0405, .0335), (.0400, .0330), (.0380, .0305)], 'Leather', 20, cap=False)
tube('Trim', [(0, .001, -.0632), (0, .001, -.0625)], [(.0005, .0005), (.030, .023)], 'Leather', 20, cap=False)
tube('Trim', [(0, .001, -.0625), (0, .001, -.0612), (0, .001, -.057), (0, .001, -.049), (0, .001, -.044)], [(.030, .023), (.038, .031), (.0420, .0350), (.0420, .0350), (.039, .032)], 'Cream', 20, cap=False)
for k in range(11):
    # Overlapping tufts merge into one soft fleece roll.
    a = k * math.tau / 11
    blob('Trim', (math.cos(a) * .0395, .001 + math.sin(a) * .0325, -.0535 + .0025 * math.sin(k * 2.3)), (.030, .030, .024), 'Cream', 30 + k, 1, .18)
# Brass stud and buckle on the back of the wrist; cream piping on both strap edges.
blob('Hardware', (0, -.0335, -.023), (.012, .0055, .012), 'Brass', 1, 1, 0)
for x in [-.019, .019]: rbox('Hardware', (x, -.0300, -.023), (.0045, .004, .020), 'Brass', .3, 0, 0)
for z in [-.0338, -.0122]: tube('Trim', [(0, .001, z - .0006), (0, .001, z + .0006)], [(.0404, .0334), (.0404, .0334)], 'Cream', 20, cap=False)

asset('EDV2_Crystal', ao=.3, ray=.02, floor=False, flat=['Gem'])
# Crystal frame: -Y faces out of the back of the hand, +Z toward the knuckles. The gem keeps
# its +-0.029 length so the fill shader's wrist-to-knuckle level still maps onto it.
HEX = [(0, .029), (.0125, .0125), (.0125, -.0125), (0, -.029), (-.0125, -.0125), (-.0125, .0125)]
def hexring(sx, y, sz=None): return [(x * sx, y, z * (sx if sz is None else sz)) for x, z in HEX]
loft('Gem', [hexring(.40, -.0125, .66), hexring(1, -.0045), hexring(.92, .0015)], 'Gem')
hoop('Setting', hexring(1.14, -.0032, 1.07), .0021, 'Brass', 6)
for z, tip in [(.031, .0425), (-.031, -.0425)]: tube('Setting', [(0, -.0032, z), (0, -.0040, tip)], [.0030, .0006], 'Brass', 6)
for side in [-1, 1]:
    blade('Setting', (side * .006, -.0025, -.031), (side * .0245, -.0040, -.011), .0058, 'Brass', -.16, 3)
    blade('Setting', (side * .005, -.0025, -.035), (side * .0190, -.0032, -.0445), .0040, 'Brass', -.14, 2)
    blob('Setting', (side * .0148, -.0036, .0), (.0042, .0042, .0042), 'Brass', 3, 0, 0)
# Cream leather patch stitched under the setting.
loft('Plate', [circle(.0186, 18, .0036, .0335), circle(.0186, 18, .0004, .0335), circle(.0166, 18, -.0008, .0310)], 'Cream')
hoop('Plate', circle(.0188, 18, .0010, .0338), .0011, 'Leather', 4)

# ---------------------------------------------------------------- sheep
HEAD = Vector((0, .69, -.38)); EAR = Vector((-.22, .77, -.38)); LEG = Vector((-.22, .28, -.24))
asset('EDV2_Sheep', ao=.42, ray=.22, floor=False, pivots={'Head': tuple(HEAD), 'Ear': tuple(EAR), 'Leg': tuple(LEG), 'Heart': (0, 1.1, 0), 'Coin': (0, .8, 0)})
blob('Body', (0, .50, .04), (.70, .58, .90), 'Wool', 1, 2, .08)
for k in range(24):
    # Fibonacci-distributed wool curls; the belly stays smooth.
    y = 1 - (k + .5) * 2 / 24; r = math.sqrt(max(0, 1 - y * y)); a = k * 2.399963
    if y < -.42: continue
    d = Vector((math.cos(a) * r, y, math.sin(a) * r)); size = .27 + .05 * math.sin(k * 1.7)
    blob('Body', (d.x * .29, .50 + d.y * .235, .04 + d.z * .385), (size, size * .94, size), 'WoolLight' if k % 3 == 0 else 'Wool', 60 + k, 1, .16)
blob('Body', (0, .58, .50), (.20, .19, .19), 'WoolLight', 9, 1, .15)
blob('Head', HEAD, (.46, .44, .44), 'Cocoa', 3, 2, .035)
blob('Head', HEAD + Vector((0, -.10, -.15)), (.30, .21, .24), 'CocoaLight', 4, 1, .03)
for side in [-1, 1]:
    blob('Head', HEAD + Vector((side * .108, .030, -.184)), (.138, .168, .07), 'EyeWhite', 0, 1, 0)
    blob('Head', HEAD + Vector((side * .106, .022, -.207)), (.098, .128, .05), 'Hoof', 0, 1, 0)
    blob('Head', HEAD + Vector((side * .122, .058, -.228)), (.034, .034, .02), 'EyeWhite', 0, 0, 0)
    blob('Head', HEAD + Vector((side * .166, -.076, -.166)), (.09, .052, .04), 'PetalPink', 0, 1, 0)
blob('Head', HEAD + Vector((0, -.066, -.268)), (.072, .046, .03), 'Hoof', 0, 1, 0)
tube('Head', [HEAD + Vector((-.045, -.128, -.250)), HEAD + Vector((0, -.146, -.262)), HEAD + Vector((.045, -.128, -.250))], .008, 'Hoof', 4)
for i, (x, y, z, s) in enumerate([(0, .20, -.05, .22), (-.13, .17, -.02, .20), (.13, .17, -.02, .20), (-.07, .20, .10, .21), (.07, .20, .10, .21), (0, .14, .17, .22)]):
    blob('Head', HEAD + Vector((x, y, z)), (s, s * .92, s), 'WoolLight' if i % 2 else 'Wool', 90 + i, 1, .16)
blob('Ear', EAR + Vector((-.12, -.012, 0)), (.30, .11, .17), 'Cocoa', 5, 1, .05)
blob('Ear', EAR + Vector((-.13, .020, -.036)), (.19, .05, .10), 'PetalPink', 6, 1, .03)
tube('Leg', [LEG + Vector((0, .04, 0)), LEG + Vector((0, -.08, 0)), LEG + Vector((0, -.175, 0))], [.064, .057, .051], 'Cocoa', 8)
blob('Leg', LEG + Vector((0, -.215, -.012)), (.15, .105, .18), 'Hoof', 7, 1, .05, .5)
with xf((0, 1.1, 0), scale=.0066): extrude('Heart', HEART, 11, 'PetalPink', 0, .72)
with xf((0, .8, 0), (90, 0, 0)):
    lathe('Coin', (0, 0, 0), [(0, -.022), (.13, -.022), (.152, -.012), (.152, .012), (.13, .022), (0, .022)], 'Brass', 16)
    lathe('Coin', (0, 0, 0), [(0, -.028), (.088, -.028), (.098, -.022)], 'Petal', 16)
    lathe('Coin', (0, 0, 0), [(.098, .022), (.088, .028), (0, .028)], 'Petal', 16)

# ---------------------------------------------------------------- rescue cabin (forest units)
asset('EDV2_Cabin', ao=.55)
W = .44; D = .38; FRONT = -D - .058
rbox('Body', (0, .42, 0), (W * 2 - .05, .62, D * 2 - .05), 'Bark', .05)
for i in range(6):
    y = .16 + i * .105
    for z in (-D, D): tube('Body', [(-W - .075, y, z), (0, y + .004, z), (W + .075, y, z)], [.056, .058, .056], 'Wood' if i % 2 else 'WoodLight', 7)
    if i < 5:
        for x in (-W, W): tube('Body', [(x, y + .052, -D - .075), (x, y + .056, 0), (x, y + .052, D + .075)], .056, 'WoodLight' if i % 2 else 'Wood', 7)
for z in (-D, D): extrude('Body', [(-W - .03, .73), (W + .03, .73), (0, 1.17)], .07, 'WoodLight', z, .96)
# Shingled terracotta roof: five overlapping rows per slope over a dark deck.
RIDGE = 1.24; EAVE_X = .66; DROP_Y = .55; slope = math.degrees(math.atan2(DROP_Y, EAVE_X)); run = math.hypot(EAVE_X, DROP_Y)
for side in [-1, 1]:
    with xf((side * EAVE_X * .5, RIDGE - DROP_Y * .5 - .03, 0), (0, 0, -side * slope)): rbox('Roof', (0, 0, 0), (run, .03, 1.10), 'Bark', 0)
    for r in range(5):
        t = (r + .5) / 5; cols = 7 + r % 2; width = 1.14 / cols
        for c in range(cols):
            z = -.57 + (c + .5) * width; lift = .012 + .004 * math.sin(r * 3 + c * 1.7)
            with xf((side * EAVE_X * t * 1.03, RIDGE - DROP_Y * t * 1.03 + lift, z), (0, 0, -side * (slope - 7))):
                rbox('Roof', (0, 0, 0), (run / 5 * 1.28, .034, width * .95), 'TerracottaLight' if (r + c) % 3 == 0 else 'Terracotta', .32)
tube('Roof', [(0, RIDGE + .01, -.60), (0, RIDGE + .018, 0), (0, RIDGE + .01, .60)], .05, 'Terracotta', 7)
for i, (x, y, z, s) in enumerate([(-.30, 1.02, -.34, .20), (.40, .93, .30, .24), (-.48, .87, .16, .18), (.14, 1.16, -.46, .16)]):
    blob('Moss', (x, y, z), (s, s * .34, s * .9), 'Moss', 110 + i, 1, .3, .2)
# Fieldstone chimney on the back slope.
for layer in range(5):
    for k in range(4):
        x = .25 + (.062 if k % 2 else -.062) + .012 * math.sin(layer * 2 + k); z = .14 + (.062 if k < 2 else -.062)
        blob('Chimney', (x, .93 + layer * .115, z), (.15, .135, .15), 'Slate' if (layer + k) % 3 == 0 else 'Stone', 130 + layer * 4 + k, 1, .22)
rbox('Chimney', (.25, 1.515, .14), (.30, .05, .30), 'Slate', .3)
rbox('Chimney', (.25, 1.545, .14), (.17, .02, .17), 'Hoof', .2)
# Arched door, glowing windows and a porch lantern.
ARCH = [(-.125, .135), (.125, .135), (.125, .44), (.090, .525), (0, .565), (-.090, .525), (-.125, .44)]
with xf((.13, 0, 0)):
    extrude('Door', ARCH, .04, 'Wood', FRONT + .012, .93)
    tube('Door', [(x, y, FRONT - .006) for x, y in [ARCH[0]] + ARCH[6:1:-1] + [ARCH[1]]], .017, 'Bark', 6)
    for x in [-.045, .045]: tube('Door', [(x, .15, FRONT - .012), (x, .53, FRONT - .012)], .004, 'Bark', 4, cap=False)
    blob('Hardware', (.078, .34, FRONT - .028), (.03, .03, .03), 'Brass', 2, 1, 0)
    lathe('Path', (0, 0, FRONT - .16), [(.19, 0), (.20, .014), (.17, .034), (0, .038)], 'Stone', 9, .16, 3)
def window(center, facing):
    with xf(center, (0, facing, 0)):
        rbox('WindowGlow', (0, 0, 0), (.17, .17, .03), 'WindowGlow', .2)
        for a, b in [((-.095, -.095), (.095, -.095)), ((.095, -.095), (.095, .095)), ((.095, .095), (-.095, .095)), ((-.095, .095), (-.095, -.095))]:
            tube('Door', [(a[0], a[1], -.012), (b[0], b[1], -.012)], .014, 'Bark', 5)
        tube('Door', [(0, -.085, -.018), (0, .085, -.018)], .007, 'Bark', 4, cap=False); tube('Door', [(-.085, 0, -.018), (.085, 0, -.018)], .007, 'Bark', 4, cap=False)
        rbox('Door', (0, -.118, -.02), (.23, .028, .05), 'WoodLight', .3)
window((-.21, .46, FRONT + .01), 0); window((-W - .05, .46, .02), 90); window((W + .05, .46, -.04), -90)
tube('Hardware', [(.33, .64, FRONT + .02), (.33, .66, FRONT - .09)], .008, 'Brass', 5)
lantern('Hardware', 'WindowGlow', (.33, .66, FRONT - .09), .62)
# Fieldstone footing, a woodpile and a little growth at the corners.
for i in range(18):
    t = i / 18 * 4; edge_i = int(t); f = t - edge_i
    x, z = [(-W + f * 2 * W, -D), (W, -D + f * 2 * D), (W - f * 2 * W, D), (-W, D - f * 2 * D)][edge_i]
    blob('Foundation', (x * 1.10, .055, z * 1.12), (.25, .17, .23), 'Stone' if i % 3 else 'Slate', 160 + i, 1, .26, .5)
for i, (y, z) in enumerate([(.055, -.10), (.055, .0), (.055, .10), (.135, -.05), (.135, .05), (.21, 0)]):
    tube('Woodpile', [(W + .13, y, z + .0), (W + .40, y, z + .012 * math.sin(i))], .046, 'BarkLight' if i % 2 else 'Bark', 7)
    for x in [W + .105, W + .425]: blob('Woodpile', (x, y, z), (.012, .078, .078), 'WoodLight', i, 0, 0)
for i, (x, z) in enumerate([(-W - .16, -D - .14), (W + .1, -D - .2), (-W - .12, D + .12), (-.34, -D - .2)]):
    grass('Growth', (x, 0, z), 1.1, 180 + i, 'MossLight', 5)
flower('Growth', (-.42, 0, -D - .2), 1.0, 4); flower('Growth', (-W - .2, 0, -.1), 1.0, 9, 'PetalPink'); mushroom('Growth', (W + .22, 0, .26), 1.1, 2)

# ---------------------------------------------------------------- wheel hydrant (real metres)
asset('EDV2_Hydrant', ao=.55, ray=.06, floor=False)
# Origin under the body; the station places it 7.5 cm behind the outlet line.
for i, (x, z, sx, sz) in enumerate([(-.05, .02, .10, .10), (.05, .03, .11, .10), (0, -.05, .12, .09), (-.06, -.04, .08, .08), (.07, -.04, .08, .08), (0, .07, .12, .08)]):
    blob('Base', (x, .008, z), (sx, .024, sz), 'Stone' if i % 2 else 'Slate', 200 + i, 1, .18)
for i, a in enumerate([.6, 2.4, 4.3]): blob('Moss', (math.cos(a) * .072, .016, math.sin(a) * .072), (.04, .02, .035), 'Moss', 210 + i, 0, .25)
lathe('Body', (0, 0, 0), [(.050, .014), (.055, .022), (.047, .036), (.0425, .052), (.0425, .150), (.047, .160), (.047, .172), (.041, .184), (.030, .195), (.022, .200)], 'Teal', 20)
for y in [.042, .166]:
    lathe('Hardware', (0, 0, 0), [(.0445, y - .007), (.0485, y - .0035), (.0485, y + .0035), (.0445, y + .007)], 'Brass', 20)
    for k in range(10):
        a = k * math.tau / 10; blob('Hardware', (math.cos(a) * .0492, y, math.sin(a) * .0492), (.0055, .0055, .0055), 'Brass', k, 0, 0)
# Lantern cap: brass collar, warm glass, peaked roof and finial.
lathe('Hardware', (0, 0, 0), [(.0225, .199), (.0265, .203), (.0265, .209), (.0215, .212)], 'Brass', 12)
lathe('LanternGlow', (0, 0, 0), [(.0190, .210), (.0225, .222), (.0200, .236)], 'WindowGlow', 10)
lathe('Hardware', (0, 0, 0), [(.0290, .235), (.0220, .244), (.0090, .254), (0, .258)], 'Brass', 12)
blob('Hardware', (0, .262, 0), (.010, .010, .010), 'Brass', 2, 1, 0)
for k in range(4):
    a = k * math.tau / 4 + .78; tube('Hardware', [(math.cos(a) * .0205, .210, math.sin(a) * .0205), (math.cos(a) * .0235, .236, math.sin(a) * .0235)], .0022, 'Brass', 4, cap=False)
# Capped side outlets, a porthole gauge and a leaf badge on the front.
for side in [-1, 1]:
    tube('Hardware', [(side * .038, .112, 0), (side * .060, .112, 0)], .017, 'Brass', 10, cap=False)
    with xf((side * .060, .112, 0), (0, 0, -side * 90)): lathe('Hardware', (0, 0, 0), [(.0200, -.002), (.0215, .002), (.0215, .010), (.0150, .014), (0, .015)], 'Cyan', 10)
with xf((0, 0, 0), (0, -38, 0)):
    with xf((0, .112, -.0425), (-90, 0, 0)):
        lathe('Hardware', (0, 0, 0), [(.0170, -.004), (.0185, 0), (.0185, .004), (.0150, .006)], 'Brass', 14)
        lathe('Gauge', (0, 0, 0), [(.0150, .0040), (.0090, .0062), (0, .0068)], 'Cyan', 12)
with board((0, .070, -.0440)): leaf_pair('Hardware', 'Cream', .024, .0065, 34)
# Outlet spout reaching forward over the basin's near rim.
tube('Spout', [(0, .128, -.036), (0, .140, -.058), (0, .138, -.078), (0, .124, -.088)], [.0150, .0145, .0140, .0135], 'Brass', 10)
lathe('Spout', (0, .112, -.088), [(.0140, .0), (.0165, .003), (.0165, .010), (.0140, .013)], 'Cyan', 12)

asset('EDV2_ValveWheel', ao=.4, ray=.03, floor=False)
# Origin on the axle; the wheel faces the player (-Z) and turns about Z.
hoop('Wheel', [(math.cos(k * math.tau / 20) * .0455, math.sin(k * math.tau / 20) * .0455, 0) for k in range(20)], .0068, 'Cyan', 8)
for k in range(5):
    a = k * math.tau / 5 + math.pi / 2; d = Vector((math.cos(a), math.sin(a), 0))
    tube('Wheel', [d * .012, d * .028 + Vector((0, 0, -.003)), d * .044], [.0050, .0044, .0050], 'Cyan', 6, cap=False)
    blob('Wheel', d * .0455 + Vector((0, 0, -.004)), (.016, .016, .012), 'Cyan', k, 1, 0)
with xf((0, 0, 0), (-90, 0, 0)):
    lathe('Hub', (0, 0, 0), [(.0150, -.006), (.0165, -.002), (.0165, .004), (.0120, .009), (0, .011)], 'Brass', 12)
    lathe('Hub', (0, 0, 0), [(.0075, .0100), (.0060, .0140), (0, .0155)], 'Cream', 8)

asset('EDV2_ValveMount', ao=.5, ray=.05, floor=False)
# Origin at the wheel axle, 17 cm above the stone footing; a gearbox drum on its own pedestal.
with xf((0, 0, 0), (90, 0, 0)):
    lathe('Body', (0, 0, 0), [(0, .0045), (.0200, .005), (.0300, .010), (.0335, .018), (.0335, .040), (.0280, .048), (0, .050)], 'Teal', 16)
    lathe('Hardware', (0, 0, 0), [(.0340, .020), (.0362, .023), (.0362, .029), (.0340, .032)], 'Brass', 16)
lathe('Body', (0, -.17, .029), [(.034, .010), (.037, .018), (.028, .032), (.021, .050), (.0195, .135), (.024, .150)], 'Teal', 14)
lathe('Hardware', (0, -.17, .029), [(.0215, .040), (.0245, .043), (.0245, .049), (.0215, .052)], 'Brass', 14)
for i, (x, z) in enumerate([(-.03, .0), (.035, .01), (0, .06), (0, -.02)]):
    blob('Base', (x, -.165, .029 + z), (.075, .022, .07), 'Slate' if i % 2 else 'Stone', 220 + i, 1, .18)
# Feed pipe running back toward the hydrant line.
tube('Hardware', [(0, -.012, .048), (0, -.016, .075), (0, -.040, .090)], .010, 'Brass', 8)

asset('EDV2_ButtonStation', ao=.55, ray=.06, floor=False)
# Water dispenser: enamel bell, glass tank and a mossy plank roof. Origin under the body.
lathe('Base', (0, 0, 0), [(.074, .0), (.078, .006), (.076, .014), (.066, .018)], 'Wood', 18, .03, 4)
for i, a in enumerate([.3, 1.9, 3.4, 5.0]):
    blob('Base', (math.cos(a) * .082, .008, math.sin(a) * .082), (.05, .022, .045), 'Stone' if i % 2 else 'Slate', 230 + i, 1, .2)
    if i % 2: blob('Moss', (math.cos(a + .5) * .078, .018, math.sin(a + .5) * .078), (.036, .018, .03), 'Moss', 240 + i, 0, .25)
lathe('Body', (0, 0, 0), [(.058, .016), (.0625, .026), (.0615, .064), (.0560, .100), (.0500, .120)], 'Teal', 20)
lathe('Hardware', (0, 0, 0), [(.0495, .117), (.0525, .120), (.0525, .127), (.0490, .130)], 'Brass', 20)
lathe('Tank', (0, 0, 0), [(.0465, .129), (.0480, .142), (.0480, .156)], 'Cyan', 18)
lathe('Tank', (0, 0, 0), [(.0480, .156), (.0470, .170), (.0440, .180)], 'Glass', 18)
lathe('Hardware', (0, 0, 0), [(.0440, .179), (.0465, .182), (.0465, .188), (.0300, .193), (0, .195)], 'Brass', 18)
# Curved plank roof on two brackets, with moss along the ridge.
arc = []
for k in range(9):
    a = math.radians(-64 + k * 16); x = math.sin(a) * .098; y = .158 + math.cos(a) * .062
    nx, ny = math.sin(a), math.cos(a)
    arc.append([(x + nx * .009, y + ny * .009, -.068), (x + nx * .009, y + ny * .009, .068), (x - nx * .004, y - ny * .004, .068), (x - nx * .004, y - ny * .004, -.068)])
loft('Roof', arc, 'Wood')
for z in [-.071, .071]:
    tube('Roof', [(math.sin(math.radians(-64 + k * 16)) * .098, .158 + math.cos(math.radians(-64 + k * 16)) * .062 + .004, z) for k in range(9)], .0058, 'WoodLight', 5)
for i, x in enumerate([-.05, -.012, .03, .062]): blob('Moss', (x, .225 - abs(x) * .28, .012 * math.sin(i * 2)), (.045, .016, .05), 'Moss', 250 + i, 0, .3)
for side in [-1, 1]: tube('Roof', [(side * .050, .120, .0), (side * .068, .150, .0), (side * .080, .186, .0)], .0055, 'WoodLight', 5)
# Dial gauge, leaf crest and the outlet tap.
with xf((.036, .084, -.0505), (-90, 0, 28)):
    lathe('Hardware', (0, 0, 0), [(.0180, -.004), (.0195, 0), (.0195, .005), (.0165, .007)], 'Brass', 14)
    lathe('Gauge', (0, 0, 0), [(.0165, .0055), (0, .0062)], 'Cream', 12)
    tube('Gauge', [(0, .0075, 0), (.0085, .0075, .0085)], .0013, 'EnamelRed', 4)
with board((-.024, .062, -.0625)): leaf_pair('Hardware', 'Cream', .022, .006, 34)
tube('Spout', [(0, .104, -.050), (0, .132, -.066), (0, .136, -.080), (0, .124, -.088)], [.0135, .0130, .0125, .0120], 'Brass', 10)
lathe('Spout', (0, .112, -.088), [(.0125, .0), (.0150, .003), (.0150, .010), (.0125, .013)], 'Cyan', 12)

asset('EDV2_ButtonPost', ao=.5, ray=.05, floor=False)
# Origin at the button's resting face, 17 cm above the footing; the cap presses in along +Z.
with xf((0, 0, 0), (90, 0, 0)):
    lathe('Body', (0, 0, 0), [(0, .0035), (.0330, .004), (.0400, .010), (.0420, .022), (.0420, .046), (.0340, .056), (0, .058)], 'Teal', 18)
    lathe('Hardware', (0, 0, 0), [(.0310, .0015), (.0345, .0030), (.0345, .0085), (.0310, .0100)], 'Brass', 18)
lathe('Body', (0, -.17, .032), [(.036, .010), (.039, .018), (.030, .034), (.022, .054), (.0205, .128), (.027, .146)], 'Teal', 14)
lathe('Hardware', (0, -.17, .032), [(.0225, .042), (.0255, .045), (.0255, .051), (.0225, .054)], 'Brass', 14)
for i, (x, z) in enumerate([(-.03, .0), (.035, .01), (0, .06), (0, -.02)]):
    blob('Base', (x, -.165, .032 + z), (.075, .022, .07), 'Stone' if i % 2 else 'Slate', 260 + i, 1, .18)
tube('Hardware', [(0, -.014, .054), (0, -.018, .080), (0, -.042, .094)], .010, 'Brass', 8)

asset('EDV2_PushButton', ao=.3, ray=.02, floor=False)
with xf((0, 0, 0), (-90, 0, 0)):
    lathe('Cap', (0, 0, 0), [(.0270, -.010), (.0285, -.004), (.0280, .003), (.0215, .0085), (.0100, .0108), (0, .0112)], 'Cream', 18)
with xf((0, -.002, -.0105), scale=.021): extrude('Cap', DROP, .12, 'Cyan', 0, .8)

# ---------------------------------------------------------------- interface frame (real metres)
asset('EDV2_SignRail', ao=.3, ray=.02, floor=False)
# One metre of rail along X, stretched to length at runtime.
tube('Rail', [(-.5, 0, 0), (-.25, .0012, .0006), (0, -.0008, -.0005), (.25, .0010, .0004), (.5, 0, 0)], .0105, 'Wood', 8, cap=False)

asset('EDV2_SignCorner', ao=.4, ray=.03, floor=False)
# Top-right corner; the other three are this piece turned in quarter steps.
blob('Knot', (0, 0, 0), (.036, .036, .030), 'WoodLight', 3, 1, .12)
with board((-.004, -.004, -.0155)):
    for a, reach in [(180, .046), (270, .046), (225, .034)]:
        r = math.radians(a); blade('Leaf', (0, 0, 0), (math.cos(r) * reach, 0, math.sin(r) * reach), .0085 if a != 225 else .0065, 'Brass', -.10, 3)
blob('Leaf', (-.004, -.004, -.0165), (.0085, .0085, .006), 'Brass', 1, 0, 0)

asset('EDV2_SignCrest', ao=.45, ray=.04, floor=False)
# Sits on the middle of the top rail: carved plaque, brass sprout and an ember.
extrude('Plaque', [(-.092, -.014), (.092, -.014), (.092, .012), (.066, .036), (0, .050), (-.066, .036), (-.092, .012)], .020, 'Wood', 0, .90)
hoop('Plaque', [(x, y, -.010) for x, y in [(-.080, -.006), (.080, -.006), (.080, .010), (.058, .029), (0, .041), (-.058, .029), (-.080, .010)]], .0016, 'Brass', 4)
with xf((0, .0005, -.0125), scale=.019): extrude('Ember', FLAME, .22, 'Ember', 0, .7)
with board((0, .002, -.0115)):
    for side in [-1, 1]:
        blade('Leaf', (side * .012, 0, .0), (side * .050, 0, .020), .0075, 'Brass', -.10, 3); blade('Leaf', (side * .014, 0, .0), (side * .040, 0, -.006), .0055, 'Brass', -.10, 3)
for i, (x, y) in enumerate([(-.085, .020), (.082, .018), (-.060, .040), (.050, .044)]):
    blob('Moss', (x, y, .0), (.040, .020, .030), 'Moss', 270 + i, 0, .3)
with board((.078, .030, -.008)): leaf_pair('Moss', 'LeafLight', .022, .007, 30)
with board((-.082, .030, -.008)): leaf_pair('Moss', 'Leaf', .020, .007, 30)

asset('EDV2_SignLantern', ao=.4, ray=.04, floor=False)
# Bracket arm reaching +X from the frame's upper corner with a hanging lantern.
tube('Bracket', [(0, 0, 0), (.030, .020, 0), (.062, .020, 0)], [.0065, .0055, .0045], 'Bark', 6)
lantern('Bracket', 'Glow', (.060, .020, 0), .40)

asset('EDV2_SignMoss', ao=.4, ray=.03, floor=False)
# A clump for the frame's lower corners and rail tops.
for i, (x, y, s) in enumerate([(0, 0, .042), (.026, -.004, .030), (-.024, -.003, .032), (.008, .010, .026)]):
    blob('Moss', (x, y, -.004), (s, s * .5, s * .7), 'Moss', 280 + i, 0, .3)
with board((0, .004, -.014)): leaf_pair('Moss', 'LeafLight', .026, .008, 40)
with xf((0, 0, -.012), scale=.09): flower('Moss', (.26, 0, 0), 1, 3, 'Petal')

# ---------------------------------------------------------------- camp set (real metres)
asset('EDV2_CampGround', ao=.5, ray=.35, floor=False)
# A round meadow under the menu sign. Origin below the sign; the player walks in from -Z.
CN = 56; CAMP = Vector((0, 0, -.45)); CR = 2.35
camp_outline = []
for i in range(CN):
    a = i * math.tau / CN; j = 1 - .03 * (1 + math.sin(i * 1.7) + .5 * math.sin(i * .6))
    camp_outline.append((CAMP.x + math.cos(a) * CR * j, CAMP.z + math.sin(a) * CR * j))
def cring(scale, y, drop=0): return [(CAMP.x + (x - CAMP.x) * scale, y - drop * (.5 + .5 * math.sin(i * 1.9)), CAMP.z + (z - CAMP.z) * scale) for i, (x, z) in enumerate(camp_outline)]
def cband(group, a, b, mat): mesh(group, a + b, [(i, (i + 1) % CN, CN + (i + 1) % CN, CN + i) for i in range(CN)], mat)
tops = [cring(s, 0) for s in [1, .9, .7, .45, .2]]
for a, b in zip(tops, tops[1:]): mesh('Moss', a + b, [(i, CN + i, CN + (i + 1) % CN, (i + 1) % CN) for i in range(CN)], 'Moss')
mesh('Moss', tops[-1] + [(CAMP.x, 0, CAMP.z)], [(i, CN, (i + 1) % CN) for i in range(CN)], 'Moss')
lipc = [cring(1.0, 0), cring(1.012, -.03), cring(1.008, -.10, .03)]
cband('Moss', lipc[0], lipc[1], 'Moss'); cband('Moss', lipc[1], lipc[2], 'Moss')
soilc = [cring(.99, -.10, .03), cring(.975, -.26), cring(.985, -.40)]
cband('Soil', soilc[0], soilc[1], 'Soil'); cband('Soil', soilc[1], soilc[2], 'Soil')
basec = [cring(1.0, -.40), cring(1.02, -.52), cring(.93, -.66)]
cband('Slate', basec[0], basec[1], 'Slate'); cband('Slate', basec[1], basec[2], 'Slate')
mesh('Slate', cring(.93, -.66) + [(CAMP.x, -.72, CAMP.z)], [(i, (i + 1) % CN, CN) for i in range(CN)], 'Slate')
for i in range(0, CN, 2):
    x, z = camp_outline[i]; blob('Slate', (x, -.47 - .04 * math.sin(i), z), (.42, .26, .36), 'Slate' if i % 4 else 'Stone', 300 + i, 1, .3)
# Stepping stones from the arrival edge to the sign, and footing rocks for its two posts.
for i in range(6):
    z = -2.45 + i * .37; x = .10 * math.sin(i * 1.3)
    lathe('Path', (x, 0, z), [(.17, 0), (.185, .010), (.165, .024), (0, .028)], 'Path' if i % 2 else 'Stone', 9, .18, i)
for side in [-1, 1]:
    for k in range(3):
        a = k * 2.1 + side; stone('Rocks', (side * .52 + math.cos(a) * .09, 0, .04 + math.sin(a) * .08), (.16, .11, .15), 310 + k + side, 'Stone', k == 0)
    for k in range(4):
        a = k * math.tau / 4 + .5; d = Vector((math.cos(a), 0, math.sin(a)))
        tube('Rocks', [Vector((side * .52, .12, .03)) + d * .02, Vector((side * .52, .04, .03)) + d * .07, Vector((side * .52, 0, .03)) + d * .12], [.022, .017, .008], 'Bark', 5)
def cedge(i, inset): x, z = camp_outline[i % CN]; return (CAMP.x + (x - CAMP.x) * inset, CAMP.z + (z - CAMP.z) * inset)
for i in range(CN):
    x, z = cedge(i, .93)
    if z < -1.9 and abs(x) < .7: continue
    kind = i % 6
    if kind == 0: stone('Rocks', (x, 0, z), (.40, .28, .36), 320 + i, 'Stone' if i % 12 else 'Slate')
    elif kind in (1, 4): fern('Growth', (x, 0, z), .95 + .2 * (i % 3), 330 + i)
    elif kind == 2: grass('Growth', (x, 0, z), 1.25, 340 + i); flower('Growth', cedge(i, .88)[0:1] + (0,) + cedge(i, .88)[1:2], 1.1, i, 'Petal' if i % 4 else 'PetalPink')
    elif kind == 3: mushroom('Growth', (x, 0, z), 1.3, i); grass('Growth', cedge(i, .89)[0:1] + (0,) + cedge(i, .89)[1:2], 1.0, 350 + i, 'Moss', 4)
    else: grass('Growth', (x, 0, z), 1.15, 360 + i); flower('Growth', cedge(i, .96)[0:1] + (0,) + cedge(i, .96)[1:2], 1.0, i + 3, 'PetalPink' if i % 3 else 'Petal')
rng = random.Random(21)
for i in range(46):
    a = rng.uniform(0, math.tau); r = rng.uniform(.5, 2.0); x = CAMP.x + math.cos(a) * r; z = CAMP.z + math.sin(a) * r
    if abs(x) < .75 and -2.4 < z < -.2: continue
    grass('Growth', (x, 0, z), rng.uniform(.5, .8), 400 + i, 'MossLight', 4)

asset('EDV2_SignPost', ao=.35, ray=.05, floor=False)
# Unit-height log; the camp stretches it from the ground to the top of the menu board.
tube('Post', [(0, 0, 0), (.004, .35, .002), (-.003, .7, -.002), (0, 1, 0)], [.040, .036, .034, .033], 'Wood', 8, cap=False)

asset('EDV2_SignPostCap', ao=.45, ray=.04, floor=False)
lathe('Cap', (0, 0, 0), [(.033, -.01), (.043, .0), (.045, .012), (.030, .030), (0, .040)], 'WoodLight', 8, .05, 2)
blob('Moss', (.006, .034, 0), (.06, .026, .055), 'Moss', 420, 1, .3, .2)
with board((0, .0, -.046)): leaf_pair('Moss', 'LeafLight', .03, .009, 40)

asset('EDV2_Tent', ao=.5, ray=.3)
# Canvas A-frame: ridge along Z at 1.05 m, opening toward -Z.
TH = 1.05; TW = .66; TL = .72
for side in [-1, 1]:
    grid = []
    for r in range(6):
        t = r / 5; row = []
        for c in range(7):
            z = -TL + c * TL * 2 / 6; sag = math.sin(t * math.pi) * (.045 + .02 * math.sin(c * 1.4 + side))
            row.append((side * (TW * t - sag * .6), TH * (1 - t) - sag * .55 + (.012 if r == 5 else 0), z))
        grid.append(row)
    sheet('Canvas', grid, 'Canvas')
    # Folded-back door flaps and a ground skirt.
    sheet('Canvas', [[(0, TH, -TL - .004), (side * .10, TH * .55, -TL - .03)], [(side * TW, .012, -TL - .004), (side * .40, .012, -TL - .05)]], 'CanvasDark')
    tube('Poles', [(side * (TW + .05), 0, -TL - .03), (0, TH + .07, -TL - .03)], .020, 'Wood', 6)
    for z in [-TL + .08, TL - .08]: tube('Poles', [(side * (TW + .09), .06, z), (side * (TW + .12), -.01, z)], .012, 'Bark', 4)
sheet('Canvas', [[(0, TH, TL), (0, TH, TL)], [(-TW, .012, TL), (TW, .012, TL)]], 'CanvasDark')
tube('Poles', [(0, TH + .025, -TL - .16), (0, TH + .03, 0), (0, TH + .025, TL + .10)], .022, 'Wood', 7)
sheet('Canvas', [[(-TW + .03, .014, -TL + .02), (TW - .03, .014, -TL + .02)], [(-TW + .03, .014, TL - .02), (TW - .03, .014, TL - .02)]], 'CanvasDark')
tube('Bedroll', [(-.28, .10, .12), (.28, .10, .12)], .095, 'Cedar', 10)
for x in [-.16, .16]: tube('Bedroll', [(x - .012, .10, .12), (x + .012, .10, .12)], .099, 'Cream', 10, cap=False)
lantern('Poles', 'Glow', (0, TH + .01, -TL - .14), .8)

asset('EDV2_Campfire', ao=.5, ray=.15)
for k in range(9):
    a = k * math.tau / 9; blob('Stones', (math.cos(a) * .27, .05, math.sin(a) * .27), (.16, .12, .15), 'Stone' if k % 3 else 'Slate', 430 + k, 1, .25, .5)
blob('Embers', (0, .03, 0), (.36, .07, .36), 'Ember', 3, 1, .2, .3)
for k in range(5):
    a = k * math.tau / 5 + .3; d = Vector((math.cos(a), 0, math.sin(a)))
    tube('Logs', [d * .21 + Vector((0, .035, 0)), d * .10 + Vector((0, .14, 0)), d * .0 + Vector((0, .25, 0))], [.042, .038, .032], 'Bark' if k % 2 else 'BarkLight', 7)
for x, z, s in [(0, 0, 1.0), (.07, .05, .62), (-.06, -.045, .55)]:
    lathe('Flame', (x, .05, z), [(0, 0), (.070 * s, .05 * s), (.088 * s, .13 * s), (.064 * s, .24 * s), (.028 * s, .34 * s), (0, .43 * s)], 'Flame', 9, .12, x * 20)

asset('EDV2_CampCrate', ao=.5, ray=.12)
rbox('Chest', (0, .14, 0), (.52, .26, .34), 'Teal', .12)
with xf((0, .275, 0)): 
    lid = []
    for k in range(7):
        a = math.radians(-78 + k * 26); lid.append([(-.265, math.cos(a) * .09, math.sin(a) * .178), (.265, math.cos(a) * .09, math.sin(a) * .178), (.265, math.cos(a) * .075, math.sin(a) * .160), (-.265, math.cos(a) * .075, math.sin(a) * .160)])
    loft('Chest', lid, 'Teal')
    for x in [-.18, .18]:
        tube('Hardware', [(x, math.cos(math.radians(-78 + k * 26)) * .094, math.sin(math.radians(-78 + k * 26)) * .184) for k in range(7)], .012, 'Brass', 5)
for x in [-.18, .18]: rbox('Hardware', (x, .14, 0), (.03, .27, .352), 'Brass', .2)
rbox('Hardware', (0, .245, -.175), (.07, .06, .02), 'Brass', .3)
with board((0, .12, -.172)): leaf_pair('Hardware', 'Cream', .05, .014, 36)
