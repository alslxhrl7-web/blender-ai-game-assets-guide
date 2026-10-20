# 바르코3D Rig·Animate 로 받은 북한군 소총수 FBX 여러 개를 하나로 묶는다.
# 쓰는 법: blender -b --python pack_enemy.py -- <작업 폴더> <키 m> <목표 삼각형> <동작이름=fbx> ...
# 하는 일: 첫 파일의 메시·뼈대만 남기고, 나머지 파일의 동작(Action)을 이름을 붙여 모은다 → 키 맞추기 → 면 수 줄이기(가중치 유지)
#          → FBX 하나(뼈대+메시+동작 전부, 형태만) + 텍스처 JPG
import bpy, sys, os
a = sys.argv[sys.argv.index("--") + 1:]
root, height, target, pairs = a[0], float(a[1]), int(a[2]), [p.split("=", 1) for p in a[3:]]
name = "Enemy_NK"

bpy.ops.wm.read_factory_settings(use_empty=True)
arm = mesh = None
for i, (clip, path) in enumerate(pairs):
    before = set(bpy.data.objects); acts = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=path, ignore_leaf_bones=True)
    new = [o for o in bpy.data.objects if o not in before]
    act = next(iter(set(bpy.data.actions) - acts))
    act.name = clip; act.use_fake_user = True
    f0, f1 = act.frame_range
    print("REPORT clip", clip, "frames", int(f0), int(f1), "fcurves", len(act.fcurves) if hasattr(act, "fcurves") else "?")
    if i == 0:
        arm = next(o for o in new if o.type == 'ARMATURE'); mesh = next(o for o in new if o.type == 'MESH')
    else:
        for o in new: bpy.data.objects.remove(o)

# 동작을 NLA 트랙으로 하나씩 올린다(유니티에서 클립으로 나뉜다).
arm.animation_data_create()
for clip, _ in pairs:
    tr = arm.animation_data.nla_tracks.new(); tr.name = clip
    act = bpy.data.actions[clip]
    tr.strips.new(clip, int(act.frame_range[0]), act)
arm.animation_data.action = None

# 키: 첫 프레임(뼈대 기본 자세) 메시 높이로 잰다.
pts = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
h = max(p.z for p in pts) - min(p.z for p in pts)
arm.scale = arm.scale * (height / h)
tris0 = sum(len(p.vertices) - 2 for p in mesh.data.polygons)
d = mesh.modifiers.new("Dec", 'DECIMATE'); d.ratio = target / tris0; d.use_collapse_triangulate = True
with bpy.context.temp_override(object=mesh, active_object=mesh):
    bpy.ops.object.modifier_move_to_index(modifier="Dec", index=0)
    bpy.ops.object.modifier_apply(modifier="Dec")
mesh.name = name; arm.name = name + "_Rig"
print("REPORT", name, "tris", tris0, "->", sum(len(p.vertices) - 2 for p in mesh.data.polygons), "bones", len(arm.data.bones), "scale", round(height / h, 4))

os.makedirs(os.path.join(root, "Export"), exist_ok=True)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root, "Blender", name + ".blend"))
bpy.ops.export_scene.fbx(filepath=os.path.join(root, "Export", name + ".fbx"), object_types={'ARMATURE', 'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True, bake_anim_force_startend_keying=True,
                         path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')

def image_into(socket):
    todo = [l.from_node for l in socket.links]
    while todo:
        n = todo.pop()
        if n.type == 'TEX_IMAGE' and n.image: return n.image
        todo.extend(l.from_node for i in n.inputs for l in i.links)
    return None
bsdf = next(n for n in mesh.data.materials[0].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
for label, socket, px in (("BaseColor", bsdf.inputs["Base Color"], 2048), ("Normal", bsdf.inputs["Normal"], 1024)):
    img = image_into(socket)
    if img is None: print("REPORT 텍스처 없음", label); continue
    c = img.copy(); c.scale(px, px); p = os.path.join(root, "Export", f"T_{name}_{label}.jpg")
    c.file_format = "JPEG"; c.save(filepath=p, quality=88); bpy.data.images.remove(c)
    print("REPORT", label, round(os.path.getsize(p) / 1024), "KB")
