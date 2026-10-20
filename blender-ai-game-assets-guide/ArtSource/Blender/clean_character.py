# 바르코3D에서 받은 인물 모델의 외형을 정리한다(뼈대·애니메이션은 넣지 않는다).
# 쓰는 법: blender -b --python clean_character.py -- <받은 glb> <이름> <키 m, 모자·철모 포함> <작업 폴더>
# 하는 일: 똑바로 세우고(위 +Z), 정면이 블렌더의 앞(-Y)을 보게 하고, 키를 맞추고, 중심점을 두 발 사이 바닥에 두고,
#          겹친 점을 합치고, 이름을 정리한 뒤 .blend 로 저장하고 확인용 그림(앞·옆·뒤)을 뽑는다.
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
src, name, height, root = args[0], args[1], float(args[2]), args[3]
turn = float(args[4]) if len(args) > 4 else 0.0   # 정면이 -Y 를 보지 않을 때 Z 축으로 돌릴 각도

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in bpy.context.scene.objects: o.select_set(o.type == 'MESH')
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
for o in [o for o in bpy.context.scene.objects if o != obj]: bpy.data.objects.remove(o)
obj.parent = None
if turn: obj.matrix_world = Matrix.Rotation(math.radians(turn), 4, 'Z') @ obj.matrix_world
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def bounds():
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    return Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))), Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))

mn, mx = bounds()
raw = tuple(round(v, 3) for v in (mx - mn))
f = height / (mx.z - mn.z)
obj.scale = (f, f, f)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
mn, mx = bounds()
obj.data.transform(Matrix.Translation(-Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))))
obj.location = (0, 0, 0)

bm = bmesh.new(); bm.from_mesh(obj.data)
before = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00005)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
after = len(bm.verts); tris = sum(len(fc.verts) - 2 for fc in bm.faces)
bm.to_mesh(obj.data); bm.free()
for p in obj.data.polygons: p.use_smooth = True

obj.name = name; obj.data.name = name
for i, m in enumerate(obj.data.materials): m.name = f"MAT_{name}"
for i, img in enumerate([i for i in bpy.data.images if i.size[0] > 0]): img.name = f"T_{name}_{i}"
mn, mx = bounds()
print("REPORT", name, "raw_size", raw, "verts", before, "->", after, "tris", tris, "size(m) 폭/두께/키", tuple(round(v, 3) for v in (mx - mn)))

bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root, "Blender", name + ".blend"))

scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'TEXTURE'
scene.render.resolution_x, scene.render.resolution_y = 700, 1100
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = height * 1.12
cam = bpy.data.objects.new("cam", cd); scene.collection.objects.link(cam); scene.camera = cam
center = Vector((0, 0, height / 2))
for label, d in {"front": Vector((0, -1, 0)), "side": Vector((1, 0, 0)), "back": Vector((0, 1, 0))}.items():
    cam.location = center + d * 6
    cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(root, "Preview", f"{name}_{label}.png")
    bpy.ops.render.render(write_still=True)
