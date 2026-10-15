"""Mixamo 업로드 대체본: 몸+철모를 한 메시로 합치고, 축 변환 굽기 없이, 텍스처는 묻지 않은 FBX / OBJ+텍스처 zip."""
import sys, os, zipfile
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import *

bpy.ops.wm.open_mainfile(filepath=p("blender", "seokgu_04_final.blend"))
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects["Seokgu"]
bpy.ops.object.join()
body = bpy.context.active_object
body.name = "Seokgu"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

out_dir = p("export", "mixamo_alt")
os.makedirs(out_dir, exist_ok=True)
# 텍스처를 파일로 꺼냄
for img in bpy.data.images:
    if img.packed_file:
        img.filepath_raw = os.path.join(out_dir, img.name + ".png")
        img.file_format = 'PNG'
        img.save()
        img.unpack(method='REMOVE') if False else None

fbx = os.path.join(out_dir, "Seokgu_mixamo_v2.fbx")
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=False, path_mode='STRIP', embed_textures=False,
                         mesh_smooth_type='FACE')
obj = os.path.join(out_dir, "Seokgu.obj")
bpy.ops.wm.obj_export(filepath=obj, export_selected_objects=True, export_materials=True,
                      path_mode='STRIP', forward_axis='NEGATIVE_Z', up_axis='Y')
z = os.path.join(out_dir, "Seokgu_mixamo_obj.zip")
with zipfile.ZipFile(z, "w", zipfile.ZIP_DEFLATED) as zf:
    for f in os.listdir(out_dir):
        if f.endswith((".obj", ".mtl", ".png")):
            zf.write(os.path.join(out_dir, f), f)
for f in (fbx, z):
    print("OUT", f, round(os.path.getsize(f) / 1048576, 2), "MB")
print("TRIS", tris(body), "OBJECTS", len([o for o in bpy.data.objects if o.type == 'MESH']))
