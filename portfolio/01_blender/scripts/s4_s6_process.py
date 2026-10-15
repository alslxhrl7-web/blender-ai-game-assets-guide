"""4~6단계: 최적화 → 스타일 통일 → 게임용 편집·검수. 결과 .blend와 로그를 남긴다.
순서를 지시문과 바꾼 곳: Merge by Distance를 조각 정리보다 먼저 한다.
바르코3D 메시는 UV 이음매마다 정점이 갈라져 있어서(석구 3,836조각) 합치기 전에 1% 규칙을 쓰면 몸통까지 지워진다.
"""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
import numpy as np
from mathutils import Vector, Matrix
from common import *

LOG = {}


def log(key, k, v):
    LOG.setdefault(key, {})[k] = v
    print(f"LOG {key} {k}: {v}")


def islands(bm):
    bm.verts.ensure_lookup_table()
    seen = set(); out = []
    for v in bm.verts:
        if v.index in seen:
            continue
        comp = []; stack = [v]; seen.add(v.index)
        while stack:
            c = stack.pop(); comp.append(c)
            for e in c.link_edges:
                w = e.other_vert(c)
                if w.index not in seen:
                    seen.add(w.index); stack.append(w)
        out.append(comp)
    return out


def bbox_of(co):
    mn = Vector((min(v.x for v in co), min(v.y for v in co), min(v.z for v in co)))
    mx = Vector((max(v.x for v in co), max(v.y for v in co), max(v.z for v in co)))
    return mn, mx


# ---------------- 4. 최적화 ----------------
def optimize(key, obj):
    target = RES[key][4]
    t0 = tris(obj)
    bm = bmesh.new(); bm.from_mesh(obj.data)
    v0 = len(bm.verts)
    raw_isl = len(islands(bm))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0001)
    v1 = len(bm.verts)
    isl = islands(bm)
    total = len(bm.verts)
    removed = []
    kill = []
    for c in isl:
        if len(c) < total * 0.01:
            mn, mx = bbox_of([v.co for v in c])
            cen = (mn + mx) / 2
            removed.append({"verts": len(c), "center": [round(x, 3) for x in cen], "size": [round(x, 3) for x in (mx - mn)]})
            kill += c
    bmesh.ops.delete(bm, geom=kill, context='VERTS')
    bm.to_mesh(obj.data); bm.free(); obj.data.update()
    t1 = tris(obj)
    log(key, "merge_by_distance", f"정점 {v0} → {v1} (겹친 정점 {v0 - v1}개 합침, 거리 0.1 mm)")
    log(key, "islands", f"조각 {raw_isl}(합치기 전) → {len(isl)}(합친 후) → 지운 조각 {len(removed)} / 남긴 조각 {len(isl) - len(removed)}")
    log(key, "removed_parts", removed)

    ratio = min(1.0, target / t1)
    mod = obj.modifiers.new("Decimate_Game", 'DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = ratio
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    t2 = tris(obj)
    log(key, "decimate", {"tris_raw": t0, "tris_before_decimate": t1, "ratio": round(ratio, 4), "tris_after": t2})


# ---------------- 5. 스타일 통일 ----------------
def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def rgb_to_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(-1); mn = rgb.min(-1); d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-8
    rc = np.where(m, (mx - r) / np.where(m, d, 1), 0)
    gc = np.where(m, (mx - g) / np.where(m, d, 1), 0)
    bc = np.where(m, (mx - b) / np.where(m, d, 1), 0)
    h = np.where(r == mx, bc - gc, np.where(g == mx, 2.0 + rc - bc, 4.0 + gc - rc))
    h = np.where(m, (h / 6.0) % 1.0, 0)
    s = np.where(mx > 1e-8, d / np.where(mx > 1e-8, mx, 1), 0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    i = np.floor(h * 6.0).astype(np.int32) % 6
    f = h * 6.0 - np.floor(h * 6.0)
    p_ = v * (1 - s); q = v * (1 - s * f); t = v * (1 - s * (1 - f))
    out = np.zeros(h.shape + (3,), dtype=np.float32)
    for k, (a, b, c) in enumerate([(v, t, p_), (q, v, p_), (p_, v, t), (p_, q, v), (t, p_, v), (v, p_, q)]):
        m = i == k
        out[m, 0] = a[m]; out[m, 1] = b[m]; out[m, 2] = c[m]
    return out


def img_np(img):
    w, h = img.size
    a = np.empty(w * h * 4, dtype=np.float32); img.pixels.foreach_get(a)
    return a.reshape(h, w, 4)


def img_set(img, a):
    img.pixels.foreach_set(a.astype(np.float32).ravel())
    img.update()
    img.pack()


def find_tex(mat, socket_name):
    """Principled 입력에 연결된 이미지 텍스처를 거슬러 찾는다."""
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    sock = bsdf.inputs[socket_name]
    seen = []
    stack = [l.from_node for l in sock.links]
    while stack:
        n = stack.pop()
        if n.type == 'TEX_IMAGE':
            return n, bsdf
        seen.append(n.name)
        for i in n.inputs:
            for l in i.links:
                stack.append(l.from_node)
    return None, bsdf


def style(key, obj, sat=0.9, val=0.87):
    mat = obj.material_slots[0].material
    mat.name = f"M_{obj.name}"
    links = [f"{l.from_node.type}.{l.from_socket.name} → {l.to_node.type}.{l.to_socket.name}" for l in mat.node_tree.links]
    log(key, "material_links", links)

    # 텍스처 해상도: 4K → 2K (1인칭 원경 기준)
    sizes = {}
    for img in bpy.data.images:
        if img.size[0] > 2048:
            before = tuple(img.size)
            img.scale(2048, 2048)
            img.pack()
            sizes[img.name] = f"{before[0]}→2048"
    log(key, "texture_resize", sizes or "변경 없음 (이미 2K)")

    # 베이스 컬러: Hue/Saturation/Value(채도 0.9, 명도 0.87)를 선형 공간에서 적용한 뒤 텍스처에 굽는다
    # (FBX는 셰이더 노드를 싣지 못하므로 같은 값을 텍스처 픽셀에 직접 반영)
    tex, bsdf = find_tex(mat, "Base Color")
    a = img_np(tex.image)
    rgb = srgb_to_lin(a[..., :3])
    mean_before = a[..., :3].mean(axis=(0, 1))
    h, s, v = rgb_to_hsv(rgb)
    s0 = float(s.mean())
    rgb2 = hsv_to_rgb(h, np.clip(s * sat, 0, 1), v * val)
    a[..., :3] = lin_to_srgb(rgb2)
    img_set(tex.image, a)
    log(key, "hsv", {"saturation": sat, "value": val, "평균색_sRGB_전": [round(float(x), 3) for x in mean_before],
                     "평균색_sRGB_후": [round(float(x), 3) for x in a[..., :3].mean(axis=(0, 1))], "평균채도_전(선형)": round(s0, 3)})

    # 금속: ORM(R=AO, G=Roughness, B=Metallic)에서 금속 픽셀(B>0.5)만 Metallic 0.8~1.0, Roughness 0.4~0.55로
    mtex, _ = find_tex(mat, "Metallic")
    if mtex:
        o = img_np(mtex.image)
        B = o[..., 2]; G = o[..., 1]
        mask = B > 0.5
        frac = float(mask.mean())
        stat_b = (round(float(B[mask].mean()), 3) if mask.any() else None, round(float(G[mask].mean()), 3) if mask.any() else None)
        o[..., 2] = np.where(mask, 0.8 + 0.2 * (B - 0.5) / 0.5, B)
        o[..., 1] = np.where(mask, np.clip(G, 0.4, 0.55), G)
        img_set(mtex.image, o)
        log(key, "metal", {"금속_픽셀_비율": round(frac, 4), "금속부_평균(Metallic,Roughness)_전": stat_b,
                           "금속부_평균_후": (round(float(o[..., 2][mask].mean()), 3) if mask.any() else None,
                                          round(float(o[..., 1][mask].mean()), 3) if mask.any() else None)})
    else:
        log(key, "metal", "Metallic 맵 없음 (마대·흙·판자만 있음)")
    return mat


# ---------------- 6. 편집 · 검수 ----------------
def split_faces(obj, pred, name, faces_idx=None):
    """pred(face_center)가 참인 면(또는 faces_idx의 면)을 새 오브젝트로 떼어 낸다. bmesh로 처리."""
    me = obj.data
    bm = bmesh.new(); bm.from_mesh(me)
    if faces_idx is not None:
        sel = [f for f in bm.faces if f.index in faces_idx]
    else:
        sel = [f for f in bm.faces if pred(f.calc_center_median())]
    if not sel:
        bm.free(); return None
    new_me = bpy.data.meshes.new(name)
    bm2 = bm.copy()
    # 새 메시: 선택 면만 남김
    idx = {f.index for f in sel}
    bm2.faces.ensure_lookup_table()
    bmesh.ops.delete(bm2, geom=[f for f in bm2.faces if f.index not in idx], context='FACES')
    bm2.to_mesh(new_me); bm2.free()
    # 원래 메시: 선택 면 삭제
    bmesh.ops.delete(bm, geom=sel, context='FACES')
    bm.to_mesh(me); bm.free(); me.update()
    new = bpy.data.objects.new(name, new_me)
    for m in obj.data.materials:
        new_me.materials.append(m)
    bpy.context.scene.collection.objects.link(new)
    return new


def origin_to_bottom_center(o):
    mn, mx = world_bbox([o])
    pivot = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
    o.data.transform(Matrix.Translation(-pivot))
    o.location = pivot
    o.data.update()


def separate_helmet(obj):
    """철모: 머리 위 15% 안에서 수평 폭이 가장 넓은 높이(챙)를 찾아, 그보다 위의 면을 떼어 낸다."""
    co = np.array([v.co[:] for v in obj.data.vertices])
    H = co[:, 2].max()
    head = co[(co[:, 2] > H * 0.85) & (np.abs(co[:, 0]) < 0.2)]
    cx, cy = np.median(head[:, 0]), np.median(head[:, 1])
    best = (0, None)
    for z in np.arange(H * 0.86, H * 0.97, 0.004):
        sl = head[(head[:, 2] > z - 0.002) & (head[:, 2] < z + 0.002)]
        if len(sl) < 10:
            continue
        r = np.percentile(np.hypot(sl[:, 0] - cx, sl[:, 1] - cy), 98)
        if r > best[0]:
            best = (r, z)
    brim_r, brim_z = best
    cut = brim_z - 0.006
    # 높이만으로 자르면 챙 아래 이마 피부가 딸려 온다 → 면의 UV 위치에서 베이스 컬러를 읽어 피부색(R이 G보다 15% 이상 큼)은 뺀다
    tex, _ = find_tex(obj.material_slots[0].material, "Base Color")
    img = img_np(tex.image); ih, iw = img.shape[:2]
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.faces.ensure_lookup_table()
    uvl = bm.loops.layers.uv.active
    cand = set()
    for f in bm.faces:
        c = f.calc_center_median()
        if c.z < cut - 0.04 or math.hypot(c.x - cx, c.y - cy) > brim_r * 1.15:
            continue
        u = sum(l[uvl].uv.x for l in f.loops) / len(f.loops); v = sum(l[uvl].uv.y for l in f.loops) / len(f.loops)
        r, g, b = img[min(ih - 1, max(0, int(v * ih))), min(iw - 1, max(0, int(u * iw))), :3]
        if not (r > g * 1.15 and r > b * 1.3):
            cand.add(f.index)
    # 선택된 면 중 가장 큰 연결 덩어리만 철모로
    best_comp = set(); seen = set()
    for i in cand:
        if i in seen: continue
        comp = {i}; stack = [bm.faces[i]]; seen.add(i)
        while stack:
            f = stack.pop()
            for e in f.edges:
                for g2 in e.link_faces:
                    if g2.index in cand and g2.index not in seen:
                        seen.add(g2.index); comp.add(g2.index); stack.append(g2)
        if len(comp) > len(best_comp): best_comp = comp
    bm.free()
    helmet = split_faces(obj, None, "Seokgu_Helmet", faces_idx=best_comp)
    # 몸통 쪽 머리 구멍 메우기
    bm = bmesh.new(); bm.from_mesh(obj.data)
    edges = [e for e in bm.edges if e.is_boundary and min(v.co.z for v in e.verts) > cut - 0.09]
    res = bmesh.ops.holes_fill(bm, edges=edges, sides=1000)
    if not res["faces"]:
        # 고리가 단순 루프가 아니면 holes_fill이 실패한다 → 삼각형 채우기로 덮는다
        res = {"faces": bmesh.ops.triangle_fill(bm, use_beauty=True, use_dissolve=False, edges=edges)["geom"]}
        res["faces"] = [g for g in res["faces"] if isinstance(g, bmesh.types.BMFace)]
    bmesh.ops.triangulate(bm, faces=res["faces"])
    bm.to_mesh(obj.data); bm.free(); obj.data.update()
    # 철모 재질: 금속 기준 (Metallic 0.85, Roughness 0.5)
    hm = obj.data.materials[0].copy(); hm.name = "M_Seokgu_Helmet"
    bsdf = next(n for n in hm.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    for sock, val in (("Metallic", 0.85), ("Roughness", 0.5)):
        for l in list(bsdf.inputs[sock].links):
            hm.node_tree.links.remove(l)
        bsdf.inputs[sock].default_value = val
    helmet.data.materials.clear(); helmet.data.materials.append(hm)
    origin_to_bottom_center(helmet)
    return helmet, {"챙_높이_m": round(float(brim_z), 3), "챙_반지름_m": round(float(brim_r), 3),
                    "철모_tris": tris(helmet), "머리_구멍_메운_면": len(res["faces"])}


def split_sandbag(obj):
    parts = {}
    rules = [
        ("Sandbag_Wall_Front", lambda c: c.y < -1.55 and abs(c.x) < 2.4 and c.z > 0.12),
        ("Sandbag_Wall_Left",  lambda c: c.x < -1.5 and -1.55 <= c.y < 1.3 and c.x > -2.45 and c.z > 0.12),
        ("Sandbag_Wall_Right", lambda c: c.x > 1.5 and -1.55 <= c.y < 1.3 and c.x < 2.4 and c.z > 0.12),
    ]
    for name, rule in rules:
        o = split_faces(obj, rule, name)
        origin_to_bottom_center(o)
        parts[name] = tris(o)
    obj.name = "Sandbag_Ground"; obj.data.name = obj.name
    parts["Sandbag_Ground"] = tris(obj)
    root = bpy.data.objects.new("Env_SandbagPosition", None)
    bpy.context.scene.collection.objects.link(root)
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            o.parent = root
    return parts


def normals_and_holes(objs, max_sides=64):
    out = {}
    for o in objs:
        bm = bmesh.new(); bm.from_mesh(o.data)
        # 노멀 방향: 바깥쪽 재계산 전 '뒤집힌 면' 추정 (재계산 후 방향이 바뀐 면 수)
        before = {f.index: f.normal.copy() for f in bm.faces}
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        flipped = sum(1 for f in bm.faces if f.normal.dot(before[f.index]) < 0)
        nm0 = sum(1 for e in bm.edges if not e.is_manifold)
        bnd0 = [e for e in bm.edges if e.is_boundary]
        res = bmesh.ops.holes_fill(bm, edges=bnd0, sides=max_sides)
        bmesh.ops.triangulate(bm, faces=res["faces"])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        # 열린 메시는 recalc가 안쪽을 고를 수 있다. 부호 있는 부피가 음수면 전체를 뒤집어 바깥쪽으로
        vol = sum(f.calc_center_median().dot(f.normal) * f.calc_area() for f in bm.faces) / 3
        if vol < 0:
            bmesh.ops.reverse_faces(bm, faces=bm.faces)
        flipped = sum(1 for f in bm.faces if f.index in before and f.normal.dot(before[f.index]) < 0)
        nm1 = sum(1 for e in bm.edges if not e.is_manifold)
        bm.to_mesh(o.data); bm.free(); o.data.update()
        out[o.name] = {"뒤집혔던_면": flipped, "non_manifold_전": nm0, "메운_구멍면": len(res["faces"]), "non_manifold_후": nm1, "tris": tris(o)}
    return out


# ---------------- 실행 ----------------
NAMES = {"seokgu": "Seokgu", "garand": "M1Garand", "sandbag": "SandbagPosition"}
for key in RES:
    bpy.ops.wm.open_mainfile(filepath=p("blender", f"{key}_01_spec.blend"))
    obj = bpy.data.objects[NAMES[key]]
    bpy.context.view_layer.objects.active = obj; obj.select_set(True)

    optimize(key, obj)
    bpy.ops.wm.save_as_mainfile(filepath=p("blender", f"{key}_02_opt.blend"))

    style(key, obj)
    bpy.ops.wm.save_as_mainfile(filepath=p("blender", f"{key}_03_style.blend"))

    if key == "seokgu":
        helmet, info = separate_helmet(obj)
        log(key, "parts", info)
    elif key == "sandbag":
        log(key, "parts", split_sandbag(obj))
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    log(key, "normals_holes", normals_and_holes(meshes))
    log(key, "tris_final", sum(tris(o) for o in meshes))
    bpy.ops.wm.save_as_mainfile(filepath=p("blender", f"{key}_04_final.blend"))

with open(p("claude_log", "process_log.json"), "w", encoding="utf-8") as f:
    json.dump(LOG, f, ensure_ascii=False, indent=1)
print("DONE")
