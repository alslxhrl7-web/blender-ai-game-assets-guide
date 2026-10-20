# 바르코3D 사격 동작(Fire)은 쏠 때마다 두 발이 땅에서 떨어져 뛰는 것처럼 보인다.
# 하체(Root·Hips 위치·다리)를 첫 프레임 자세에 고정하고, 상체·팔의 반동만 남긴다. rig_allies.py 보다 먼저 돌린다.
# 쓰는 법: blender -b Enemy_NK.blend --python fix_fire.py -- <ArtSource 폴더>
#   → Enemy_NK.blend 를 고쳐 저장하고, 확인 그림(고치기 전·후 옆모습 8프레임)을 Preview 에 남긴다.
import bpy, sys, os
from mathutils import Vector

root = sys.argv[sys.argv.index("--") + 1:][0]
LOCK = ("Root", "Hips", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot")

def fcurves(action):
    if hasattr(action, "fcurves") and len(action.fcurves):
        return list(action.fcurves)
    out = []
    for layer in getattr(action, "layers", []):
        for strip in layer.strips:
            for bag in strip.channelbags:
                out.extend(bag.fcurves)
    return out

rig = bpy.data.objects["Enemy_NK_Rig"]
act = bpy.data.actions["Fire"]
f0 = act.frame_range[0]
changed = 0
for fc in fcurves(act):
    path = fc.data_path
    if not path.startswith('pose.bones["'): continue
    bone = path.split('"')[1]
    if bone not in LOCK: continue
    v0 = fc.evaluate(f0)
    for k in fc.keyframe_points:
        k.co.y = v0; k.handle_left.y = v0; k.handle_right.y = v0
    fc.update()
    changed += 1
print("REPORT Fire locked fcurves", changed, "of", len(fcurves(act)))

# 확인 그림: Fire 만 켜고 옆에서 8프레임
tracks = rig.animation_data.nla_tracks
for t in tracks: t.mute = t.name != "Fire"
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'
sc.render.resolution_x, sc.render.resolution_y = 400, 500
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = 2.2
cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
mesh = bpy.data.objects["Enemy_NK"]
c = Vector((0, 0, 0.85))
cam.location = c + Vector((5, 0, 0)); cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
a, b = act.frame_range
for i in range(8):
    sc.frame_set(int(a + (b - a) * i / 7))
    sc.render.filepath = os.path.join(root, "Preview", f"FireFix_{i}.png")
    bpy.ops.render.render(write_still=True)
for t in tracks: t.mute = False
bpy.ops.wm.save_mainfile()
