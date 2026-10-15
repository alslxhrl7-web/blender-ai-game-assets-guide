import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector
from common import *

def islands(bm):
    bm.verts.ensure_lookup_table()
    seen = set(); out = []
    for v in bm.verts:
        if v.index in seen: continue
        comp = []; stack = [v]; seen.add(v.index)
        while stack:
            c = stack.pop(); comp.append(c)
            for e in c.link_edges:
                w = e.other_vert(c)
                if w.index not in seen:
                    seen.add(w.index); stack.append(w)
        out.append(comp)
    return out

for key in RES:
    bpy.ops.wm.open_mainfile(filepath=p("blender", f"{key}_01_spec.blend"))
    obj = [o for o in bpy.data.objects if o.type == 'MESH'][0]
    bm = bmesh.new(); bm.from_mesh(obj.data)
    n0 = len(bm.verts)
    raw_isl = len(islands(bm))
    for dist in (0.0001,):
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=dist)
    isl = islands(bm)
    total = len(bm.verts)
    isl.sort(key=len, reverse=True)
    small = [c for c in isl if len(c) < total * 0.01]
    print(f"ISL {key}: verts {n0}->{total}, islands raw {raw_isl} -> merged {len(isl)}, <1%: {len(small)}, biggest {[len(c) for c in isl[:8]]}")
    for c in small[:40]:
        co = [v.co for v in c]
        mn = Vector((min(v.x for v in co), min(v.y for v in co), min(v.z for v in co)))
        mx = Vector((max(v.x for v in co), max(v.y for v in co), max(v.z for v in co)))
        cen = (mn + mx) / 2
        print(f"ISL   n={len(c)} center=({cen.x:.3f},{cen.y:.3f},{cen.z:.3f}) size=({mx.x-mn.x:.3f},{mx.y-mn.y:.3f},{mx.z-mn.z:.3f})")
    bm.free()
