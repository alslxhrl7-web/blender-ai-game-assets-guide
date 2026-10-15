"""5단계 비교용 나란히 씬 + 8단계 FBX 내보내기 + stats_after.csv"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import *

STAGE_OFFSETS = {"seokgu": (-0.7, 0.0, 0.0), "garand": (0.5, -0.3, 0.0), "sandbag": (0.0, 4.2, 0.0)}


def lineup(stage, out):
    reset_scene()
    for key, off in STAGE_OFFSETS.items():
        path = p("blender", f"{key}_{stage}.blend")
        with bpy.data.libraries.load(path) as (src, dst):
            dst.objects = list(src.objects)
        for o in dst.objects:
            bpy.context.scene.collection.objects.link(o)
            if o.parent is None:
                o.location = (o.location.x + off[0], o.location.y + off[1], o.location.z + off[2])
    bpy.ops.wm.save_as_mainfile(filepath=out)


lineup("02_opt", p("blender", "lineup_style_before.blend"))
lineup("03_style", p("blender", "lineup_style_after.blend"))

FBX_OPTS = dict(apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                path_mode='COPY', embed_textures=True, use_mesh_modifiers=True, mesh_smooth_type='FACE',
                bake_space_transform=True, add_leaf_bones=False)  # bake_space_transform = Apply Transform: Unity에서 X 270° 회전이 붙지 않게 축 변환을 메시에 굽는다

for key in RES:
    bpy.ops.wm.open_mainfile(filepath=p("blender", f"{key}_04_final.blend"))
    bpy.ops.object.select_all(action='SELECT')
    out = p(RES[key][5])
    os.makedirs(os.path.dirname(out), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'MESH', 'EMPTY', 'ARMATURE'}, **FBX_OPTS)
    print("EXPORT", out, os.path.getsize(out))
    if key == "seokgu":
        m = p("export", "Seokgu_for_mixamo.fbx")
        bpy.ops.export_scene.fbx(filepath=m, use_selection=True, object_types={'MESH'}, **FBX_OPTS)
        print("EXPORT", m, os.path.getsize(m))

# stats_after: 내보낸 FBX를 새 씬으로 다시 불러와 2단계와 같은 항목으로 잰다
rows = []
for key in RES:
    reset_scene()
    out = p(RES[key][5])
    objs = import_fbx(out)
    rows += stats_rows(key, objs, out)
write_csv(p("stats_after.csv"), rows)
for r in rows:
    print("ROW", r)
