# 배경 나무·바위를 실물에 가깝게: Poly Haven 실측 스캔(CC0)을 바탕으로 만든다. carve_nature.py 의 뼈대·굽기 도구를 함께 쓴다.
# 쓰는 법: blender -b --python realistic_nature.py -- <이름|all> <작업 폴더(ArtSource)>
# 자료 받기: fetch_polyhaven.py (ArtSource/Textures/PolyHaven/)
#   BG_RockA/B/C : 화강암 바위 스캔(namaqualand_boulder_03/04/05) → 1,500 삼각형으로 줄이고 스캔의 색·굴곡을 구워 넣는다.
#                  사막 바위의 주황 녹(철분)을 덜어 팔공산 일대 화강암처럼 회백색 쪽으로 맞춘다.
#   BG_Pine      : 적송 뼈대(carve_nature) + 실측 소나무 껍질(pine_bark) + 실제 솔잎 가지(pine_sapling_small)를 찍은 잎 카드.
#   BG_DeadTree  : 포탄 맞은 고사목 뼈대 + 실측 소나무 껍질에 그을음.
#   BG_Bush      : 실제 관목 잎(shrub_04)을 찍은 잎 카드 + 잔가지.
# 잎 카드는 투명한 부분이 있어 색 텍스처를 PNG(알파 포함)로 낸다: T_<이름>_BaseColor.png. 유니티 재질은 Alpha Clip·양면(M01VarcoDresser).
import bpy, bmesh, sys, os, math, random, glob
import numpy as np
from mathutils import Vector, Matrix, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import carve_nature as cn

root, which = cn.root, cn.which
PH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Textures", "PolyHaven")  # 받은 원본(출력 폴더와 무관)


def tex(asset, key):
    return glob.glob(os.path.join(PH, asset, "textures", "*%s*" % key))[0]


def import_scan(asset, keep=None):
    bpy.ops.import_scene.gltf(filepath=glob.glob(os.path.join(PH, asset, "*.gltf"))[0])
    ms = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for o in list(bpy.context.scene.objects):
        if o.type != 'MESH' or (keep and keep not in o.name): bpy.data.objects.remove(o)
    ms = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for o in ms:
        o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ms


def img_np(img):
    a = np.empty(img.size[0] * img.size[1] * 4, np.float32)
    img.pixels.foreach_get(a)
    return a.reshape(img.size[1], img.size[0], 4)


# ---------------------------------------------------------------- 실측 껍질(박스 투영 + 색의 밝기로 요철)
def scan_bark(nt, bsdf, red=True, charred=False):
    co = cn.coord(nt, (1 / 1.6, 1 / 1.6, 1 / 1.6))  # 텍스처 한 장 = 1.6 m(적송 아래 껍질 판이 크다)
    im = nt.nodes.new('ShaderNodeTexImage'); im.image = bpy.data.images.load(tex("pine_bark", "diff"), check_existing=True)
    im.projection = 'BOX'; im.projection_blend = .3
    nt.links.new(co, im.inputs['Vector'])
    c = im.outputs['Color']
    if red:  # 적송: 위로 갈수록 얇게 벗겨지는 붉은 주황 껍질
        hs = cn.N(nt, 'ShaderNodeHueSaturation', **{'Hue': .495, 'Saturation': 1.25, 'Value': 1.3})
        nt.links.new(c, hs.inputs['Color'])
        tint = nt.nodes.new('ShaderNodeMix'); tint.data_type = 'RGBA'; tint.blend_type = 'MULTIPLY'; tint.inputs[0].default_value = 1
        nt.links.new(hs.outputs[0], tint.inputs[6]); tint.inputs[7].default_value = (1, .66, .5, 1)
        # 아래 몸통: 두껍게 갈라진 회갈색 껍질 판(거북등)
        low = cn.N(nt, 'ShaderNodeHueSaturation', **{'Saturation': .45, 'Value': .72})
        nt.links.new(c, low.inputs['Color'])
        up = cn.ramp(nt, cn.height(nt, 1.8, 3.4), [(0, (0, 0, 0)), (1, (1, 1, 1))])
        c = cn.mix(nt, up.outputs[0], low.outputs[0], tint.outputs[2])
    if charred:
        nz = cn.N(nt, 'ShaderNodeTexNoise', **{'Scale': 2.2, 'Detail': 6.0})
        nt.links.new(cn.coord(nt, (1, 1, .5)), nz.inputs['Vector'])
        burn = cn.ramp(nt, nz.outputs['Fac'], [(.5, (1, 1, 1)), (.6, (0, 0, 0))])
        soot = nt.nodes.new('ShaderNodeMix'); soot.data_type = 'RGBA'; soot.blend_type = 'MULTIPLY'; soot.inputs[0].default_value = 1
        nt.links.new(c, soot.inputs[6]); soot.inputs[7].default_value = (.12, .11, .1, 1)
        c2 = cn.mix(nt, burn.outputs[0], c, soot.outputs[2])
        # 껍질이 떨어져 드러난 회색 속나무(비바람에 바랜 색)
        pe = cn.N(nt, 'ShaderNodeTexNoise', **{'Scale': 3.5, 'Detail': 3.0})
        nt.links.new(cn.coord(nt, (1.6, 1.6, .45)), pe.inputs['Vector'])
        bare = cn.ramp(nt, pe.outputs['Fac'], [(.64, (0, 0, 0)), (.69, (.8, .8, .8))])
        wood = cn.N(nt, 'ShaderNodeHueSaturation', **{'Saturation': .3, 'Value': 1.35})
        nt.links.new(c, wood.inputs['Color'])
        c = cn.mix(nt, bare.outputs[0], c2, wood.outputs[0])
    dirt = cn.ramp(nt, cn.height(nt, 0, .3), [(.0, (1, 1, 1)), (1, (0, 0, 0))])
    c = cn.mix(nt, dirt.outputs[0], c, (.33, .26, .2))
    nt.links.new(c, bsdf.inputs['Base Color'])
    # 요철: 껍질 사진의 골이 어두우니 밝기를 높이로 쓴다(박스 투영에서도 방향이 틀리지 않는 bump)
    bw = cn.N(nt, 'ShaderNodeRGBToBW'); nt.links.new(im.outputs['Color'], bw.inputs[0])
    bump = cn.N(nt, 'ShaderNodeBump', **{'Strength': .9, 'Distance': .015})
    nt.links.new(bw.outputs[0], bump.inputs['Height'])
    nt.links.new(bump.outputs[0], bsdf.inputs['Normal'])


cn.bark = scan_bark  # carve_nature 의 make_dead 등이 이 껍질을 쓰게 한다


# ---------------------------------------------------------------- 굽기(이미지에 직접, 저장은 따로)
def bake_into(high, low, img_c, img_n, extr, ray, color=True):
    low.data.materials.clear()
    m = bpy.data.materials.new("M_bake"); low.data.materials.append(m)
    nt = m.node_tree
    t = nt.nodes.new('ShaderNodeTexImage')
    bpy.ops.object.select_all(action='DESELECT')
    high.select_set(True); low.select_set(True); bpy.context.view_layer.objects.active = low
    kw = dict(use_selected_to_active=True, cage_extrusion=extr, max_ray_distance=ray, margin=8)
    bpy.context.scene.cycles.samples = 4
    if color:
        t.image = img_c; nt.nodes.active = t
        bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, **kw)
    t.image = img_n; nt.nodes.active = t
    bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', **kw)


def final_material(low, name, img_c, img_n, alpha):
    low.data.materials.clear()
    m = bpy.data.materials.new("M_" + name); low.data.materials.append(m)
    nt = m.node_tree; b = nt.nodes['Principled BSDF']
    tc = nt.nodes.new('ShaderNodeTexImage'); tc.image = img_c
    nt.links.new(tc.outputs[0], b.inputs['Base Color'])
    if alpha:
        nt.links.new(tc.outputs['Alpha'], b.inputs['Alpha'])
        try: m.surface_render_method = 'DITHERED'
        except AttributeError: m.blend_method = 'CLIP'
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = img_n
    nm = nt.nodes.new('ShaderNodeNormalMap')
    nt.links.new(tn.outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], b.inputs['Normal'])
    b.inputs['Roughness'].default_value = .9


def save(img, name, kind, ext):
    img.filepath_raw = os.path.join(root, "Export", "T_%s_%s.%s" % (name, kind, ext))
    img.file_format = 'PNG' if ext == "png" else 'JPEG'
    img.save()


# ---------------------------------------------------------------- 바위: 화강암 스캔
ROCKS = {  # 스캔, Z 회전(도), 크기(m)
    "BG_RockA": ("namaqualand_boulder_03", 90, (1.5, 1.42, 1.0)),
    "BG_RockB": ("namaqualand_boulder_04", 0, (1.5, 1.38, 1.05)),
    "BG_RockC": ("namaqualand_boulder_05", 0, (1.5, 1.3, .9)),
}


def make_rock(name):
    scan, turn, (sx, sy, sz) = ROCKS[name]
    ms = import_scan(scan)
    high = cn.join(ms, name + "_high")
    high.data.transform(Matrix.Rotation(math.radians(turn), 4, 'Z'))
    mn, mx = cn.bounds(high)
    high.data.transform(Matrix.Translation(-Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))))
    high.data.transform(Matrix.Diagonal(Vector((sx / (mx.x - mn.x), sy / (mx.y - mn.y), sz / (mx.z - mn.z), 1))))
    # 사막 바위의 주황 녹을 덜어 회백색 화강암 쪽으로
    for m in high.data.materials:
        nt = m.node_tree; b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
        src = b.inputs['Base Color'].links[0].from_socket
        hs = cn.N(nt, 'ShaderNodeHueSaturation', **{'Saturation': .4, 'Value': .86})
        nt.links.new(src, hs.inputs['Color']); nt.links.new(hs.outputs[0], b.inputs['Base Color'])
    low = high.copy(); low.data = high.data.copy(); low.name = low.data.name = name
    bpy.context.scene.collection.objects.link(low)
    d = low.modifiers.new("D", 'DECIMATE'); d.ratio = 1500 / cn.tris(high); d.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = low; bpy.ops.object.modifier_apply(modifier=d.name)
    cn.finish_low(low)
    cn.bake(high, low, name, 2048, .03, .08)
    cn.export(low, high, name)


# ---------------------------------------------------------------- 잎 카드: 실제 가지 스캔을 정사영으로 찍는다
def render_card(asset, keep, view, res=1024):
    """반환: (색 RGBA, 법선 RGB, 실제 크기 m) — 배경은 투명, 색은 빛 없이 텍스처 그대로(알베도)."""
    cn.reset()
    sc = bpy.context.scene
    ms = import_scan(asset, keep)
    pts = [o.matrix_world @ Vector(c) for o in ms for c in o.bound_box]
    mn = Vector([min(p[i] for p in pts) for i in range(3)]); mx = Vector([max(p[i] for p in pts) for i in range(3)])
    c = (mn + mx) / 2; size = mx - mn
    cam = bpy.data.objects.new("C", bpy.data.cameras.new("C")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = 'ORTHO'
    if view == 'top':
        cam.location = c + Vector((0, 0, size.z + 1)); cam.rotation_euler = (0, 0, 0); span = max(size.x, size.y)
    else:
        cam.location = c - Vector((0, size.y + 1, 0)); cam.rotation_euler = (math.radians(90), 0, 0); span = max(size.x, size.z)
    cam.data.ortho_scale = span * 1.03
    cam.data.clip_end = 100
    sc.render.resolution_x = sc.render.resolution_y = res
    sc.render.film_transparent = True
    sc.render.image_settings.color_mode = 'RGBA'
    sc.cycles.samples = 24
    mats = {m for o in ms for m in o.data.materials if m}
    outs = {}
    for pass_ in ("color", "normal"):
        for m in mats:
            nt = m.node_tree
            b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
            out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
            em = nt.nodes.new('ShaderNodeEmission')
            if pass_ == "color":
                l = b.inputs['Base Color'].links
                if l: nt.links.new(l[0].from_socket, em.inputs['Color'])
                else: em.inputs['Color'].default_value = b.inputs['Base Color'].default_value
            else:  # 카메라 기준 법선(카드의 접선 공간과 같다) → 0~1
                nm = b.inputs['Normal'].links[0].from_socket if b.inputs['Normal'].links else None
                g = nt.nodes.new('ShaderNodeNewGeometry')
                vt = nt.nodes.new('ShaderNodeVectorTransform'); vt.vector_type = 'NORMAL'; vt.convert_from = 'WORLD'; vt.convert_to = 'CAMERA'
                nt.links.new(nm if nm else g.outputs['Normal'], vt.inputs[0])
                # 뒷면이면 뒤집기
                flip = nt.nodes.new('ShaderNodeVectorMath'); flip.operation = 'SCALE'
                sgn = nt.nodes.new('ShaderNodeMath'); sgn.operation = 'MULTIPLY_ADD'
                nt.links.new(g.outputs['Backfacing'], sgn.inputs[0]); sgn.inputs[1].default_value = -2; sgn.inputs[2].default_value = 1
                nt.links.new(vt.outputs[0], flip.inputs[0]); nt.links.new(sgn.outputs[0], flip.inputs['Scale'])
                ma = nt.nodes.new('ShaderNodeVectorMath'); ma.operation = 'MULTIPLY_ADD'
                nt.links.new(flip.outputs[0], ma.inputs[0]); ma.inputs[1].default_value = (.5, .5, .5); ma.inputs[2].default_value = (.5, .5, .5)
                nt.links.new(ma.outputs[0], em.inputs['Color'])
            mixs = nt.nodes.new('ShaderNodeMixShader')
            tr = nt.nodes.new('ShaderNodeBsdfTransparent')
            if b.inputs['Alpha'].links: nt.links.new(b.inputs['Alpha'].links[0].from_socket, mixs.inputs[0])
            else: mixs.inputs[0].default_value = 1
            nt.links.new(tr.outputs[0], mixs.inputs[1]); nt.links.new(em.outputs[0], mixs.inputs[2])
            nt.links.new(mixs.outputs[0], out.inputs['Surface'])
        sc.view_settings.view_transform = 'Standard' if pass_ == "color" else 'Raw'
        path = os.path.join(bpy.app.tempdir, "card_%s_%s_%s.png" % (asset, view, pass_))
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        outs[pass_] = img_np(bpy.data.images.load(path))
    return outs["color"], outs["normal"], span * 1.03


def clean_card(col, nrm):
    """가는 솔잎이 Alpha Clip 에서 사라지지 않게 알파를 키우고, 투명한 곳에 색을 번지게 해(밉맵 테두리 방지) 둔다."""
    a = np.clip(col[..., 3] * 2.2, 0, 1)
    rgb = col[..., :3].copy(); n = nrm[..., :3].copy()
    filled = a > .05
    for _ in range(24):
        acc = np.zeros_like(rgb); accn = np.zeros_like(n); cnt = np.zeros(a.shape, np.float32)
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            m = np.roll(np.roll(filled, dy, 0), dx, 1)
            acc += np.roll(np.roll(rgb, dy, 0), dx, 1) * m[..., None]
            accn += np.roll(np.roll(n, dy, 0), dx, 1) * m[..., None]
            cnt += m
        grow = (~filled) & (cnt > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]; n[grow] = accn[grow] / cnt[grow][:, None]
        filled = filled | grow
    rgb[~filled] = rgb[filled].mean(0); n[~filled] = (.5, .5, 1)
    ys, xs = np.nonzero(a > .3)
    box = (xs.min(), xs.max() + 1, ys.min(), ys.max() + 1)
    return np.dstack([rgb, a]), np.dstack([n, np.ones_like(a)]), box


class Cards:
    """잎 카드(사각형 2삼각형) 모음: 아틀라스 UV 와 덩어리 바깥을 향한 법선(덩어리째 둥글게 빛을 받게)."""
    def __init__(self): self.V, self.F, self.UV, self.N = [], [], [], []

    def add(self, c, R, U, w, h, uv, center, roundness=.6):
        R = R.normalized(); U = U.normalized()
        corners = [c - R * w / 2 - U * h / 2, c + R * w / 2 - U * h / 2, c + R * w / 2 + U * h / 2, c - R * w / 2 + U * h / 2]
        u0, u1, v0, v1 = uv
        o = len(self.V); self.V += corners; self.F.append((o, o + 1, o + 2, o + 3))
        self.UV.append([(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
        fn = R.cross(U)
        ns = []
        for p in corners:
            out = (p - center); out = out.normalized() if out.length > 1e-4 else Vector((0, 0, 1))
            if fn.dot(out) < 0: fn_ = -fn
            else: fn_ = fn
            ns.append((fn_ * (1 - roundness) + out * roundness).normalized())
        self.N.append(ns)

    def obj(self, name):
        me = bpy.data.meshes.new(name); me.from_pydata([tuple(v) for v in self.V], [], self.F); me.update()
        uv = me.uv_layers.new(name="UVMap")
        for poly, uvs in zip(me.polygons, self.UV):
            for li, t in zip(poly.loop_indices, uvs): uv.data[li].uv = t
        loops = [None] * len(me.loops)
        for poly, ns in zip(me.polygons, self.N):
            for li, n in zip(poly.loop_indices, ns): loops[li] = n
        for p in me.polygons: p.use_smooth = True
        me.normals_split_custom_set(loops)
        o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
        return o


def atlas_paste(atlas, card, box, region, res):
    """카드 그림(res×res)을 아틀라스의 오른쪽 위/아래 칸에 붙이고, 카드가 쓸 UV 사각형을 돌려준다."""
    H = atlas.shape[0]; half = H // 2
    if card.shape[0] != half:  # 크기 맞추기(가장 가까운 화소)
        idx = (np.arange(half) * card.shape[0] / half).astype(int); card = card[idx][:, idx]
        box = tuple(int(b * half / res) for b in box)
    y0 = half if region == 'top' else 0
    atlas[y0:y0 + half, half:] = card
    x0, x1, by0, by1 = box
    return (.5 + x0 / H, .5 + x1 / H, (y0 + by0) / H, (y0 + by1) / H), ((x1 - x0) / half, (by1 - by0) / half)


def wood_part(limbs, seed, depth, scale_f, lo_sides=7, lo_step=.38):
    hi, lo = cn.woody(limbs, seed, lo_sides=lo_sides, lo_step=lo_step, depth=depth)
    wh = cn.mesh_obj("wood_hi", *cn.merge(hi)); wl = cn.mesh_obj("wood_lo", *cn.merge(lo))
    for o in (wh, wl): o.data.transform(Matrix.Scale(scale_f, 4))
    return wh, wl


def leafy(name, cards_fn, wood_fn, size=2048):
    """잎 카드 나무 공통: 줄기는 구워서 아틀라스 왼쪽 반, 잎 카드 그림은 오른쪽 반에."""
    shots = cards_fn("shots")  # [(색, 법선, 상자, 실제 폭)] 위/옆
    cn.reset()
    wh, wl, f = wood_fn()
    wh.data.materials.append(cn.node_mat("Bark", lambda nt, b: scan_bark(nt, b, red=(name == "BG_Pine"))))
    cn.finish_low(wl)
    for l in wl.data.uv_layers[0].data: l.uv.x *= .5  # 왼쪽 반으로
    img_c = bpy.data.images.new("T_%s_BaseColor" % name, size, size, alpha=True)
    img_n = bpy.data.images.new("T_%s_Normal" % name, size, size, alpha=False); img_n.colorspace_settings.name = 'Non-Color'
    bake_into(wh, wl, img_c, img_n, .04, .1)
    C = img_np(img_c); Nn = img_np(img_n)
    C[..., 3] = 1
    rects = {}
    for region, (col, nrm, box, span) in zip(('top', 'side'), shots):
        uvr, frac = atlas_paste(C, col, box, region, col.shape[0])
        atlas_paste(Nn, nrm, box, region, col.shape[0])
        rects[region] = (uvr, (frac[0] * span, frac[1] * span))
    img_c.pixels.foreach_set(C.ravel()); img_n.pixels.foreach_set(Nn.ravel())
    cards = cards_fn(rects, f)
    co = cards.obj("cards")
    low = cn.join([wl, co], name)
    final_material(low, name, img_c, img_n, alpha=True)
    save(img_c, name, "BaseColor", "png"); save(img_n, name, "Normal", "jpg")
    cn.export(low, wh, name)


# ---------------------------------------------------------------- 적송
def pine_skeleton():
    rnd = random.Random(5)
    H = 4.75
    trunk = [Vector((.55 * math.sin(i / 47 * 2.4) - .1 * i / 47, .18 * math.sin(i / 47 * 4.1), H * i / 47)) for i in range(48)]
    limbs = [(trunk, cn.taper(48, .25, .07, flare=.7), None)]
    pads = [(trunk[-1] + Vector((0, 0, .15)), 1.25)]
    for k, (h, az, L) in enumerate([(2.55, 20, 1.6), (2.9, 145, 1.45), (3.25, 260, 1.5), (3.6, 80, 1.15), (3.85, 200, 1.05), (4.15, 320, .95)]):
        s = trunk[int(h / H * 47)]
        d = Vector((math.cos(math.radians(az)), math.sin(math.radians(az)), .45))
        pts = cn.path(s, d, L, .1, rnd, .08, .03)
        limbs.append((pts, cn.taper(len(pts), .1 - k * .008, .03), None))
        pads.append((pts[-1] + Vector((0, 0, .1)), 1.0))
        if L > 1.2:
            j = len(pts) // 2
            sd = (d + Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), .5))).normalized()
            sp = cn.path(pts[j], sd, .7, .1, rnd, .1, .05)
            limbs.append((sp, cn.taper(len(sp), .035, .015), None))
            pads.append((sp[-1] + Vector((0, 0, .08)), .7))
    for h, az in ((1.1, 60), (1.7, 230), (2.05, 330)):  # 아래쪽 말라 부러진 가지
        s = trunk[int(h / H * 47)]
        d = Vector((math.cos(math.radians(az)), math.sin(math.radians(az)), .1))
        pts = cn.path(s, d, rnd.uniform(.25, .45), .08, rnd, .05)
        limbs.append((pts, cn.taper(len(pts), .035, .012), None))
    return limbs, pads


def make_pine(name):
    limbs, pads = pine_skeleton()
    top = max(c.z for c, r in pads) + .95
    f = 5.98 / top

    def cards_fn(rects, f_=None):
        if rects == "shots":
            out = []
            for view in ('top', 'side'):
                col, nrm, span = render_card("pine_sapling_small", "pine_sapling_small_a", view)
                col, nrm, box = clean_card(col, nrm)
                out.append((col, nrm, box, span))
            return out
        rnd = random.Random(9); cards = Cards()
        (tuv, (tw, th)), (suv, (sw, sh)) = rects['top'], rects['side']
        for c, r in pads:
            c = c * f_; r = r * f_
            n_top = int(62 * r)
            for k in range(n_top):  # 위에서 본 솔잎 덩이: 납작한 층으로 겹쳐 쌓는다
                a = rnd.uniform(0, 2 * math.pi); rr = r * math.sqrt(rnd.random()) * .75
                p = c + Vector((math.cos(a) * rr, math.sin(a) * rr * .9, rnd.uniform(-.18, .22) * r))
                nrm = Vector((rnd.gauss(0, .25), rnd.gauss(0, .25), 1)).normalized()
                R = nrm.cross(Vector((math.cos(a + rnd.uniform(0, 3)), math.sin(a + 1), 0))).normalized()
                U = nrm.cross(R)
                s = rnd.uniform(.75, 1.05) * r * .95
                cards.add(p, R, U, s, s * th / tw, tuv, c + Vector((0, 0, -.3 * r)))
            for k in range(int(30 * r)):  # 옆으로 뻗은 솔잎 가지: 가장자리를 들쭉날쭉하게
                a = rnd.uniform(0, 2 * math.pi)
                out = Vector((math.cos(a), math.sin(a), rnd.uniform(-.15, .35))).normalized()
                h = rnd.uniform(.55, .85) * r
                w = h * sw / sh
                p = c + out * (r * .45 + h * .35) + Vector((0, 0, rnd.uniform(-.1, .12) * r))
                side = Vector((0, 0, 1)).cross(out).normalized()
                tilt = rnd.uniform(-.9, .9)
                R = (side * math.cos(tilt) + Vector((0, 0, 1)) * math.sin(tilt)).normalized()
                cards.add(p, R, out, w, h, suv, c + Vector((0, 0, -.3 * r)))
        return cards

    def wood_fn():
        wh, wl = wood_part(limbs, 5, .04, f)
        return wh, wl, f

    leafy(name, cards_fn, wood_fn)


# ---------------------------------------------------------------- 덤불(실제 관목 잎)
def make_bush(name):
    def cards_fn(rects, f_=None):
        if rects == "shots":
            out = []
            for view in ('side', 'top'):
                col, nrm, span = render_card("shrub_04", None, view)
                col, nrm, box = clean_card(col, nrm)
                out.append((col, nrm, box, span))
            return out  # 첫째(옆 모습)를 'top' 칸에, 둘째를 'side' 칸에 둔다
        rnd = random.Random(4); cards = Cards()
        (auv, (aw, ah)), (buv, (bw, bh)) = rects['top'], rects['side']
        center = Vector((0, 0, .2))
        for k in range(150):
            u = rnd.uniform(0, 2 * math.pi); v = math.acos(rnd.uniform(-.2, 1))
            d = Vector((math.sin(v) * math.cos(u), math.sin(v) * math.sin(u), math.cos(v)))
            p = Vector((d.x * .4, d.y * .36, max(d.z, 0) * .26 + .14)) * rnd.uniform(.5, 1.0)
            p.z += .08
            uvr, (cw, ch) = ((auv, (aw, ah)) if k % 3 else (buv, (bw, bh)))
            s = rnd.uniform(.6, .95)
            w = cw * s; h = ch * s
            R = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, .4))).normalized()
            U = (d + Vector((0, 0, .6))).normalized()
            U = (U - R * U.dot(R)).normalized()
            cards.add(p, R, U, w, h, uvr, center, .7)
        return cards

    def wood_fn():
        rnd = random.Random(3); limbs = []
        for k in range(9):
            a = rnd.uniform(0, 2 * math.pi)
            s = Vector((math.cos(a) * .06, math.sin(a) * .06, 0))
            pts = cn.path(s, Vector((math.cos(a), math.sin(a), 1.6)), rnd.uniform(.3, .5), .07, rnd, .12)
            limbs.append((pts, cn.taper(len(pts), .009, .003), None))
        hi, lo = cn.woody(limbs, 3, hi_sides=6, lo_sides=4, lo_step=.2, depth=.02)
        wh = cn.mesh_obj("wood_hi", *cn.merge(hi)); wl = cn.mesh_obj("wood_lo", *cn.merge(lo))
        return wh, wl, 1.0

    leafy(name, cards_fn, wood_fn)


def make_dead(name):
    cn.make_dead(name)  # 뼈대·굽기는 그대로, 껍질만 실측(scan_bark, 그을음)


MAKERS = {"BG_RockA": make_rock, "BG_RockB": make_rock, "BG_RockC": make_rock,
          "BG_Pine": make_pine, "BG_DeadTree": make_dead, "BG_Bush": make_bush}
for nm in (MAKERS if which == "all" else [which]):
    cn.reset()
    MAKERS[nm](nm)
