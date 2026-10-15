import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import *

for key in RES:
    reset_scene()
    objs = import_fbx(p(RES[key][1]))
    print("=====", key)
    for o in objs:
        extra = ""
        if o.type == 'MESH':
            extra = f"verts={len(o.data.vertices)} tris={tris(o)} mats={[s.material.name for s in o.material_slots if s.material]}"
        print(" ", o.type, o.name, "parent=", o.parent.name if o.parent else None,
              "loc", tuple(round(c, 3) for c in o.location), "rot", tuple(round(c, 3) for c in o.rotation_euler),
              "scale", tuple(round(c, 4) for c in o.scale), "dims", tuple(round(c, 3) for c in o.dimensions), extra)
    print("  textures:", textures([o for o in objs if o.type == 'MESH']))
    for img in bpy.data.images:
        print("  img", img.name, img.filepath, img.size[:], img.packed_file is not None)
