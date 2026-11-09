"""Render the existing Art V2 hydrant as the Android launcher icon.
Run with Blender --background --python Tools/RenderEmberdeepHydrantIcon.py.
No source model or previous icon is overwritten.
"""
import bpy
import hashlib
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/EmberdeepV2/Emberdeep-ArtV2.blend'
OUTPUT = ROOT / 'Assets/FireRescue/Art/AppIcon/Emberdeep-Hydrant-Icon.png'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
scene = bpy.context.scene
for collection in bpy.data.collections:
    collection.hide_render = collection.name != 'EDV2_Hydrant'
studio = bpy.data.collections.new('Hydrant icon studio')
scene.collection.children.link(studio)

def link(obj):
    studio.objects.link(obj)
    return obj

def aim(obj, target=(0, 0, .12)):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z', 'Y').to_euler()

for name, position, energy, color in [
    ('Warm key', (-.35, .45, .6), 22, (1, .87, .70)),
    ('Cool fill', (.4, .25, .35), 12, (.65, .86, 1)),
    ('Rim', (.05, -.35, .5), 25, (.65, 1, .91)),
]:
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.color, data.size = energy, color, .45
    obj = link(bpy.data.objects.new(name, data))
    obj.location = position
    aim(obj)

mesh = bpy.data.meshes.new('Icon floor')
mesh.from_pydata([(-20,-20,-.004),(20,-20,-.004),(20,20,-.004),(-20,20,-.004)], [], [(0,1,2,3)])
floor = link(bpy.data.objects.new('Icon floor', mesh))
mat = bpy.data.materials.new('Icon forest green')
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value = (.008,.036,.028,1)
bsdf.inputs['Roughness'].default_value = 1
floor.data.materials.append(mat)
camera_data = bpy.data.cameras.new('Hydrant launcher camera')
camera_data.type = 'ORTHO'
camera_data.ortho_scale = .40
camera = link(bpy.data.objects.new('Hydrant launcher camera', camera_data))
camera.location = (.34,.65,.35)
aim(camera)
scene.camera = camera
scene.world.use_nodes = True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value = (.055,.09,.075,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value = .3
scene.render.engine = 'CYCLES'
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.view_settings.look = 'AgX - Medium High Contrast'
scene.view_settings.exposure = -1.5
scene.render.resolution_x = scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGB'
scene.render.film_transparent = False
scene.render.filepath = str(OUTPUT)
bpy.ops.render.render(write_still=True)
metadata = {'title':'Emberdeep hydrant launcher icon', 'source':str(SOURCE.relative_to(ROOT)),
    'model':'EDV2_Hydrant', 'generator':'Tools/RenderEmberdeepHydrantIcon.py / Blender 4.3',
    'pixels':[1024,1024], 'output':str(OUTPUT.relative_to(ROOT)),
    'sha256':hashlib.sha256(OUTPUT.read_bytes()).hexdigest(),
    'provenance':'Rendered from the original project-owned Art V2 hydrant; no downloaded assets.',
    'masking':'Central safe area for Android round and adaptive masks; opaque forest green background.'}
OUTPUT.with_suffix('.json').write_text(json.dumps(metadata, indent=2)+'\n', encoding='utf-8')
print(json.dumps(metadata))
