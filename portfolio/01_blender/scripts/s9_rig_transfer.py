"""7·9단계 (Mixamo 대체): 바르코3D Rig+Animate FBX의 뼈대·동작을 블렌더 정리 메시(seokgu_04_final)에 옮겨 붙인다.
입력: varco_raw/mixamo/ 안의 FBX 2개 (이름에 idle / walk가 들어 있으면 그 이름을, 없으면 프레임 수로 긴 쪽 Idle, 짧은 쪽 Walk)
1) 첫 FBX에서 아마추어+메시를 가져와, 바르코 메시 바운딩 박스를 정리 메시에 맞춰 아마추어를 같은 배율·위치로 옮긴다
2) 아마추어 스케일을 적용하고, 위치 키프레임도 같은 배율로 곱해 자세가 깨지지 않게 한다
3) 정리 메시(몸·철모)에 Data Transfer로 바르코 메시의 정점 웨이트를 옮기고 아마추어에 붙인다 (철모는 Head 본에 고정)
4) 두 번째 FBX는 액션만 가져오고 아마추어·메시는 지운다
5) 액션 이름 Idle / Walk, Fake User, NLA 트랙, 첫·끝 프레임 본 회전 차이 출력
6) export/Hero_Seokgu.fbx 내보내기 (Armature + Mesh, Bake Animation, NLA Strips)
"""
import sys, os, glob, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import numpy as np
from mathutils import Vector, Matrix
from common import *

files = sorted(glob.glob(p("varco_raw", "mixamo", "*.fbx")))
assert len(files) >= 2, f"FBX 2개가 필요하다: {files}"
print("INPUT", files)

bpy.ops.wm.open_mainfile(filepath=p("blender", "seokgu_04_final.blend"))
body = bpy.data.objects["Seokgu"]
helmet = bpy.data.objects.get("Seokgu_Helmet")
clean_meshes = [o for o in (body, helmet) if o]


def bbox_np(objs, use_rest=True):
    pts = []
    for o in objs:
        n = len(o.data.vertices)
        co = np.empty(n * 3); o.data.vertices.foreach_get("co", co)
        co = co.reshape(n, 3)
        m = np.array(o.matrix_world)
        pts.append(co @ m[:3, :3].T + m[:3, 3])
    a = np.vstack(pts)
    return a.min(0), a.max(0)


def import_set(path):
    before = set(bpy.data.objects); acts_before = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False, ignore_leaf_bones=True)
    new = [o for o in bpy.data.objects if o not in before]
    acts = [a for a in bpy.data.actions if a not in acts_before]
    return new, acts


# ---- 1) 첫 FBX: 아마추어 + 바르코 메시
objs1, acts1 = import_set(files[0])
arm = next(o for o in objs1 if o.type == 'ARMATURE')
vmesh = [o for o in objs1 if o.type == 'MESH']
print("ARM", arm.name, "scale", tuple(round(s, 4) for s in arm.scale), "rot", tuple(round(r, 3) for r in arm.rotation_euler),
      "bones", len(arm.data.bones), "meshes", [m.name for m in vmesh])

# 바르코 메시의 정지 자세 바운딩 박스 (아마추어 모디파이어 영향 없이 데이터 좌표)
vmn, vmx = bbox_np(vmesh)
cmn, cmx = bbox_np(clean_meshes)
s = (cmx[2] - cmn[2]) / (vmx[2] - vmn[2])
vc = Vector(((vmn[0] + vmx[0]) / 2, (vmn[1] + vmx[1]) / 2, vmn[2]))
cc = Vector(((cmn[0] + cmx[0]) / 2, (cmn[1] + cmx[1]) / 2, cmn[2]))
print("MATCH scale", round(s, 4), "varco size", np.round(vmx - vmn, 3), "clean size", np.round(cmx - cmn, 3))
# 루트들(아마추어와 부모 없는 바르코 메시)을 같이 변환
roots = [o for o in objs1 if o.parent is None]
M = Matrix.Translation(cc) @ Matrix.Scale(s, 4) @ Matrix.Translation(-vc)
for o in roots:
    o.matrix_world = M @ o.matrix_world
bpy.context.view_layer.update()

# ---- 2) 스케일·회전 적용 (Rotation 0 / Scale 1) + 위치 키프레임 보정
arm_scale = arm.matrix_world.to_scale()[0]
bpy.ops.object.select_all(action='DESELECT')
for o in roots:
    o.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)



def all_fcurves(action):
    # Blender 5.x 슬롯 액션과 예전 액션 모두 지원
    if hasattr(action, "layers") and action.layers:
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    yield from bag.fcurves
    elif hasattr(action, "fcurves"):
        yield from action.fcurves


def scale_locs(action, k):
    for fc in all_fcurves(action):
        if fc.data_path.endswith(".location"):
            for kp in fc.keyframe_points:
                kp.co[1] *= k; kp.handle_left[1] *= k; kp.handle_right[1] *= k


for a in acts1:
    scale_locs(a, arm_scale)
print("APPLIED armature scale", round(arm_scale, 4), "→ 1.0, 위치 키 ×", round(arm_scale, 4))

# ---- 3) 웨이트 옮기기
src = vmesh[0] if len(vmesh) == 1 else max(vmesh, key=lambda o: len(o.data.vertices))
# 원본 메시가 동작 첫 자세(걷기 보폭)로 변형된 채면 다리 웨이트가 뒤섞인다 → 웨이트를 옮기는 동안 기본 자세로
arm.data.pose_position = 'REST'
bpy.context.view_layer.update()
for cm in clean_meshes:
    cm.vertex_groups.clear()
    for vg in src.vertex_groups:
        cm.vertex_groups.new(name=vg.name)
    dt = cm.modifiers.new("WeightTransfer", 'DATA_TRANSFER')
    dt.object = src
    dt.use_vert_data = True
    dt.data_types_verts = {'VGROUP_WEIGHTS'}
    dt.vert_mapping = 'POLYINTERP_NEAREST'
    dt.layers_vgroup_select_src = 'ALL'
    dt.layers_vgroup_select_dst = 'NAME'
    bpy.context.view_layer.objects.active = cm
    bpy.ops.object.modifier_apply(modifier=dt.name)

arm.data.pose_position = 'POSE'

# 철모는 머리 본 하나에 100%로 (흔들리거나 늘어나지 않게)
head = next((b.name for b in arm.data.bones if "head" in b.name.lower() and "end" not in b.name.lower()), None) or next((b.name for b in arm.data.bones if "neck" in b.name.lower()), None)  # 바르코 리그는 Head 본이 없어 Neck에 고정
if helmet and head:
    helmet.vertex_groups.clear()
    g = helmet.vertex_groups.new(name=head)
    g.add(list(range(len(helmet.data.vertices))), 1.0, 'REPLACE')
print("HEAD BONE", head)

for cm in clean_meshes:
    cm.parent = arm
    cm.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = cm.modifiers.new("Armature", 'ARMATURE'); mod.object = arm

# 바르코 메시는 지운다
for o in vmesh:
    bpy.data.objects.remove(o, do_unlink=True)

# ---- 4) 두 번째 FBX: 액션만
objs2, acts2 = import_set(files[1])
for o in objs2:
    bpy.data.objects.remove(o, do_unlink=True)
arm2_scale = arm_scale  # 같은 바르코 리그라 배율이 같다
for a in acts2:
    scale_locs(a, arm2_scale)

# ---- 5) 이름 정리
def frames(a):
    return a.frame_range[1] - a.frame_range[0]

pairs = [(files[0], acts1), (files[1], acts2)]
named = {}
for f, acts in pairs:
    a = max(acts, key=frames)  # 파일마다 가장 긴 액션 하나
    low = os.path.basename(f).lower()
    if "idle" in low:
        named["Idle"] = a
    elif "walk" in low:
        named["Walk"] = a
    else:
        named[f] = a
rest = [v for k, v in named.items() if k not in ("Idle", "Walk")]
if rest:
    rest.sort(key=frames)
    if "Walk" not in named: named["Walk"] = rest.pop(0)
    if "Idle" not in named and rest: named["Idle"] = rest.pop(0)
for k in list(named):
    if k not in ("Idle", "Walk"): named.pop(k)
for name, a in named.items():
    a.name = name
    a.use_fake_user = True
# 남는 액션 지우기
for a in list(bpy.data.actions):
    if a.name not in ("Idle", "Walk"):
        bpy.data.actions.remove(a)

# NLA 트랙
arm.animation_data_create()
arm.animation_data.action = None
for name in ("Idle", "Walk"):
    a = bpy.data.actions[name]
    tr = arm.animation_data.nla_tracks.new(); tr.name = name
    st = tr.strips.new(name, int(a.frame_range[0]), a)
    if hasattr(a, "slots") and a.slots:
        try: st.action_slot = a.slots[0]
        except Exception: pass

# 첫·끝 프레임 자세 비교 (본별 회전 차이, 도)
report = {}
for name in ("Idle", "Walk"):
    a = bpy.data.actions[name]
    arm.animation_data.action = a
    if hasattr(a, "slots") and a.slots:
        try: arm.animation_data.action_slot = a.slots[0]
        except Exception: pass
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    bpy.context.scene.frame_set(f0)
    q0 = {b.name: b.matrix.to_quaternion() for b in arm.pose.bones}
    bpy.context.scene.frame_set(f1)
    diffs = sorted(((min(d, 360 - d), n) for d, n in ((math.degrees(q0[b.name].rotation_difference(b.matrix.to_quaternion()).angle), b.name) for b in arm.pose.bones)), reverse=True)
    report[name] = {"frames": f1 - f0 + 1, "max_deg": round(diffs[0][0], 2), "max_bone": diffs[0][1],
                    "mean_deg": round(sum(d for d, _ in diffs) / len(diffs), 2)}
    print("LOOP", name, report[name])
arm.animation_data.action = None
bpy.context.scene.frame_start = 1
bpy.context.scene.frame_end = max(r["frames"] for r in report.values())
bpy.context.scene.render.fps = 30

print("ARM final scale", tuple(round(x, 4) for x in arm.scale), "rot", tuple(round(x, 4) for x in arm.rotation_euler))
bpy.ops.wm.save_as_mainfile(filepath=p("blender", "seokgu_05_rigged.blend"))

# ---- 6) 내보내기
bpy.ops.object.select_all(action='DESELECT')
arm.select_set(True)
for cm in clean_meshes: cm.select_set(True)
out = p("export", "Hero_Seokgu.fbx")
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={'ARMATURE', 'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, path_mode='COPY', embed_textures=True,
                         add_leaf_bones=False, bake_anim=True, bake_anim_use_nla_strips=True,
                         bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True)
print("EXPORT", out, round(os.path.getsize(out) / 1048576, 2), "MB")
