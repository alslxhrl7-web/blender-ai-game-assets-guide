# 바르코3D에서 받은 소품·차량 모델을 게임용으로 정리한다(뼈대 없음).
# 쓰는 법: blender -b --python clean_prop.py -- <받은 glb> <이름> <실제 길이 m(가장 긴 수평 변)> <Z축 회전 도> <목표 삼각형> <작업 폴더>
# 하는 일: 합치기 → 정면이 블렌더 앞(-Y, 유니티 +Z)을 보게 돌리기 → 실제 길이로 맞추기 → 원점을 바닥 가운데로
#          → 겹친 점 합치기 → 목표 삼각형으로 줄이기(Decimate) → FBX(형태만) + 텍스처 JPG(색 2K·법선 1K) → 확인 그림
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index("--") + 1:]
src, name, length, turn, target, root = a[0], a[1], float(a[2]), float(a[3]), int(a[4]), a[5]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in bpy.context.scene.objects: o.select_set(o.type == 'MESH')
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
for o in [o for o in bpy.context.scene.objects if o != obj]: bpy.data.objects.remove(o)
obj.parent = None
obj.matrix_world = Matrix.Rotation(math.radians(turn), 4, 'Z') @ obj.matrix_world
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def bounds():
    pts = [v.co for v in obj.data.vertices]
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])

def tris():
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)

mn, mx = bounds()
raw = tuple(round(v, 3) for v in (mx - mn)); tris0 = tris()
# 길이가 음수면 높이로 맞춘다(나무·전봇대·지게처럼 세로로 긴 것)
f = length / max(mx.x - mn.x, mx.y - mn.y) if length > 0 else -length / (mx.z - mn.z)
obj.data.transform(Matrix.Scale(f, 4))
mn, mx = bounds()
obj.data.transform(Matrix.Translation(-Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))))

bm = bmesh.new(); bm.from_mesh(obj.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(obj.data); bm.free()
if tris() > target:
    d = obj.modifiers.new("Decimate", 'DECIMATE'); d.ratio = target / tris(); d.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=d.name)
for p in obj.data.polygons: p.use_smooth = True
obj.name = obj.data.name = name
mn, mx = bounds()
print("REPORT", name, "raw", raw, "tris", tris0, "->", tris(), "size(m) 폭X/길이Y/높이Z", tuple(round(v, 3) for v in (mx - mn)))

os.makedirs(os.path.join(root, "Export"), exist_ok=True)
os.makedirs(os.path.join(root, "Preview"), exist_ok=True)
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root, "Blender", name + ".blend"))
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
# 정면 -Y, 위 +Z 그대로 유니티(+Z 앞, +Y 위)로 가도록 변환을 적용해 내보낸다(1차 과제와 같은 규격).
bpy.ops.export_scene.fbx(filepath=os.path.join(root, "Export", name + ".fbx"), use_selection=True, object_types={'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                         path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')

def image_into(socket):
    todo = [l.from_node for l in socket.links]
    while todo:
        n = todo.pop()
        if n.type == 'TEX_IMAGE' and n.image: return n.image
        todo.extend(l.from_node for i in n.inputs for l in i.links)
    return None

bsdf = next(n for n in obj.data.materials[0].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
for label, socket, px in (("BaseColor", bsdf.inputs["Base Color"], 2048), ("Normal", bsdf.inputs["Normal"], 1024)):
    img = image_into(socket)
    if img is None: print("REPORT", name, "텍스처 없음", label); continue
    c = img.copy(); c.scale(px, px)
    path = os.path.join(root, "Export", f"T_{name}_{label}.jpg")
    c.file_format = "JPEG"; c.save(filepath=path, quality=88); bpy.data.images.remove(c)
    print("REPORT", name, label, round(os.path.getsize(path) / 1024), "KB")

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 1000, 700
size = max(mx - mn); center = (mn + mx) / 2
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = size * 1.2
cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
for label, d in {"front": Vector((0, -1, 0)), "side": Vector((-1, 0, 0)), "persp": Vector((-0.8, -1, 0.6)).normalized()}.items():
    cam.location = center + d * size * 3
    cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(root, "Preview", f"{name}_{label}.png")
    bpy.ops.render.render(write_still=True)
