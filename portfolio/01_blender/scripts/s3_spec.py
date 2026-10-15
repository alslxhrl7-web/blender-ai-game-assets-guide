"""3단계: 게임 규격 정리 (요청 1 스크립트를 고친 판).
- 크기: 석구는 키(Z), 소총·진지는 가로로 가장 긴 변을 기준 길이에 맞춘다
- 정면 −Y: 석구·진지는 이미 −Y. 소총은 총구가 +X라서 Z축 −90°로 돌려 총구를 −Y로
- 바닥 Z=0, 원점은 바닥 중앙(소총은 개머리판 끝 바닥 중앙)
- All Transforms 적용 → Rotation 0 / Scale 1
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector, Matrix
from common import *

ROT_Z = {"seokgu": 0.0, "garand": -90.0, "sandbag": 0.0}
table = []
for key in RES:
    bpy.ops.wm.open_mainfile(filepath=p("blender", f"{key}_00_raw.blend"))
    obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
    obj.name = {"seokgu": "Seokgu", "garand": "M1Garand", "sandbag": "SandbagPosition"}[key]
    obj.data.name = obj.name
    before = tuple(round(d, 3) for d in obj.dimensions)

    # 1) 회전
    obj.rotation_euler = (0, 0, math.radians(ROT_Z[key]))
    bpy.context.view_layer.objects.active = obj; obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # 2) 균등 스케일
    _, _, axis, length, _, _ = RES[key][1:] if False else (None, None, RES[key][2], RES[key][3], None, None)
    d = obj.dimensions
    cur = d.z if axis == "Z" else max(d.x, d.y)
    s = length / cur
    obj.scale = (s, s, s)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    # 3) 원점: 바닥 중앙. 소총은 개머리판 끝(+Y 끝) 바닥 중앙
    mn, mx = world_bbox([obj])
    if key == "garand":
        pivot = Vector(((mn.x + mx.x) / 2, mx.y, mn.z))
    else:
        pivot = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
    obj.data.transform(Matrix.Translation(-pivot))
    obj.location = (0, 0, 0)
    obj.data.update()

    after = tuple(round(x, 3) for x in obj.dimensions)
    mn, mx = world_bbox([obj])
    row = (RES[key][0], before, after, round(s, 4), tuple(round(c, 3) for c in obj.location),
           tuple(round(c, 3) for c in obj.rotation_euler), tuple(round(c, 3) for c in obj.scale),
           round(mn.z, 4))
    table.append(row)
    bpy.ops.wm.save_as_mainfile(filepath=p("blender", f"{key}_01_spec.blend"))

print("SPEC | 리소스 | 정리 전 크기(XYZ) | 정리 후 크기(XYZ, m) | 배율 | 원점 | 회전 | 스케일 | 바닥 Z")
for r in table:
    print("SPEC |", " | ".join(str(x) for x in r))
