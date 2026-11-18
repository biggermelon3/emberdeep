"""Executed by BuildEmberdeepArtV2.py --art-v3; dimensions and semantic IDs stay stable."""

# Sculpt the broadleaf silhouette around visible, forked timber.
asset('EDV2_Broadleaf')
beam('Trunk',(0,0,0),(.035,.67,.018),.090,'Bark',10,.052)
beam('Trunk',(.035,.55,.018),(-.075,1.38,.035),.056,'Bark',9,.018)
for j in range(6):
    a=j*math.tau/6+.3
    beam('Trunk',(.018,.18,0),(math.cos(a)*.18,.012,math.sin(a)*.16),.035,'Bark',6,.009)
for j in range(11):
    a=j*2.399;h=.88+(j%4)*.145;r=.19+(j%3)*.055
    end=Vector((math.cos(a)*r,h,math.sin(a)*r))
    fork=Vector((.025,.54+j*.043,.01))
    beam('Trunk',fork,end,.032,'Bark',7,.010)
    beam('Trunk',end,end+Vector((math.cos(a)*.07,.12,math.sin(a)*.07)),.012,'BarkLight',5,.004)
    # A low irregular closed core keeps the crown solid through all view angles.
    rock('Canopy',tuple(end-Vector((0,.035,0))),(.36+(j%2)*.07,.22,.34),'Leaf' if j%3 else 'LeafLight',1800+j)
    for k in range(11):
        aa=a+k*2.399;rad=.105+(k%3)*.025
        start=end+Vector((math.cos(aa)*rad*.35,.10+(k%3)*.02,math.sin(aa)*rad*.35))
        tip=end+Vector((math.cos(aa)*rad*1.5,.065+(k%2)*.045,math.sin(aa)*rad*1.5))
        leaf('Canopy',start,tip,.052,'LeafLight' if k%4==0 else 'Leaf',.024)
rock('Canopy',(-.04,1.42,.02),(.38,.26,.35),'LeafLight',1880)

# Cedar: break the regular rings, expose branch ends and give each tier a lean.
current='EDV2_Cedar'
buf=g('Canopy')
buf.v=[(x+.020*math.sin(y*5),y+.018*math.sin(x*17+z*11),z+.012*math.sin(y*7)) for x,y,z in buf.v]
for tier in range(5):
    h=.43+tier*.22;r=.37*(1-tier*.13)
    for j in range(5):
        a=j*math.tau/5+tier*.66
        end=(math.cos(a)*r,h-.025,math.sin(a)*r)
        beam('Trunk',(0,h+.09,0),end,.013,'BarkLight',5,.004)
        leaf('Canopy',(end[0]*.78,h+.045,end[2]*.78),(end[0]*1.08,h-.035,end[2]*1.08),.045,'CedarLight',.025)

# Details share existing semantic meshes, avoiding extra renderer proliferation.
current='EDV2_Cabin'
for side in [-1,1]:
    beam('Timber',(side*.47,.12,-.40),(side*.47,.80,-.40),.021,'BarkLight',6)
    for j in range(4):
        z=-.25+j*.16
        beam('Hardware',(side*.47,.19,z),(side*.485,.19,z),.007,'Brass',6)
box('Timber',(.235,.35,-.43),(.32,.035,.10),'WoodLight')
for j in range(3):
    beam('Timber',(.50,.08,.12+j*.08),(.50,.08,.17+j*.08),.045,'Bark',7)
    beam('Timber',(.50,.08,.115+j*.08),(.50,.08,.12+j*.08),.035,'WoodLight',7)

current='EDV2_Rock'
for j in range(4):
    a=j*2.399
    rock('Moss',(math.cos(a)*.10,.22+(.02 if j%2 else 0),math.sin(a)*.085),(.12,.025,.10),'Moss',1900+j)

current='EDV2_Log'
# Low relief moss follows the upper bark; small twigs break the cylindrical outline.
for j in range(5):
    rock('Moss',(-.21+j*.10,.224,.012*math.sin(j)),(.12,.018,.08),'Moss',1930+j)
beam('Bark',(.12,.12,.025),(.15,.24,.07),.022,'Bark',6,.008)

current='EDV2_Fern'
for j in range(3):
    a=j*2.4;base=Vector((math.cos(a)*.035,0,math.sin(a)*.035))
    last=base
    for k in range(6):
        t=(k+1)/6;at=base+Vector((math.cos(a)*.035*math.sin(t*2),t*.19,math.sin(a)*.035*math.sin(t*2)))
        beam('Fronds',last,at,.004,'Fern',4,.003);last=at
    leaf('Fronds',last,last+Vector((.018,.015,0)),.014,'LeafLight',.007)

for kit in ['EDV2_Hydrant','EDV2_CreekPump']:
    current=kit
    for side in [-1,1]:
        beam('Hardware',(side*.028,.067,-.030),(side*.028,.067,-.034),.005,'Brass',6)

current='EDV2_Terrain'
# Small flower patches live only on the peripheral bank, outside rescue lanes.
for side in [-1,1]:
    for j in range(8):
        x=side*(4.72+.07*math.sin(j*1.9));z=3.1+j*1.05
        for k in range(3):
            px=x+(k-1)*.065;pz=z+math.sin(k*2.3)*.06;h=.08+k*.015
            beam('RimGrowth',(px,.005,pz),(px,h,pz),.003,'Fern',4)
            for petal in range(4):
                a=petal*math.tau/4
                leaf('RimGrowth',(px,h,pz),(px+math.cos(a)*.032,h+.012,pz+math.sin(a)*.032),.015,'WindowGlow',.007)
for j in range(18):
    x=-4.05+j*.46
    for side in [-1,1]:
        rock('BorderRocks',(x,.001,2.1+side*(.47+.02*math.sin(j))),(.10,.035,.08),'Stone',2000+j*2+side)
