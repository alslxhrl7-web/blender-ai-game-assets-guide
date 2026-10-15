import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from common import *

def dom(o):
    names = {g.index: g.name for g in o.vertex_groups}
    d = []
    for v in o.data.vertices:
        d.append(names[max(v.groups, key=lambda g: g.weight).group] if v.groups else None)
    return d

def side(n):
    if not n: return None
    return "L" if n.startswith("Left") else ("R" if n.startswith("Right") else "C")

for path in ("seokgu_05_rigged.blend",):
    bpy.ops.wm.open_mainfile(filepath=p("blender", path))
    o = bpy.data.objects["Seokgu"]
    d = dom(o)
    bm = bmesh.new(); bm.from_mesh(o.data)
    lens = np.array([e.calc_length() for e in bm.edges])
    print("EDGES", len(lens), "p50 %.4f p99 %.4f max %.4f" % (np.percentile(lens, 50), np.percentile(lens, 99), lens.max()), ">8cm", int((lens > 0.08).sum()))
    bridge = []
    for e in bm.edges:
        a, b = e.verts
        sa, sb = side(d[a.index]), side(d[b.index])
        if {sa, sb} == {"L", "R"}:
            bridge.append(e)
    print("BRIDGE L-R edges", len(bridge))
    for e in sorted(bridge, key=lambda e: -e.calc_length())[:10]:
        a, b = e.verts
        print("  len %.3f" % e.calc_length(), d[a.index], tuple(round(c, 3) for c in a.co), d[b.index], tuple(round(c, 3) for c in b.co))
    # 손/팔 본과 다리 본이 한 모서리로 이어진 것
    arms_legs = [e for e in bm.edges if {"arm" in (d[e.verts[0].index] or "").lower() or "forearm" in (d[e.verts[0].index] or "").lower(),
                                           "leg" in (d[e.verts[1].index] or "").lower() or "upleg" in (d[e.verts[1].index] or "").lower()} == {True}]
    print("ARM-LEG edges", len(arms_legs))
    # 긴 모서리
    for e in sorted(bm.edges, key=lambda e: -e.calc_length())[:8]:
        a, b = e.verts
        print("  LONG %.3f" % e.calc_length(), d[a.index], tuple(round(c, 3) for c in a.co), d[b.index], tuple(round(c, 3) for c in b.co))
    bm.free()
