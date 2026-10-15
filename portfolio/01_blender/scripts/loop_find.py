import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import *
bpy.ops.wm.open_mainfile(filepath=p("blender", "seokgu_05_rigged.blend"))
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
for tr in arm.animation_data.nla_tracks: tr.mute = True
for name in ("Walk", "Idle"):
    a = bpy.data.actions[name]
    arm.animation_data.action = a
    if a.slots: arm.animation_data.action_slot = a.slots[0]
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
    def pose(fr):
        bpy.context.scene.frame_set(fr)
        return {b.name: b.matrix.to_quaternion() for b in arm.pose.bones}
    q0 = pose(f0)
    res = []
    for fr in range(f0 + 10, f1 + 1):
        q = pose(fr)
        ds = [min(d, 360 - d) for d in (math.degrees(q0[n].rotation_difference(q[n]).angle) for n in q0)]
        res.append((max(ds), sum(ds) / len(ds), fr))
    best = sorted(res)[:3]
    print("LOOPFIND", name, "range", f0, f1, "best(max,mean,frame)", [(round(m, 2), round(me, 2), fr) for m, me, fr in best])

# Walk: 처음·끝이 맞물리는 구간(길이 20프레임 이상) 찾기
a = bpy.data.actions["Walk"]; arm.animation_data.action = a
if a.slots: arm.animation_data.action_slot = a.slots[0]
f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
P = {}
for fr in range(f0, f1 + 1):
    bpy.context.scene.frame_set(fr)
    P[fr] = {b.name: b.matrix.to_quaternion() for b in arm.pose.bones}
best = []
for s in range(f0, f1 - 20):
    for e in range(s + 20, f1 + 1):
        ds = [min(d, 360 - d) for d in (math.degrees(P[s][n].rotation_difference(P[e][n]).angle) for n in P[s])]
        best.append((max(ds), sum(ds) / len(ds), s, e))
best.sort()
print("SEGMENT", [(round(m, 2), round(me, 2), s, e) for m, me, s, e in best[:5]])
