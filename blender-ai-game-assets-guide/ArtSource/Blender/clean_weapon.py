# 바르코3D에서 받은 무기 모델을 게임에 넣을 수 있게 정리한다.
# 쓰는 법: blender -b --python clean_weapon.py -- <받은 glb> <이름> <실제 길이 m> <총구가 향한 축: +X/-X/+Y/-Y> <손잡이 위치: 개머리판 쪽에서 0~1> <손잡이 높이: 위에서 0~1> <작업 폴더>
# 하는 일: 총구를 -Y(블렌더의 앞)로 돌리고, 실제 길이에 맞추고, 중심점을 손잡이로 옮기고, 겹친 점을 합치고, 이름을 정리한 뒤
#          .blend 로 저장하고 FBX 와 확인용 그림을 내보낸다.
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

src, name, length, muzzle, grip_along, grip_down, root = sys.argv[sys.argv.index("--") + 1:][:7]
length, grip_along, grip_down = float(length), float(grip_along), float(grip_down)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in bpy.context.scene.objects: o.select_set(o.type == 'MESH')
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
for o in [o for o in bpy.context.scene.objects if o != obj]: bpy.data.objects.remove(o)
obj.parent = None

# 1) 총구를 -Y 로
turn = {"+X": -90, "-X": 90, "+Y": 180, "-Y": 0}[muzzle]
obj.matrix_world = Matrix.Rotation(math.radians(turn), 4, 'Z') @ obj.matrix_world
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def bounds():
    pts = [obj.matrix_world @ v.co for v in obj.data.vertices]  # bound_box 는 메시를 고친 직후에는 옛 값이라 점에서 직접 잰다
    return Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))), Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))

# 2) 실제 길이에 맞춘다
mn, mx = bounds()
factor = length / (mx.y - mn.y)
obj.scale = (factor, factor, factor)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

# 3) 중심점을 손잡이(방아쇠) 자리로. 개머리판이 +Y, 총구가 -Y 다.
mn, mx = bounds()
pivot = Vector(((mn.x + mx.x) / 2, mx.y - (mx.y - mn.y) * grip_along, mx.z - (mx.z - mn.z) * grip_down))
obj.data.transform(Matrix.Translation(-pivot))
obj.location = (0, 0, 0)

# 4) 겹친 점 합치기, 면 방향 정리
bm = bmesh.new(); bm.from_mesh(obj.data)
before = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00005)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
islands = 0
seen = set()
for v in bm.verts:
    if v in seen: continue
    islands += 1
    stack = [v]
    while stack:
        a = stack.pop()
        if a in seen: continue
        seen.add(a)
        stack.extend(e.other_vert(a) for e in a.link_edges)
tris = sum(len(f.verts) - 2 for f in bm.faces)
after = len(bm.verts)
bm.to_mesh(obj.data); bm.free()
for p in obj.data.polygons: p.use_smooth = True

# 5) 이름
obj.name = name; obj.data.name = name
for i, m in enumerate(obj.data.materials): m.name = f"MAT_{name}" if i == 0 else f"MAT_{name}_{i}"
for i, img in enumerate([i for i in bpy.data.images if i.size[0] > 0]): img.name = f"T_{name}_{i}"

mn, mx = bounds()
print("REPORT", name, "verts", before, "->", after, "tris", tris, "islands", islands, "size(m) 폭/길이/높이", tuple(round(v, 3) for v in (mx - mn)), "pivot_to_muzzle", round(-mn.y, 3), "pivot_to_butt", round(mx.y, 3))

# 6) 저장과 내보내기
blend = os.path.join(root, "Blender", name + ".blend")
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=blend)
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
# FBX 에는 형태만 넣고, 텍스처는 따로 가볍게(JPG) 내보낸다. 저장소가 무거워지지 않게 하려는 것이다.
# 금속·거칠기 지도는 내보내지 않는다(유니티에서 값 하나로 대신한다).
bpy.ops.export_scene.fbx(filepath=os.path.join(root, "Export", name + ".fbx"), use_selection=True, object_types={'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')

def image_into(socket):
    # 소켓에 이어진 노드를 거슬러 올라가 이미지 텍스처를 찾는다
    todo = [l.from_node for l in socket.links]
    while todo:
        n = todo.pop()
        if n.type == 'TEX_IMAGE' and n.image: return n.image
        todo.extend(l.from_node for i in n.inputs for l in i.links)
    return None

mat = obj.data.materials[0]
bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
for label, socket, px, quality in (("BaseColor", bsdf.inputs["Base Color"], 2048, 88), ("Normal", bsdf.inputs["Normal"], 1024, 90)):
    img = image_into(socket)
    if img is None: print("REPORT", name, "텍스처 없음", label); continue
    copy = img.copy()
    copy.scale(px, px)
    path = os.path.join(root, "Export", f"T_{name}_{label}.jpg")
    copy.file_format = "JPEG"
    copy.save(filepath=path, quality=quality)  # 색 변환 없이 그대로 저장한다(법선 지도가 틀어지지 않게)
    bpy.data.images.remove(copy)
    print("REPORT", name, label, f"{px}px", round(os.path.getsize(path) / 1024), "KB")

# 7) 확인용 그림: 옆(왼쪽에서), 위, 비스듬히. 빨간 구슬이 중심점이다.
scene = bpy.context.scene
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.012, location=(0, 0, 0))
dot = bpy.context.view_layer.objects.active
red = bpy.data.materials.new("pivot"); red.diffuse_color = (1, 0, 0, 1); dot.data.materials.append(red)
center = (mn + mx) / 2; size = max(mx - mn)
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'TEXTURE'
scene.render.resolution_x, scene.render.resolution_y = 1200, 700
scene.render.film_transparent = False
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = size * 1.15
cam = bpy.data.objects.new("cam", cd); scene.collection.objects.link(cam); scene.camera = cam
for label, d in {"side": Vector((-1, 0, 0)), "top": Vector((0, 0, 1)), "persp": Vector((-0.8, -1, 0.5)).normalized()}.items():
    cam.location = center + d * size * 3
    cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(root, "Preview", f"{name}_{label}.png")
    bpy.ops.render.render(write_still=True)
