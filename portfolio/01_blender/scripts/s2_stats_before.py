"""2단계: 원본 FBX를 각각 새 씬으로 불러와 정리 전 수치를 stats_before.csv로 저장하고, 캡처용 .blend를 남긴다."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import *

rows = []
for key in RES:
    reset_scene()
    fbx = p(RES[key][1])
    objs = import_fbx(fbx)
    for o in objs:
        if o.type == 'MESH':
            o.name = {"seokgu": "Seokgu_raw", "garand": "M1Garand_raw", "sandbag": "Sandbag_raw"}[key]
    rows += stats_rows(key, objs, fbx)
    bpy.ops.wm.save_as_mainfile(filepath=p("blender", f"{key}_00_raw.blend"))

write_csv(p("stats_before.csv"), rows)
for r in rows:
    print("ROW", r)
