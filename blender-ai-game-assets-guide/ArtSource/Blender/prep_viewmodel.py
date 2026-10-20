# 1인칭 손에 드는 총(뷰모델)을 만든다. 조준하면 가늠자 구멍과 가늠쇠가 화면 한가운데 한 줄로 서게 하는 것이 목적이다.
# 쓰는 법: blender -b --python prep_viewmodel.py -- <원본 fbx> <이름> <실제 길이 m> <가늠자 위치 0~1> <가늠쇠 위치 0~1> <ArtSource 폴더> <색 텍스처> [법선 텍스처]
#   가늠자·가늠쇠 위치는 개머리판 끝(0)에서 총구(1)까지의 비율이다. M1 개런드: 가늠자 0.30~0.42, 가늠쇠 0.88~0.99 구간에서 가장 높은 점.
# 하는 일:
#   1. 가장 긴 축을 총열로 보고, 단면이 큰 쪽을 개머리판으로 정한다. 총열(총구 쪽 단면 중심)이 개머리판 중심보다 높은 쪽이 위다.
#   2. 총구가 블렌더 -Y(유니티 +Z), 위가 +Z 가 되게 돌리고 실제 길이로 맞춘다.
#   3. 가늠자 꼭대기와 가늠쇠 꼭대기를 찾아, 두 점을 잇는 선이 수평이 되도록 총을 살짝 숙인다(조준선).
#   4. 원점을 가늠자 구멍(꼭대기에서 4mm 아래)으로 옮긴다. 유니티에서 원점을 눈앞에 두면 조준선이 화면 중앙을 지난다.
#   5. FBX(형태만) + 텍스처 JPG + 확인 그림. 가늠쇠까지 거리와 높이 차를 REPORT 로 남긴다.
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index("--") + 1:]
src, name, length, rear_lo, front_lo, root, color = a[0], a[1], float(a[2]), float(a[3]), float(a[4]), a[5], a[6]
normal = a[7] if len(a) > 7 else None
REAR = (rear_lo, rear_lo + 0.12)
FRONT = (front_lo, 0.99)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
for o in bpy.context.scene.objects: o.select_set(o.type == 'MESH')
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
for o in [o for o in bpy.context.scene.objects if o != obj]: bpy.data.objects.remove(o)
obj.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
V = lambda: [v.co.copy() for v in obj.data.vertices]

# 1. 축 정하기
pts = V()
mn = Vector([min(p[i] for p in pts) for i in range(3)]); mx = Vector([max(p[i] for p in pts) for i in range(3)])
size = mx - mn
L = max(range(3), key=lambda i: size[i])
others = [i for i in range(3) if i != L]
U = max(others, key=lambda i: size[i])   # 두 번째로 긴 축 = 위아래(개머리판 높이)
W = [i for i in others if i != U][0]
def ends(lo_is_butt):
    span = size[L]
    lo = [p for p in pts if p[L] < mn[L] + span * 0.05]
    hi = [p for p in pts if p[L] > mx[L] - span * 0.05]
    return lo, hi
lo, hi = ends(True)
ext = lambda ps: max(p[U] for p in ps) - min(p[U] for p in ps)
butt_is_lo = ext(lo) > ext(hi)
butt, muzzle = (lo, hi) if butt_is_lo else (hi, lo)
cen = lambda ps: sum((p[U] for p in ps), 0.0) / len(ps)
up_sign = 1.0 if cen(muzzle) > cen(butt) else -1.0
fwd_sign = 1.0 if not butt_is_lo else 1.0
fwd = Vector((0, 0, 0)); fwd[L] = 1.0 if butt_is_lo else -1.0          # 개머리판 → 총구
upv = Vector((0, 0, 0)); upv[U] = up_sign
side = fwd.cross(upv)
# 총구 → 블렌더 -Y, 위 → +Z
R = Matrix((Vector((0, -1, 0)).cross(Vector((0, 0, 1))), Vector((0, -1, 0)), Vector((0, 0, 1)))).transposed() @ Matrix((side, fwd, upv))
obj.data.transform(R.to_4x4())
pts = V()
mn = Vector([min(p[i] for p in pts) for i in range(3)]); mx = Vector([max(p[i] for p in pts) for i in range(3)])
f = length / (mx.y - mn.y)
obj.data.transform(Matrix.Scale(f, 4))
pts = V()
mn = Vector([min(p[i] for p in pts) for i in range(3)]); mx = Vector([max(p[i] for p in pts) for i in range(3)])
# 개머리판 끝 = +Y 끝(mx.y), 총구 = -Y 끝(mn.y)
t = lambda p: (mx.y - p.y) / (mx.y - mn.y)            # 0 = 개머리판, 1 = 총구

# 3. 가늠자·가늠쇠
def top(rng):
    y0 = min(p.y for p in pts); y1 = max(p.y for p in pts)
    c = [p for p in pts if rng[0] <= (y1 - p.y) / (y1 - y0) <= rng[1]]
    return max(c, key=lambda p: p.z)
rear, front = top(REAR), top(FRONT)

# 3-1. 가늠 장치 바꾸기: 저폴리 모델의 가늠자·가늠쇠는 덩어리라 조준하면 M1 처럼 읽히지 않는다.
#      둘 다 지우고 m1_sights.py 가 만드는 실물 형태로 바꿔 단다.
#      가늠자 = 얇은 판의 작은 구멍 + 받침 + 고도(왼쪽)·풍향(오른쪽) 휠, 가늠쇠 = 받침 + 평평한 블레이드 + 둥근 보호 날개 2개.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import m1_sights as S
y0 = min(p.y for p in pts); y1 = max(p.y for p in pts)
tt = lambda p: (y1 - p.y) / (y1 - y0)
def level(lo, hi):
    zs = sorted(p.z for p in pts if lo <= tt(p) <= hi)
    return zs[int(len(zs) * 0.9)]
recv_top = level(REAR[1], REAR[1] + 0.05)                # 가늠자 바로 앞 기관부 윗면
gas_top = level(FRONT[0] - 0.06, FRONT[0] - 0.01)        # 가늠쇠 바로 뒤 총열·가스 실린더 윗면
bm = bmesh.new(); bm.from_mesh(obj.data)
uv = bm.loops.layers.uv.active
dark_uv = None
for f in bm.faces:                                         # 기관부(검은 쇠) 윗면의 UV 하나를 가늠 장치 색으로 쓴다
    c = f.calc_center_median()
    if REAR[1] <= (y1 - c.y) / (y1 - y0) <= REAR[1] + 0.05 and abs(c.z - recv_top) < 0.01:
        dark_uv = f.loops[0][uv].uv.copy(); break
cut_r = [v for v in bm.verts if REAR[0] - 0.02 <= tt(v.co) <= REAR[1] and v.co.z > recv_top + 0.002 and abs(v.co.x) < 0.05]
cut_f = [v for v in bm.verts if FRONT[0] - 0.005 <= tt(v.co) <= FRONT[1] and v.co.z > gas_top + 0.002 and abs(v.co.x) < 0.03]
bmesh.ops.delete(bm, geom=cut_r + cut_f, context='VERTS')
# 지운 자리에 생긴 구멍을 메운다(뒷면이 보이면 조준할 때 흰 톱니처럼 보인다).
holes = [e for e in bm.edges if e.is_boundary and (REAR[0] - 0.04 <= tt(e.verts[0].co) <= REAR[1] + 0.02 or FRONT[0] - 0.02 <= tt(e.verts[0].co) <= FRONT[1] + 0.01)]
filled = bmesh.ops.holes_fill(bm, edges=holes, sides=0)
for f in filled["faces"]:
    f.smooth = False
    if dark_uv is not None:
        for l in f.loops: l[uv].uv = dark_uv
bm.to_mesh(obj.data); bm.free()
print("REPORT sights: removed rear", len(cut_r), "front", len(cut_f), "verts, filled", len(filled["faces"]), "faces; receiver top", round(recv_top, 4), "gas top", round(gas_top, 4))

# 새 가늠 장치: 원점(받침 아래면 가운데)을 각 자리의 윗면에 둔다.
rear_ob, front_ob = S.build_rear(), S.build_front()
for ob in (rear_ob, front_ob): S.bevel_and_finish(ob, S.BEVEL)
rear_ob.location = (0.0, rear.y, recv_top)
front_ob.location = (0.0, front.y, gas_top)
hole = Vector((0.0, rear.y, recv_top + S.SIGHT_LINE_Z))                  # 가늠자 구멍 가운데
blade = Vector((0.0, front.y, gas_top + S.FS_BASE_H + S.BLADE_H))         # 가늠쇠 블레이드 윗면
nf = len(obj.data.polygons)
for o in bpy.context.view_layer.objects: o.select_set(o in (obj, rear_ob, front_ob))
bpy.context.view_layer.objects.active = obj
bpy.ops.object.join()
bm = bmesh.new(); bm.from_mesh(obj.data)
uv = bm.loops.layers.uv.active
bm.faces.ensure_lookup_table()
for f in bm.faces[nf:]:                                    # 새로 붙인 가늠 장치 면: 기관부 검은 쇠 색(재질 하나로 유지)
    if dark_uv is not None:
        for l in f.loops: l[uv].uv = dark_uv
bm.to_mesh(obj.data); bm.free()
pts = V()
rear, front = hole, blade
print("REPORT sights: hole centre z", round(hole.z, 4), "blade top z", round(blade.z, 4), "sight radius", round(hole.y - blade.y, 3), "m")
ang = math.atan2(front.z - rear.z, rear.y - front.y)     # 가늠쇠가 높으면 양수
rot = Matrix.Rotation(ang, 4, 'X')                       # 총구 쪽(-Y)을 내린다
obj.data.transform(Matrix.Translation(rear) @ rot @ Matrix.Translation(-rear))
# 4. 원점 = 가늠자 구멍 가운데(돌린 축이라 그대로다)
eye = rear.copy()
obj.data.transform(Matrix.Translation(-eye))
pts = V()
print("REPORT", name, "pitch fix deg", round(math.degrees(ang), 2))
mn = Vector([min(p[i] for p in pts) for i in range(3)]); mx = Vector([max(p[i] for p in pts) for i in range(3)])
print("REPORT", name, "bounds min", tuple(round(x, 3) for x in mn), "max", tuple(round(x, 3) for x in mx), "tris", sum(len(p.vertices) - 2 for p in obj.data.polygons))

# 원래 총 면만 매끈하게(새로 만든 고리·기둥·메운 면은 위에서 평면으로 둔 그대로)
obj.name = obj.data.name = name
export = os.path.join(root, "Export"); os.makedirs(export, exist_ok=True)
bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.join(export, name + ".fbx"), use_selection=True, object_types={'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                         path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')
for label, path, px in (("BaseColor", color, 2048), ("Normal", normal, 1024)):
    if not path: continue
    img = bpy.data.images.load(path); c = img.copy(); c.scale(px, px)
    out = os.path.join(export, f"T_{name}_{label}.jpg"); c.file_format = "JPEG"; c.save(filepath=out, quality=90)
    print("REPORT", label, round(os.path.getsize(out) / 1024), "KB")

# 확인 그림: 옆모습(조준선 표시)과 눈 위치에서 본 조준 화면
mat = bpy.data.materials.new("vm"); mat.use_nodes = True
tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = bpy.data.images.load(color)
bsdf = mat.node_tree.nodes["Principled BSDF"]; mat.node_tree.links.new(tex.outputs[0], bsdf.inputs["Base Color"])
obj.data.materials.clear(); obj.data.materials.append(mat)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 1200, 500
cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
cd.type = 'ORTHO'; cd.ortho_scale = length * 1.1
cam.location = Vector((2.0, -length * 0.4, 0.0)); cam.rotation_euler = (math.radians(90), 0, math.radians(90))
sc.render.filepath = os.path.join(root, "Preview", name + "_side.png"); bpy.ops.render.render(write_still=True)
cd.type = 'PERSP'; cd.lens = 24; cd.clip_start = 0.005
sc.render.resolution_x, sc.render.resolution_y = 1200, 700
cam.location = Vector((0.0, 0.07, 0.0)); cam.rotation_euler = (math.radians(90), 0, 0)   # 눈은 가늠자 7cm 뒤, -Y(총구)를 본다
cam.rotation_euler = (Vector((0, -1, 0))).to_track_quat('-Z', 'Y').to_euler()
sc.render.filepath = os.path.join(root, "Preview", name + "_aim.png"); bpy.ops.render.render(write_still=True)
