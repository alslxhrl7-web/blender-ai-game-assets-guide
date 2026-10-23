# M1 개런드 가늠 장치(앞 가늠쇠 + 뒤 가늠자)를 만들고 Unity 용 FBX 로 내보낸다.
# Blender 5.2 · Unity URP · 1 Blender 단위 = 1 m
#
# 쓰는 법
#   Blender 텍스트 편집기에서 Run Script, 또는 명령줄:
#   blender -b --python m1_sights.py
#   → 장면을 비우고 M1_FrontSight, M1_RearSight 를 만든 뒤 FBX 와 확인용 렌더 2장을 저장한다.
#
# 축 약속(이 프로젝트의 다른 총 모델과 같다)
#   Blender: 총구 쪽 = -Y, 위 = +Z, 오른쪽 = +X
#   Unity  : 총구 쪽 = +Z, 위 = +Y   (내보내기 설정으로 바뀐다. 맨 아래 export 주석 참고)
#
# 원점
#   두 오브젝트 모두 "총에 붙는 받침 아래면의 가운데"가 원점이다.
#   뒤 가늠자는 원점(0,0,0), 앞 가늠쇠는 SIGHT_RADIUS 만큼 앞(-Y)에 둔다.
#   가늠자 구멍 중심과 가늠쇠 블레이드 윗면이 같은 높이(조준선)에 오도록 앞 가늠쇠의 높이를 계산해 놓는다.

import bpy, bmesh, math, os
from mathutils import Vector, Matrix

# =====================================================================
#  조정할 값 (단위 m)
# =====================================================================
SIGHT_RADIUS = 0.565          # 가늠자 구멍 ~ 가늠쇠 사이 거리. 실제 M1 은 약 0.565 m. 총 모델에 맞춰 바꾼다.

# --- 앞 가늠쇠 (M1_FrontSight) ---
FS_BASE_W = 0.014             # 받침 블록 폭(좌우)
FS_BASE_L = 0.012             # 받침 블록 길이(앞뒤)
FS_BASE_H = 0.006             # 받침 블록 높이
BLADE_W = 0.0032              # 블레이드 폭(좌우). 조준 구멍 안에서 또렷하게 보이도록 너무 가늘게 하지 않는다
BLADE_T = 0.0045              # 블레이드 두께(앞뒤)
BLADE_H = 0.014               # 받침 위로 올라온 블레이드 높이(윗면은 평평)
WING_GAP = 0.0105             # 두 보호 날개 안쪽 면 사이 간격(블레이드가 이 사이에 선다)
WING_W = 0.0022               # 날개 판 두께(좌우). 뒤에서 보면 이 폭의 기둥으로 보인다
WING_L = 0.009                # 날개 판 길이(앞뒤)
WING_H = 0.0128               # 받침 위 날개 높이(블레이드보다 약간 낮게)
WING_ARC_SEG = 12             # 날개 윗부분 둥근 마감의 분할 수

# --- 뒤 가늠자 (M1_RearSight) ---
RS_BASE_W = 0.024             # 받침(Sight Base) 폭
RS_BASE_L = 0.020             # 받침 길이
RS_BASE_H = 0.006             # 받침 높이
PLATE_W = 0.022               # 구멍 판 폭
PLATE_H = 0.019               # 구멍 판 높이(받침 윗면부터)
PLATE_T = 0.0018              # 구멍 판 두께(앞뒤). 얇은 금속판
PLATE_TOP_R = 0.004           # 판 위쪽 두 모서리 둥근 반지름
HOLE_D = 0.0055               # 구멍 지름. 판 폭의 30% 이하(0.022 × 0.3 = 0.0066)
HOLE_Z = 0.011                # 판 아래면에서 구멍 중심까지 높이
HOLE_SEG = 24                 # 구멍 원 분할 수
KNOB_ELEV_R = 0.0075          # 고도 조절 휠 반지름(왼쪽)
KNOB_WIND_R = 0.0062          # 풍향 조절 휠 반지름(오른쪽)
KNOB_W = 0.0045               # 휠 두께(좌우)
KNOB_SEG = 14                 # 휠 둘레 분할 수(톱니처럼 보이도록 짝수)
KNOB_Z = 0.0055               # 받침 아래면에서 휠 중심 높이

BEVEL = 0.0003                # 모서리 베벨 폭(윤곽이 또렷하게 읽히는 정도)

# --- 출력 ---
OUT_DIR = r"C:\Users\mbc\Documents\user\FiveRiver\blender-ai-game-assets-guide\ArtSource"
EXPORT_PATH = os.path.join(OUT_DIR, "Export", "M1_Sights.fbx")
RENDER_AIM = os.path.join(OUT_DIR, "Preview", "M1_Sights_aim.png")
RENDER_SIDE = os.path.join(OUT_DIR, "Preview", "M1_Sights_side.png")

# 조준선 높이 = 뒤 가늠자 원점에서 구멍 중심까지
SIGHT_LINE_Z = RS_BASE_H + HOLE_Z

# =====================================================================
#  도우미
# =====================================================================
def add_box(bm, cx, cy, cz, sx, sy, sz):
    """중심(cx,cy,cz), 크기(sx,sy,sz)인 상자. 만든 면들을 돌려준다."""
    r = bmesh.ops.create_cube(bm, size=1.0)
    for v in r["verts"]:
        v.co = Vector((cx + v.co.x * sx, cy + v.co.y * sy, cz + v.co.z * sz))
    return list({f for v in r["verts"] for f in v.link_faces})

def extrude_profile_y(bm, pts_xz, y0, y1):
    """XZ 평면의 닫힌 윤곽(반시계)을 Y 방향 y0~y1 로 밀어낸 기둥. (앞면, 뒷면, 옆면 목록)을 돌려준다."""
    a = [bm.verts.new((x, y0, z)) for x, z in pts_xz]
    b = [bm.verts.new((x, y1, z)) for x, z in pts_xz]
    n = len(pts_xz)
    cap0 = bm.faces.new(list(reversed(a)))
    cap1 = bm.faces.new(b)
    sides = [bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i])) for i in range(n)]
    return cap0, cap1, sides

def make_object(name, bm, smooth_faces=()):
    me = bpy.data.meshes.new(name)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)      # 법선을 바깥으로
    sm = set(smooth_faces)
    for f in bm.faces:
        f.smooth = f in sm                                   # 평평한 면 flat, 둥근 면만 smooth
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob

def bevel_and_finish(ob, width):
    """모서리 베벨(각도 제한)을 적용하고 삼각형으로 바꿔 삼각형 수를 센다."""
    bv = ob.modifiers.new("Bevel", 'BEVEL')
    bv.width = width; bv.segments = 1; bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(50)
    bv.harden_normals = False
    tr = ob.modifiers.new("Tri", 'TRIANGULATE')
    with bpy.context.temp_override(object=ob, active_object=ob, selected_objects=[ob]):
        bpy.ops.object.modifier_apply(modifier=bv.name)
        bpy.ops.object.modifier_apply(modifier=tr.name)
    return len(ob.data.polygons)

def unwrap(ob):
    for o in bpy.context.view_layer.objects: o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')

# =====================================================================
#  1. 앞 가늠쇠
# =====================================================================
def build_front():
    """앞 가늠쇠 오브젝트를 만든다(원점 = 받침 아래면 가운데, 베벨·삼각형 전)."""
    bm = bmesh.new()
    smooth = []
    # 받침 블록(가스 실린더 홈에 끼워짐). 원점 = 받침 아래면 가운데
    add_box(bm, 0, 0, FS_BASE_H / 2, FS_BASE_W, FS_BASE_L, FS_BASE_H)
    # 블레이드: 윗면이 평평한 칼날형 기둥
    add_box(bm, 0, 0, FS_BASE_H + BLADE_H / 2, BLADE_W, BLADE_T, BLADE_H)
    # 보호 날개 2개: 위가 둥근 얇은 판. 윤곽은 뒤에서 본 모양(XZ), 앞뒤(Y)로 WING_L 만큼
    for side in (-1, 1):
        xc = side * (WING_GAP / 2 + WING_W / 2)
        r = WING_W / 2
        straight = FS_BASE_H + WING_H - r                         # 둥근 마감이 시작되는 높이
        pts = [(xc - r, FS_BASE_H), (xc + r, FS_BASE_H)]
        for i in range(WING_ARC_SEG + 1):                        # 오른쪽 → 왼쪽 반원
            a = math.pi * i / WING_ARC_SEG
            pts.append((xc + r * math.cos(a), straight + r * math.sin(a)))
        cap0, cap1, sides = extrude_profile_y(bm, pts, -WING_L / 2, WING_L / 2)
        smooth += sides[2:2 + WING_ARC_SEG]                      # 둥근 윗부분만 부드럽게
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    return make_object("M1_FrontSight", bm, smooth)

# =====================================================================
#  2. 뒤 가늠자
# =====================================================================
def build_rear():
    """뒤 가늠자 오브젝트를 만든다(원점 = 받침 아래면 가운데, 베벨·삼각형 전)."""
    bm = bmesh.new()
    smooth = []
    add_box(bm, 0, 0, RS_BASE_H / 2, RS_BASE_W, RS_BASE_L, RS_BASE_H)   # 받침(Sight Base)

    # 구멍 판: 구멍 원과 판 테두리를 같은 각도로 짚어 사각형 띠로 잇는다(얇은 판에 뚫린 작은 구멍).
    hc = Vector((0.0, RS_BASE_H + HOLE_Z))                       # 구멍 중심(X, Z)
    x0, x1, z0, z1 = -PLATE_W / 2, PLATE_W / 2, RS_BASE_H, RS_BASE_H + PLATE_H
    def outline_point(ang):
        """구멍 중심에서 ang 방향으로 판 테두리(위 두 모서리는 둥글게)에 닿는 점."""
        d = Vector((math.cos(ang), math.sin(ang)))
        ts = []
        if abs(d.x) > 1e-9: ts += [(x0 - hc.x) / d.x, (x1 - hc.x) / d.x]
        if abs(d.y) > 1e-9: ts += [(z0 - hc.y) / d.y, (z1 - hc.y) / d.y]
        t = min(t for t in ts if t > 0)
        p = hc + d * t
        for cx in (x0 + PLATE_TOP_R, x1 - PLATE_TOP_R):          # 위 모서리 둥글게
            c = Vector((cx, z1 - PLATE_TOP_R))
            if (p.x < x0 + PLATE_TOP_R or p.x > x1 - PLATE_TOP_R) and p.y > z1 - PLATE_TOP_R:
                # 원과 광선의 교점
                oc = hc - c; b = oc.dot(d); cc = oc.dot(oc) - PLATE_TOP_R ** 2
                disc = b * b - cc
                if disc >= 0: p = hc + d * (-b + math.sqrt(disc))
        return p
    angs = [2 * math.pi * i / HOLE_SEG for i in range(HOLE_SEG)]
    yf, yb = -PLATE_T / 2, PLATE_T / 2                           # 판 앞면(-Y), 뒷면(+Y)
    ring = []
    for ang in angs:
        o = outline_point(ang)
        h = hc + Vector((math.cos(ang), math.sin(ang))) * (HOLE_D / 2)
        ring.append((bm.verts.new((o.x, yf, o.y)), bm.verts.new((o.x, yb, o.y)),
                     bm.verts.new((h.x, yf, h.y)), bm.verts.new((h.x, yb, h.y))))
    n = len(ring)
    for i in range(n):
        of, ob_, hf, hb = ring[i]
        of2, ob2, hf2, hb2 = ring[(i + 1) % n]
        bm.faces.new((of, of2, hf2, hf))                          # 앞면
        bm.faces.new((ob_, hb, hb2, ob2))                         # 뒷면
        bm.faces.new((of, ob_, ob2, of2))                         # 바깥 테두리
        smooth.append(bm.faces.new((hf, hf2, hb2, hb)))           # 구멍 안쪽 벽(부드럽게)

    # 조절 휠: 사수 기준 왼쪽(+X) = 고도(크다), 오른쪽(-X) = 풍향(작다). X 축 방향 원통, 둘레를 톱니처럼 번갈아 반지름을 준다.
    for side, rad in ((1, KNOB_ELEV_R), (-1, KNOB_WIND_R)):   # 총구가 -Y, 위가 +Z 일 때 사수의 왼쪽은 +X
        xa = side * (RS_BASE_W / 2)
        xb = side * (RS_BASE_W / 2 + KNOB_W)
        pa, pb = [], []
        for i in range(KNOB_SEG):
            a = 2 * math.pi * i / KNOB_SEG
            rr = rad * (1.0 if i % 2 == 0 else 0.9)              # 홈 파인 손잡이
            y, z = rr * math.cos(a), KNOB_Z + rr * math.sin(a)
            pa.append(bm.verts.new((xa, y, z))); pb.append(bm.verts.new((xb, y, z)))
        bm.faces.new(pa if side > 0 else list(reversed(pa)))
        bm.faces.new(list(reversed(pb)) if side > 0 else pb)
        for i in range(KNOB_SEG):
            j = (i + 1) % KNOB_SEG
            bm.faces.new((pa[i], pa[j], pb[j], pb[i]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    return make_object("M1_RearSight", bm, smooth)

if __name__ == "__main__":
    # =====================================================================
    #  장면 비우기
    # =====================================================================
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mat = bpy.data.materials.new("MAT_M1_Sights")               # 재질 슬롯 1개: 파커라이징한 검은 쇠
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.045, 0.045, 0.042, 1)
    bsdf.inputs["Metallic"].default_value = 0.6
    bsdf.inputs["Roughness"].default_value = 0.55
    mat.diffuse_color = (0.09, 0.09, 0.085, 1)                   # 뷰포트·확인 렌더 색
    
    front = build_front()
    rear = build_rear()
    # =====================================================================
    #  3. 배치: 같은 조준선 위
    # =====================================================================
    rear.location = (0.0, 0.0, 0.0)
    front.location = (0.0, -SIGHT_RADIUS, SIGHT_LINE_Z - (FS_BASE_H + BLADE_H))   # 블레이드 윗면 = 조준선

    # =====================================================================
    #  4. 마무리: 베벨·삼각형, UV, 재질
    # =====================================================================
    counts = {}
    for ob in (front, rear):
        counts[ob.name] = bevel_and_finish(ob, BEVEL)
        unwrap(ob)
        ob.data.materials.append(mat)
    for name, tris in counts.items():
        print(f"REPORT {name}: {tris} 삼각형")
    print(f"REPORT 조준선 높이 {SIGHT_LINE_Z * 1000:.1f} mm (뒤 받침 아래면 기준), 가늠쇠까지 {SIGHT_RADIUS * 1000:.0f} mm")
    print(f"REPORT 구멍 지름 {HOLE_D * 1000:.1f} mm = 판 폭의 {HOLE_D / PLATE_W * 100:.0f}%")

    # =====================================================================
    #  5. Unity 용 FBX
    # =====================================================================
    # 회전·크기를 적용한다(위치는 남겨 두어 두 가늠 장치의 간격·높이가 그대로 간다).
    for o in bpy.context.view_layer.objects: o.select_set(o in (front, rear))
    bpy.context.view_layer.objects.active = rear
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    os.makedirs(os.path.dirname(EXPORT_PATH), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=EXPORT_PATH,
        use_selection=True,                     # 두 오브젝트만
        object_types={'MESH'},
        apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_ALL',    # 단위 배율을 FBX 안에 넣어 Unity 에서 Scale Factor 1, Transform 크기 1 로 들어온다
        axis_forward='-Z', axis_up='Y',         # Blender 의 -Y(총구)·+Z(위) → Unity 의 +Z(앞)·+Y(위)
        bake_space_transform=True,              # 축 변환을 정점에 구워 넣어, Unity 에서 오브젝트 회전이 (0,0,0) 으로 들어온다(-90° X 회전이 붙지 않는다)
        mesh_smooth_type='FACE',                # 면별 flat/smooth 를 그대로 보낸다
        use_mesh_modifiers=True,
        path_mode='STRIP', embed_textures=False)
    print("REPORT FBX:", EXPORT_PATH)

    # =====================================================================
    #  6. 확인용 렌더: 조준 시점(구멍 중심 뒤에서 앞을 본 모습)과 옆모습
    # =====================================================================
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'
    sc.display.shading.show_cavity = True
    sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.75, 0.78, 0.8)
    os.makedirs(os.path.dirname(RENDER_AIM), exist_ok=True)
    cd = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam); sc.camera = cam
    cd.clip_start = 0.001
    # 조준 시점: 눈은 구멍 중심에서 7 cm 뒤(+Y), 총구(-Y)를 본다
    sc.render.resolution_x, sc.render.resolution_y = 1200, 800
    cd.type = 'PERSP'; cd.lens = 35
    cam.location = (0.0, 0.07, SIGHT_LINE_Z)
    cam.rotation_euler = Vector((0, -1, 0)).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = RENDER_AIM; bpy.ops.render.render(write_still=True)
    # 옆모습: 뒤 가늠자와 앞 가늠쇠를 각각 가까이(빨간 선 = 조준선 높이)
    cd.type = 'ORTHO'; cd.ortho_scale = 0.05
    sc.render.resolution_x, sc.render.resolution_y = 800, 800
    line = bpy.data.objects.new("sight_line", bpy.data.meshes.new("sl"))
    bm = bmesh.new(); add_box(bm, 0, -SIGHT_RADIUS / 2, SIGHT_LINE_Z, 0.0002, SIGHT_RADIUS + 0.06, 0.0002); bm.to_mesh(line.data); bm.free()
    lm = bpy.data.materials.new("line"); lm.diffuse_color = (1, 0, 0, 1); line.data.materials.append(lm); sc.collection.objects.link(line)
    for tag, y in (("rear", 0.0), ("front", -SIGHT_RADIUS)):
        cam.location = (0.5, y, SIGHT_LINE_Z - 0.004)
        cam.rotation_euler = Vector((-1, 0, 0)).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = RENDER_SIDE.replace(".png", f"_{tag}.png"); bpy.ops.render.render(write_still=True)
    print("REPORT 렌더:", RENDER_AIM, RENDER_SIDE)
