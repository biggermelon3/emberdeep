"""Export the current editable Blender scene without regenerating its geometry."""
import bpy, os
ROOT=os.path.abspath(os.path.join(os.path.dirname(__file__),'../..'))
source=os.path.join(ROOT,'ArtSource/CreekDiorama/CreekDiorama.blend')
bpy.ops.wm.open_mainfile(filepath=source)
for o in list(bpy.data.objects):
    if o.type=='EMPTY' and not o.children:bpy.data.objects.remove(o,do_unlink=True)
bpy.ops.object.select_all(action='DESELECT')
for c in bpy.data.collections:
    if c.name[:2] in ['01','02','03','04','05','06','07','08','09']:
        for o in c.objects:o.select_set(True)
objects=list(bpy.context.selected_objects)
bpy.context.view_layer.objects.active=next(o for o in objects if o.type=='MESH')
bpy.ops.wm.save_as_mainfile(filepath=source)
bpy.ops.object.duplicate()
duplicates=list(bpy.context.selected_objects)
bpy.ops.object.convert(target='MESH')
for o in duplicates:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(ROOT,'Assets/CreekDiorama/Models/CreekDiorama.fbx'),use_selection=True,object_types={'MESH','EMPTY'},global_scale=.2,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
print('EXPORTED_WITH_HIERARCHY',len(duplicates))
