import sys, os, glob, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import *

for f in sorted(glob.glob(p("varco_raw", "mixamo", "*.fbx"))):
    reset_scene()
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=f, automatic_bone_orientation=False, ignore_leaf_bones=True)
    objs = [o for o in bpy.data.objects if o not in before]
    print("FILE", os.path.basename(f))
    for o in objs:
        print("  OBJ", o.type, o.name, "parent", o.parent.name if o.parent else None, "scale", tuple(round(s, 4) for s in o.scale),
              "rot", tuple(round(r, 3) for r in o.rotation_euler), "dims", tuple(round(d, 3) for d in o.dimensions),
              ("tris %d" % tris(o)) if o.type == 'MESH' else "")
    arm = next(o for o in objs if o.type == 'ARMATURE')
    print("  BONES", len(arm.data.bones), [b.name for b in arm.data.bones][:30])
    for a in bpy.data.actions:
        print("  ACTION", a.name, "range", tuple(a.frame_range))
    # 다리 움직임 크기: 허벅지 본 회전의 프레임별 변화
    a = arm.animation_data.action if arm.animation_data else None
    if a:
        f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
        legs = [b for b in arm.pose.bones if any(k in b.name.lower() for k in ("upleg", "thigh", "upperleg"))]
        q = {}
        for fr in range(f0, f1 + 1):
            bpy.context.scene.frame_set(fr)
            for b in legs:
                q.setdefault(b.name, []).append(b.matrix.to_quaternion())
        for n, qs in q.items():
            mx = max(math.degrees(qs[0].rotation_difference(x).angle) for x in qs)
            print("  LEG", n, "max swing deg", round(mx, 1))
        bpy.context.scene.frame_set(f0)
        root = arm.pose.bones[0]
        p0 = (arm.matrix_world @ root.head).copy()
        bpy.context.scene.frame_set(f1)
        print("  ROOT", root.name, "moved", round(((arm.matrix_world @ root.head) - p0).length, 3))
