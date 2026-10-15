import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import numpy as np
from common import *

bpy.ops.wm.open_mainfile(filepath=p("blender", "seokgu_05_rigged.blend"))
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
bones = {b.name for b in arm.data.bones}
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    names = {g.index: g.name for g in o.vertex_groups}
    tot = np.zeros(len(o.data.vertices)); nb = np.zeros(len(o.data.vertices), int)
    for v in o.data.vertices:
        for g in v.groups:
            if names[g.group] in bones:
                tot[v.index] += g.weight
                if g.weight > 0.01: nb[v.index] += 1
    co = np.array([v.co[:] for v in o.data.vertices])
    zero = tot < 0.05
    print("MESH", o.name, "verts", len(tot), "zero-weight", int(zero.sum()), "sum>1.05", int((tot > 1.05).sum()),
          "groups", len(o.vertex_groups), "non-bone groups", [n for n in names.values() if n not in bones][:5])
    if zero.any():
        z = co[zero]
        print("  ZERO z range", np.round(z[:, 2].min(), 3), np.round(z[:, 2].max(), 3), "x range", np.round(z[:, 0].min(), 3), np.round(z[:, 0].max(), 3))
        hist = np.histogram(z[:, 2], bins=[0, 0.1, 0.3, 0.6, 0.9, 1.2, 1.5, 1.8])[0]
        print("  ZERO by height", hist.tolist())
    print("  mod", [(m.type, getattr(m, 'object', None) and m.object.name) for m in o.modifiers], "parent", o.parent and o.parent.name)
