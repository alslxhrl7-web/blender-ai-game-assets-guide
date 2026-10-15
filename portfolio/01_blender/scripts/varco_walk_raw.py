import sys, os, glob
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from common import *
f = [x for x in glob.glob(p("varco_raw", "mixamo", "*.fbx")) if "3343f0" in x][0]
reset_scene()
bpy.ops.import_scene.fbx(filepath=f, automatic_bone_orientation=False, ignore_leaf_bones=True)
bpy.ops.wm.save_as_mainfile(filepath=p("blender", "varco_walk_raw.blend"))
