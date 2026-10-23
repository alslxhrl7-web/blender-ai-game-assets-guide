# 배경 나무·바위를 블렌더에서 직접 깎는다(바르코 모델 대신). 1950년 8월 경북 산지(다부동) 기준.
# 쓰는 법: blender -b --python carve_nature.py -- <이름|all> <작업 폴더(ArtSource)>
#   이름: BG_Pine(적송) BG_DeadTree(포탄 맞은 고사목) BG_Bush(관목) BG_RockA/B/C(화강암 바위)
# 하는 일: 고해상도로 깎기(껍질 골·바위 결·솔잎 뭉치) → 같은 모양의 게임용 저해상도 → 저해상도에 펼친 UV 하나
#          → 고해상도의 색·굴곡을 구워 넣기(BaseColor·Normal, 나무는 그늘 AO 를 색에 곱함)
#          → Export/<이름>.fbx + T_<이름>_BaseColor.jpg/_Normal.jpg + Blender/<이름>.blend + Preview/<이름>_persp.png
# 크기·원점은 예전 바르코 모델과 같게 맞춘다(원점 = 바닥 가운데, 정면 -Y). 그래서 유니티 씬은 다시 배치하지 않아도 된다.
import bpy, bmesh, sys, os, math, random
import numpy as np
from mathutils import Vector, Matrix, noise

a = sys.argv[sys.argv.index("--") + 1:]
which, root = a[0], a[1]
for d in ("Export", "Preview", "Blender"): os.makedirs(os.path.join(root, d), exist_ok=True)


# ---------------------------------------------------------------- 공통
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = 4
    sc.view_settings.view_transform = 'Standard'
    sc.render.bake.margin = 8


def mesh_obj(name, verts, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.validate(); me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    for p in me.polygons: p.use_smooth = True
    return o


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = o.data.name = name
    return o


def tris(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


def bounds(o):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])


def frames(pts):
    """선을 따라 비틀리지 않는 좌표틀(평행 이동)."""
    ts = []
    for i in range(len(pts)):
        t = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
        ts.append(t.normalized())
    n = ts[0].cross(Vector((0, 0, 1)) if abs(ts[0].z) < .9 else Vector((1, 0, 0))).normalized()
    out = []
    for i, t in enumerate(ts):
        if i: n = (n - t * n.dot(t)).normalized()
        out.append((t, n, t.cross(n)))
    return out


def tube(pts, radii, sides, rfun=None, cap=True, tip=None):
    """줄기·가지 하나. rfun(위치, 바깥 방향, 기본 반지름) → 실제 반지름(껍질 골)."""
    verts, faces = [], []
    fr = frames(pts)
    for i, p in enumerate(pts):
        t, n, b = fr[i]
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            d = n * math.cos(ang) + b * math.sin(ang)
            r = radii[i] if rfun is None else rfun(p, d, radii[i])
            verts.append(p + d * r)
    for i in range(len(pts) - 1):
        for k in range(sides):
            a0, a1 = i * sides + k, i * sides + (k + 1) % sides
            faces.append((a0, a1, a1 + sides, a0 + sides))
    if cap:
        last = (len(pts) - 1) * sides
        verts.append(tip if tip is not None else pts[-1] + fr[-1][0] * radii[-1] * .6)
        c = len(verts) - 1
        for k in range(sides): faces.append((last + k, last + (k + 1) % sides, c))
    return verts, faces


def blob(center, rx, ry, rz, subdiv, rfun):
    """잎·솔잎 뭉치·바위의 바탕: 이코스피어를 rfun(단위 방향) 배로 부풀린다."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1)
    verts = [v.co.copy() for v in bm.verts]
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    out = []
    for d in verts:
        d = d.normalized()
        r = rfun(d)
        out.append(center + Vector((d.x * rx * r, d.y * ry * r, d.z * rz * r)))
    return out, faces


def merge(parts):
    V, F = [], []
    for v, f in parts:
        o = len(V); V += v; F += [tuple(i + o for i in ff) for ff in f]
    return V, F


# ---------------------------------------------------------------- 재질(굽기 전용, Cycles 노드)
def node_mat(name, build):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes): nt.nodes.remove(n)
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    build(nt, bsdf)
    return m


def N(nt, kind, **kw):
    n = nt.nodes.new(kind)
    for k, v in kw.items():
        if k in n.inputs: n.inputs[k].default_value = v
        else: setattr(n, k, v)
    return n


def lin(c):
    return tuple(x ** 2.2 if 0 <= x <= 1 else x for x in c)


def ramp(nt, src, stops):
    r = nt.nodes.new('ShaderNodeValToRGB')
    el = r.color_ramp.elements
    while len(el) > 1: el.remove(el[-1])
    el[0].position, el[0].color = stops[0][0], (*lin(stops[0][1]), 1)
    for pos, col in stops[1:]:
        e = el.new(pos); e.color = (*lin(col), 1)
    nt.links.new(src, r.inputs[0])
    return r


def mix(nt, fac, c1, c2):
    m = nt.nodes.new('ShaderNodeMix'); m.data_type = 'RGBA'
    nt.links.new(fac, m.inputs[0])
    for sock, c in ((m.inputs[6], c1), (m.inputs[7], c2)):
        if isinstance(c, tuple): sock.default_value = (*lin(c), 1)
        else: nt.links.new(c, sock)
    return m.outputs[2]


def coord(nt, scale=(1, 1, 1)):
    tc = N(nt, 'ShaderNodeTexCoord')
    mp = N(nt, 'ShaderNodeMapping')
    mp.inputs['Scale'].default_value = scale
    nt.links.new(tc.outputs['Object'], mp.inputs[0])
    return mp.outputs[0]


def normal_z(nt):
    g = N(nt, 'ShaderNodeNewGeometry')
    s = N(nt, 'ShaderNodeSeparateXYZ')
    nt.links.new(g.outputs['Normal'], s.inputs[0])
    return s.outputs['Z']


def height(nt, lo, hi):
    """물체 높이 z 를 lo~hi 에서 0~1 로."""
    tc = N(nt, 'ShaderNodeTexCoord')
    s = N(nt, 'ShaderNodeSeparateXYZ')
    nt.links.new(tc.outputs['Object'], s.inputs[0])
    mr = N(nt, 'ShaderNodeMapRange')
    mr.inputs['From Min'].default_value, mr.inputs['From Max'].default_value = lo, hi
    nt.links.new(s.outputs['Z'], mr.inputs[0])
    return mr.outputs[0]


def granite(nt, bsdf, tint=(1, 1, 1)):
    def t(c): return tuple(c[i] * tint[i] for i in range(3))
    co = coord(nt)
    big = N(nt, 'ShaderNodeTexNoise', **{'Scale': 2.2, 'Detail': 8.0, 'Roughness': .6})
    nt.links.new(co, big.inputs['Vector'])
    base = ramp(nt, big.outputs['Fac'], [(.3, t((.36, .34, .31))), (.5, t((.52, .50, .46))), (.72, t((.66, .63, .58)))])
    # 화강암 알갱이(검은 운모·흰 장석)
    grain = N(nt, 'ShaderNodeTexVoronoi', **{'Scale': 55.0})
    nt.links.new(co, grain.inputs['Vector'])
    gw = ramp(nt, grain.outputs['Distance'], [(.0, (1, 1, 1)), (.18, (0, 0, 0))])
    mul = N(nt, 'ShaderNodeMath', operation='MULTIPLY')
    nt.links.new(gw.outputs[0], mul.inputs[0]); mul.inputs[1].default_value = .35
    c = mix(nt, mul.outputs[0], base.outputs[0], (.6, .58, .55))
    dark = N(nt, 'ShaderNodeTexVoronoi', **{'Scale': 34.0})
    nt.links.new(co, dark.inputs['Vector'])
    dk = ramp(nt, dark.outputs['Distance'], [(.0, (1, 1, 1)), (.12, (0, 0, 0))])
    c = mix(nt, dk.outputs[0], c, (.07, .065, .06))
    # 이끼·지의류: 위를 보는 면에만 얼룩으로
    ln = N(nt, 'ShaderNodeTexNoise', **{'Scale': 6.0, 'Detail': 6.0})
    nt.links.new(co, ln.inputs['Vector'])
    lr = ramp(nt, ln.outputs['Fac'], [(.55, (0, 0, 0)), (.62, (1, 1, 1))])
    up = ramp(nt, normal_z(nt), [(.25, (0, 0, 0)), (.7, (1, 1, 1))])
    lm = N(nt, 'ShaderNodeMath', operation='MULTIPLY')
    nt.links.new(lr.outputs[0], lm.inputs[0]); nt.links.new(up.outputs[0], lm.inputs[1])
    c = mix(nt, lm.outputs[0], c, (.62, .62, .52))
    # 틈의 어둠(뾰족함 Pointiness)과 바닥의 흙
    g = N(nt, 'ShaderNodeNewGeometry')
    cav = ramp(nt, g.outputs['Pointiness'], [(.45, (.45, .45, .45)), (.5, (.92, .92, .92)), (.55, (1, 1, 1))])
    cm = N(nt, 'ShaderNodeMix'); cm.data_type = 'RGBA'; cm.blend_type = 'MULTIPLY'; cm.inputs[0].default_value = 1
    nt.links.new(c, cm.inputs[6]); nt.links.new(cav.outputs[0], cm.inputs[7]); c = cm.outputs[2]
    dirt = ramp(nt, height(nt, 0, .32), [(.0, (1, 1, 1)), (1, (0, 0, 0))])
    c = mix(nt, dirt.outputs[0], c, (.23, .18, .13))
    nt.links.new(c, bsdf.inputs['Base Color'])


def bark(nt, bsdf, red=True, charred=False):
    co = coord(nt, (7, 7, 1.6))
    cr = N(nt, 'ShaderNodeTexVoronoi', feature='DISTANCE_TO_EDGE', **{'Scale': 1.0})
    nt.links.new(co, cr.inputs['Vector'])
    crack = ramp(nt, cr.outputs['Distance'], [(.0, (0, 0, 0)), (.07, (1, 1, 1))])
    nz = N(nt, 'ShaderNodeTexNoise', **{'Scale': 9.0, 'Detail': 8.0})
    nt.links.new(coord(nt), nz.inputs['Vector'])
    if charred:
        plate = ramp(nt, nz.outputs['Fac'], [(.35, (.035, .03, .028)), (.6, (.11, .095, .08))])
        # 껍질이 벗겨진 회색 속나무
        pe = N(nt, 'ShaderNodeTexNoise', **{'Scale': 3.0, 'Detail': 4.0})
        nt.links.new(coord(nt, (2, 2, .6)), pe.inputs['Vector'])
        pr = ramp(nt, pe.outputs['Fac'], [(.58, (0, 0, 0)), (.62, (1, 1, 1))])
        wood = ramp(nt, nz.outputs['Fac'], [(.3, (.27, .25, .22)), (.7, (.42, .39, .34))])
        c = mix(nt, pr.outputs[0], plate.outputs[0], wood.outputs[0])
        ember = ramp(nt, nz.outputs['Fac'], [(.66, (0, 0, 0)), (.7, (1, 1, 1))])
        c = mix(nt, ember.outputs[0], c, (.26, .12, .05))
    else:
        low = ramp(nt, nz.outputs['Fac'], [(.3, (.17, .13, .10)), (.65, (.30, .24, .19))])
        c = low.outputs[0]
        if red:  # 적송: 위로 갈수록 붉은 주황 껍질
            hi = ramp(nt, nz.outputs['Fac'], [(.3, (.42, .19, .10)), (.65, (.62, .33, .19))])
            up = ramp(nt, height(nt, 1.6, 3.6), [(0, (0, 0, 0)), (1, (1, 1, 1))])
            c = mix(nt, up.outputs[0], c, hi.outputs[0])
    m = N(nt, 'ShaderNodeMix'); m.data_type = 'RGBA'; m.blend_type = 'MULTIPLY'; m.inputs[0].default_value = 1
    nt.links.new(c, m.inputs[6]); nt.links.new(ramp(nt, crack.outputs[0], [(0, (.4, .4, .4)), (1, (1, 1, 1))]).outputs[0], m.inputs[7])
    c = m.outputs[2]
    dirt = ramp(nt, height(nt, 0, .25), [(.0, (1, 1, 1)), (1, (0, 0, 0))])
    c = mix(nt, dirt.outputs[0], c, (.2, .16, .12))
    nt.links.new(c, bsdf.inputs['Base Color'])


def needles(nt, bsdf, leafy=False):
    co = coord(nt)
    nz = N(nt, 'ShaderNodeTexNoise', **{'Scale': 4.0, 'Detail': 6.0})
    nt.links.new(co, nz.inputs['Vector'])
    if leafy:
        cells = N(nt, 'ShaderNodeTexVoronoi', **{'Scale': 70.0})
        nt.links.new(co, cells.inputs['Vector'])
        base = ramp(nt, cells.outputs['Color'], [(.0, (.17, .25, .08)), (.5, (.25, .34, .11)), (1, (.36, .43, .16))])
        c = base.outputs[0]
        yel = ramp(nt, nz.outputs['Fac'], [(.7, (0, 0, 0)), (.76, (.6, .6, .6))])
        c = mix(nt, yel.outputs[0], c, (.38, .34, .13))
    else:
        st = N(nt, 'ShaderNodeTexNoise', **{'Scale': 60.0, 'Detail': 2.0})
        nt.links.new(co, st.inputs['Vector'])
        base = ramp(nt, st.outputs['Fac'], [(.35, (.10, .17, .07)), (.5, (.19, .29, .11)), (.65, (.32, .40, .16))])
        c = base.outputs[0]
        dead = ramp(nt, nz.outputs['Fac'], [(.66, (0, 0, 0)), (.7, (1, 1, 1))])
        c = mix(nt, dead.outputs[0], c, (.34, .22, .09))
    # 밝고 어두움은 고해상도 면 방향 대신 저해상도에서 구운 그늘(AO)로 준다(잔 굴곡마다 뒤집혀 얼룩지지 않게)
    nt.links.new(c, bsdf.inputs['Base Color'])


# ---------------------------------------------------------------- 굽기
def bake(high, low, name, size, extrusion, ray, ao=0.0, ao_dist=.8):
    sc = bpy.context.scene
    low.data.materials.clear()
    m = bpy.data.materials.new("M_" + name); m.use_nodes = True
    low.data.materials.append(m)
    nt = m.node_tree
    img_c = bpy.data.images.new("T_%s_BaseColor" % name, size, size, alpha=False)
    img_n = bpy.data.images.new("T_%s_Normal" % name, size, size, alpha=False)
    img_n.colorspace_settings.name = 'Non-Color'
    tex = nt.nodes.new('ShaderNodeTexImage')

    def target(img):
        tex.image = img; nt.nodes.active = tex; tex.select = True

    bpy.ops.object.select_all(action='DESELECT')
    high.hide_render = False
    high.select_set(True); low.select_set(True); bpy.context.view_layer.objects.active = low
    kw = dict(use_selected_to_active=True, cage_extrusion=extrusion, max_ray_distance=ray, margin=8)
    target(img_c); sc.cycles.samples = 4
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, **kw)
    target(img_n)
    bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', **kw)
    if ao > 0:  # 저해상도 자신의 그늘(잎 뭉치 안쪽·가지 밑)을 색에 곱한다
        img_a = bpy.data.images.new("ao", size, size, alpha=False); img_a.colorspace_settings.name = 'Non-Color'
        target(img_a)
        high.hide_render = True
        if sc.world is None: sc.world = bpy.data.worlds.new("W")
        sc.world.light_settings.distance = ao_dist
        sc.cycles.samples = 48
        bpy.ops.object.select_all(action='DESELECT'); low.select_set(True)
        bpy.ops.object.bake(type='AO', margin=8)
        c = np.empty(size * size * 4, np.float32); o = np.empty_like(c)
        img_c.pixels.foreach_get(c); img_a.pixels.foreach_get(o)
        c = c.reshape(-1, 4); f = 1 - ao + ao * o.reshape(-1, 4)[:, :1]
        c[:, :3] *= f
        img_c.pixels.foreach_set(c.ravel())
        bpy.data.images.remove(img_a)
        high.hide_render = False
    for img, path in ((img_c, "T_%s_BaseColor.jpg" % name), (img_n, "T_%s_Normal.jpg" % name)):
        img.filepath_raw = os.path.join(root, "Export", path); img.file_format = 'JPEG'; img.save()
    # 미리보기·유니티와 같은 연결: 색 + 법선
    bsdf = nt.nodes['Principled BSDF']
    tex.image = img_c
    nt.links.new(tex.outputs[0], bsdf.inputs['Base Color'])
    tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = img_n
    nm = nt.nodes.new('ShaderNodeNormalMap')
    nt.links.new(tn.outputs[0], nm.inputs['Color']); nt.links.new(nm.outputs[0], bsdf.inputs['Normal'])
    bsdf.inputs['Roughness'].default_value = .9
    return m


def finish_low(low):
    """삼각형으로 나누고(유니티와 같은 대각선) UV 하나 펼치기."""
    bpy.ops.object.select_all(action='DESELECT')
    low.select_set(True); bpy.context.view_layer.objects.active = low
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.quads_convert_to_tris()
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=.006)
    bpy.ops.object.mode_set(mode='OBJECT')
    for p in low.data.polygons: p.use_smooth = True


def export(low, high, name):
    mn, mx = bounds(low)
    print("REPORT", name, "tris(game)", tris(low), "tris(carved)", tris(high), "size(m) X/Y/Z", tuple(round(v, 2) for v in (mx - mn)))
    high.name = name + "_high"; high.hide_viewport = True; high.hide_render = True
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root, "Blender", name + ".blend"))
    bpy.ops.object.select_all(action='DESELECT'); low.select_set(True)
    bpy.context.view_layer.objects.active = low
    # clean_prop.py 와 같은 규격: 블렌더 -Y 정면·+Z 위 → 유니티 +Z 정면·+Y 위
    bpy.ops.export_scene.fbx(filepath=os.path.join(root, "Export", name + ".fbx"), use_selection=True, object_types={'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')
    preview(low, name)


def preview(low, name):
    sc = bpy.context.scene
    try: sc.render.engine = 'BLENDER_EEVEE'
    except TypeError: sc.render.engine = 'BLENDER_EEVEE_NEXT'
    sc.render.resolution_x, sc.render.resolution_y = 1000, 1000
    sc.view_settings.view_transform = 'AgX'
    w = sc.world or bpy.data.worlds.new("W"); sc.world = w
    try: w.use_nodes = True
    except AttributeError: pass
    if w.node_tree:
        bg = w.node_tree.nodes.get('Background')
        if bg: bg.inputs[0].default_value = (.55, .6, .66, 1); bg.inputs[1].default_value = .6
    else: w.color = (.55, .6, .66)
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3.0; sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    sc.collection.objects.link(sun)
    gnd = bpy.data.meshes.new("g"); gnd.from_pydata([(-30, -30, 0), (30, -30, 0), (30, 30, 0), (-30, 30, 0)], [], [(0, 1, 2, 3)])
    go = bpy.data.objects.new("Ground", gnd); sc.collection.objects.link(go)
    gm = bpy.data.materials.new("gm"); gm.use_nodes = True
    gm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (.3, .25, .19, 1); gnd.materials.append(gm)
    mn, mx = bounds(low)
    size = max(mx - mn); c = (mn + mx) / 2
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 50
    dist = size * 2.3
    cam.location = c + Vector((dist * .62, -dist * .78, size * .25))
    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(root, "Preview", name + "_persp.png")
    bpy.ops.render.render(write_still=True)
    for o in (sun, go, cam): bpy.data.objects.remove(o)


# ---------------------------------------------------------------- 바위(화강암)
ROCKS = {  # 씨앗, 크기(m, X·Y·Z), 깎는 면 수, 각진 정도
    "BG_RockA": (11, (1.5, 1.42, 1.0), 8, .8),   # 둥근 화강암 덩이
    "BG_RockB": (27, (1.5, 1.38, 1.05), 11, 1.0),  # 모가 난 바위(쪼개진 면)
    "BG_RockC": (43, (1.5, 1.3, .9), 9, .9),      # 납작하고 금이 간 바위
}


def make_rock(name):
    seed, (sx, sy, sz), cuts, hard = ROCKS[name]
    rnd = random.Random(seed)
    off = Vector([rnd.uniform(-50, 50) for _ in range(3)])
    planes = []
    for _ in range(cuts):
        n = Vector((rnd.gauss(0, 1), rnd.gauss(0, 1), rnd.gauss(0, .7) + .25)).normalized()
        planes.append((n, rnd.uniform(.5, .74)))

    def shape(d, fine):
        p = d * (1 + .12 * noise.fractal(d * 1.1 + off, .55, 2.0, 3))
        for n, dd in planes:  # 쪼개진 평면
            t = p.dot(n) - dd
            if t > 0: p = p - n * t * hard
        p = p * (1 + .05 * noise.fractal(p * 2.6 + off, .7, 2.0, 2))  # 중간 크기 울퉁불퉁
        if fine:
            # 절리(금): 큰 보로노이 경계 중 일부(가림 잡음이 높은 곳)만 가늘고 깊게 — 거북 등처럼 온통 갈라지지 않게
            dist, _ = noise.voronoi(p * 1.15 + off, distance_metric='DISTANCE')
            edge = dist[1] - dist[0]
            mask = noise.noise(p * 1.3 + off * .5)
            if edge < .035 and mask > .1: p = p * (1 - .03 * (1 - edge / .035) * min((mask - .1) / .2, 1))
            p = p * (1 + .012 * noise.fractal(p * 9 + off, .6, 2.0, 4) + .004 * noise.noise(p * 40 + off))
        return p

    def build(subdiv, fine):
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1)
        V = [shape(v.co.normalized(), fine) for v in bm.verts]
        F = [tuple(v.index for v in f.verts) for f in bm.faces]; bm.free()
        return V, F

    V, F = build(7, True)
    # 크기·바닥 평탄화
    mnz = min(v.z for v in V)
    zcut = mnz + .22 * (max(v.z for v in V) - mnz)
    V = [Vector((v.x, v.y, zcut + (v.z - zcut) * .25 if v.z < zcut else v.z)) for v in V]
    mn = Vector([min(v[i] for v in V) for i in range(3)]); mx = Vector([max(v[i] for v in V) for i in range(3)])
    k = Vector((sx / (mx.x - mn.x), sy / (mx.y - mn.y), sz / (mx.z - mn.z)))
    ctr = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
    V = [Vector(((v.x - ctr.x) * k.x, (v.y - ctr.y) * k.y, (v.z - ctr.z) * k.z)) for v in V]
    high = mesh_obj(name + "_high", V, F)
    high.data.materials.append(node_mat("Granite", lambda nt, b: granite(nt, b)))
    low = high.copy(); low.data = high.data.copy(); low.name = low.data.name = name
    bpy.context.scene.collection.objects.link(low)
    d = low.modifiers.new("D", 'DECIMATE'); d.ratio = 1500 / tris(high); d.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = low
    bpy.ops.object.modifier_apply(modifier=d.name)
    finish_low(low)
    bake(high, low, name, 2048, .04, .1)
    export(low, high, name)


# ---------------------------------------------------------------- 나무
def bark_r(seed, depth=.07):
    """껍질 골: 세로로 길쭉한 판과 그 사이 깊은 골."""
    off = Vector((seed * 7.1, seed * 3.3, seed * 1.7))

    def f(p, d, r):
        q = p + d * r
        s = Vector((q.x * 6, q.y * 6, q.z * 1.4)) + off
        dist, _ = noise.voronoi(s, distance_metric='DISTANCE')
        edge = min((dist[1] - dist[0]) / .12, 1)
        return r * (1 - depth * (1 - edge) + .03 * noise.noise(q * 14 + off))
    return f


def path(start, direction, length, step, rnd, wiggle=.12, lift=0.0):
    pts = [start.copy()]
    d = direction.normalized()
    n = max(2, int(length / step))
    for i in range(n):
        d = (d + Vector((rnd.gauss(0, wiggle), rnd.gauss(0, wiggle), rnd.gauss(0, wiggle) + lift))).normalized()
        pts.append(pts[-1] + d * (length / n))
    return pts


def taper(n, r0, r1, flare=0.0):
    out = []
    for i in range(n):
        t = i / max(n - 1, 1)
        r = r0 + (r1 - r0) * t
        if flare and t < .12: r *= 1 + flare * (1 - t / .12) ** 2
        out.append(r)
    return out


def resample(pts, step):
    """같은 선을 더 성기게(저해상도)."""
    L = [0]
    for i in range(1, len(pts)): L.append(L[-1] + (pts[i] - pts[i - 1]).length)
    n = max(2, int(L[-1] / step) + 1)
    out, j = [], 0
    for k in range(n):
        s = L[-1] * k / (n - 1)
        while j < len(L) - 2 and L[j + 1] < s: j += 1
        t = (s - L[j]) / max(L[j + 1] - L[j], 1e-6)
        out.append(pts[j].lerp(pts[j + 1], t))
    return out


def radii_at(pts_hi, r_hi, pts_lo):
    out = []
    for p in pts_lo:
        i = min(range(len(pts_hi)), key=lambda k: (pts_hi[k] - p).length)
        out.append(r_hi[i])
    return out


def woody(limbs, seed, hi_sides=18, lo_sides=7, lo_step=.38, depth=.07, jag=None):
    """limbs: [(점들, 반지름들, 끝 막음점 또는 None)] → (고해상도 부품, 저해상도 부품)"""
    rf = bark_r(seed, depth)
    hi, lo = [], []
    for k, (pts, rr, tip) in enumerate(limbs):
        hi.append(tube(pts, rr, hi_sides if rr[0] > .05 else 9, rf, True, tip))
        lp = resample(pts, lo_step if rr[0] > .05 else lo_step * 1.4)
        lo.append(tube(lp, radii_at(pts, rr, lp), lo_sides if rr[0] > .05 else 4, None, True, tip))
    return hi, lo


def clump_r(seed, rough, tuft):
    off = Vector((seed * 5.3, seed * 2.1, seed * 9.7))

    def lo(d):
        return 1 + rough * noise.fractal(d * 1.6 + off, .5, 2.0, 3) + tuft * .45

    def hi(d):
        t = abs(noise.noise(d * 7 + off)) + .5 * abs(noise.noise(d * 17 + off))
        return lo(d) - tuft * .45 + tuft * t + .03 * noise.noise(d * 40 + off)
    return lo, hi


def make_pine(name):
    """적송(소나무): 몸통이 비스듬히 휘고 위쪽 껍질이 붉으며, 가지 끝에 납작한 솔잎 뭉치가 층층이 얹힌다."""
    rnd = random.Random(5)
    trunk = []
    H = 4.75
    for i in range(48):
        t = i / 47
        trunk.append(Vector((.55 * math.sin(t * 2.4) - .1 * t, .18 * math.sin(t * 4.1), H * t)))
    limbs = [(trunk, taper(48, .25, .08, flare=.7), None)]
    tips = [trunk[-1] + Vector((0, 0, .1))]
    for k, (h, az, L) in enumerate([(2.55, 20, 1.6), (2.9, 145, 1.45), (3.25, 260, 1.5), (3.6, 80, 1.15), (3.85, 200, 1.05), (4.15, 320, .95)]):
        i = int(h / H * 47); s = trunk[i]
        d = Vector((math.cos(math.radians(az)), math.sin(math.radians(az)), .45))
        pts = path(s, d, L, .1, rnd, .08, .03)
        limbs.append((pts, taper(len(pts), .1 - k * .008, .03), None))
        tips.append(pts[-1])
        if L > 1.2:  # 곁가지
            j = len(pts) // 2
            sd = (d + Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), .5))).normalized()
            sp = path(pts[j], sd, .7, .1, rnd, .1, .05)
            limbs.append((sp, taper(len(sp), .03, .014), None))
            tips.append(sp[-1])
    for h, az in ((1.1, 60), (1.7, 230), (2.05, 330)):  # 아래쪽 말라 부러진 가지 그루터기
        s = trunk[int(h / H * 47)]
        d = Vector((math.cos(math.radians(az)), math.sin(math.radians(az)), .1))
        pts = path(s, d, rnd.uniform(.25, .45), .08, rnd, .05)
        limbs.append((pts, taper(len(pts), .035, .012), None))
    hi, lo = woody(limbs, 5, depth=.09)
    wood_hi = mesh_obj("wood_hi", *merge(hi)); wood_lo = mesh_obj("wood_lo", *merge(lo))
    wood_hi.data.materials.append(node_mat("Bark", lambda nt, b: bark(nt, b, red=True)))
    # 솔잎 뭉치: 가지 끝마다 납작한 덩이 1~2개
    fh, fl = [], []
    for k, t in enumerate(tips):
        for j in range(2 if k in (0, 1, 2, 3) else 1):
            c = t + Vector((rnd.uniform(-.25, .25), rnd.uniform(-.25, .25), .12 + j * .2))
            rx, ry, rz = rnd.uniform(.7, 1.0), rnd.uniform(.6, .9), rnd.uniform(.3, .42)
            if k == 0: rx, ry, rz = rx * 1.15, ry * 1.15, rz * 1.1
            lof, hif = clump_r(k * 10 + j, .28, .22)
            fh.append(blob(c, rx, ry, rz, 5, hif)); fl.append(blob(c, rx, ry, rz, 3, lof))
    fol_hi = mesh_obj("fol_hi", *merge(fh)); fol_lo = mesh_obj("fol_lo", *merge(fl))
    fol_hi.data.materials.append(node_mat("Needles", lambda nt, b: needles(nt, b)))
    tree(name, wood_hi, fol_hi, wood_lo, fol_lo, height=5.98, ao=.55)


def make_dead(name):
    """포탄에 맞아 윗부분이 부러지고 그을린 고사목. 가지는 짧게 부러진 채 남았다."""
    rnd = random.Random(8)
    H = 4.55
    trunk = [Vector((.12 * math.sin(i / 40 * 3), .08 * math.sin(i / 40 * 5), H * i / 40)) for i in range(41)]
    # 쪼개진 꼭대기: 끝 고리 대신 들쭉날쭉한 가시로 닫는다
    limbs = [(trunk, taper(41, .3, .17, flare=.6), trunk[-1] + Vector((.05, .02, .42)))]
    for k in range(11):
        h = rnd.uniform(1.6, 4.2); az = rnd.uniform(0, 360)
        s = trunk[int(h / H * 40)]
        d = Vector((math.cos(math.radians(az)), math.sin(math.radians(az)), rnd.uniform(.1, .6)))
        L = rnd.uniform(.7, 1.6)
        pts = path(s, d, L, .08, rnd, .14, -.02)
        limbs.append((pts, taper(len(pts), rnd.uniform(.07, .1), .02), None))
        if L > 1.0:
            sp = path(pts[len(pts) // 2], (d + Vector((0, 0, .8))).normalized(), .6, .08, rnd, .15)
            limbs.append((sp, taper(len(sp), .04, .012), None))
    hi, lo = woody(limbs, 8, lo_sides=8, lo_step=.25, depth=.11)
    # 부러진 꼭대기의 날카로운 나무 조각
    for k in range(5):
        az = k * 72 + rnd.uniform(-20, 20)
        base = trunk[-3] + Vector((math.cos(math.radians(az)) * .12, math.sin(math.radians(az)) * .12, 0))
        tip = base + Vector((math.cos(math.radians(az)) * .1, math.sin(math.radians(az)) * .1, rnd.uniform(.25, .7)))
        pts = [base, base.lerp(tip, .5), tip]
        hi.append(tube(pts, [.07, .04, .01], 6, None, True)); lo.append(tube(pts, [.07, .04, .01], 5, None, True))
    wood_hi = mesh_obj("wood_hi", *merge(hi)); wood_lo = mesh_obj("wood_lo", *merge(lo))
    wood_hi.data.materials.append(node_mat("Charred", lambda nt, b: bark(nt, b, red=False, charred=True)))
    tree(name, wood_hi, None, wood_lo, None, height=4.98, ao=.35)


def make_bush(name):
    """싸리·참나무 어린 덤불: 잎 뭉치 여러 개와 삐져나온 잔가지."""
    rnd = random.Random(3)
    fh, fl = [], []
    for k in range(8):
        a = k / 8 * 2 * math.pi + rnd.uniform(-.3, .3); rr = rnd.uniform(.12, .38)
        c = Vector((math.cos(a) * rr, math.sin(a) * rr * .9, rnd.uniform(.18, .34)))
        s = rnd.uniform(.26, .4)
        lof, hif = clump_r(k + 40, .3, .2)
        fh.append(blob(c, s, s * .95, s * .82, 5, hif)); fl.append(blob(c, s, s * .95, s * .82, 3, lof))
    fol_hi = mesh_obj("fol_hi", *merge(fh)); fol_lo = mesh_obj("fol_lo", *merge(fl))
    fol_hi.data.materials.append(node_mat("Leaves", lambda nt, b: needles(nt, b, leafy=True)))
    limbs = []
    for k in range(9):
        a = rnd.uniform(0, 2 * math.pi)
        s = Vector((math.cos(a) * .08, math.sin(a) * .08, 0))
        pts = path(s, Vector((math.cos(a), math.sin(a), 1.6)), rnd.uniform(.55, .85), .07, rnd, .12)
        limbs.append((pts, taper(len(pts), .018, .005), None))
    hi, lo = woody(limbs, 3, hi_sides=6, lo_sides=4, lo_step=.2, depth=.03)
    wood_hi = mesh_obj("wood_hi", *merge(hi)); wood_lo = mesh_obj("wood_lo", *merge(lo))
    wood_hi.data.materials.append(node_mat("Twig", lambda nt, b: bark(nt, b, red=False)))
    tree(name, wood_hi, fol_hi, wood_lo, fol_lo, height=0.74, ao=.6, size=1024, fit_xy=(1.2, 1.1))


def tree(name, wh, fh, wl, fl, height, ao, size=2048, fit_xy=None):
    high = join([o for o in (wh, fh) if o], name + "_high")
    low = join([o for o in (wl, fl) if o], name)
    # 높이(또는 폭)를 예전 모델에 맞추고 원점을 바닥 가운데(줄기 밑동)로
    mn, mx = bounds(low)
    f = height / (mx.z - mn.z)
    if fit_xy: f = min(fit_xy[0] / (mx.x - mn.x), fit_xy[1] / (mx.y - mn.y), f)
    M = Matrix.Scale(f, 4)
    for o in (high, low): o.data.transform(M)
    mn, mx = bounds(low)
    shift = Matrix.Translation(Vector((0, 0, -mn.z))) if fit_xy is None else Matrix.Translation(-Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z)))
    for o in (high, low): o.data.transform(shift)
    finish_low(low)
    bake(high, low, name, size, .07, .18, ao=ao, ao_dist=.35 if fit_xy else .9)
    export(low, high, name)


MAKERS = {"BG_Pine": make_pine, "BG_DeadTree": make_dead, "BG_Bush": make_bush,
          "BG_RockA": make_rock, "BG_RockB": make_rock, "BG_RockC": make_rock}
if __name__ == "__main__":  # realistic_nature.py 에서 불러 쓸 때는 실행하지 않는다
  for nm in (MAKERS if which == "all" else [which]):
    reset()
    MAKERS[nm](nm)
