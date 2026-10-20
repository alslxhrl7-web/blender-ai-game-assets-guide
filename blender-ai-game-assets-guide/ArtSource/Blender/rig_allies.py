# 북한군 소총수 뼈대(바르코3D Rig, 뼈 18개)와 동작 4개(Run·Cover·Fire·Death)를 아군 모델에 옮겨 붙인다.
# 쓰는 법: blender -b Enemy_NK.blend --python rig_allies.py -- <ArtSource 폴더> <이름=Characters_Bake 의 Low 메시> ...
#   예) -- C:/.../ArtSource Ally_Kim=Kim_Deokman_Low Ally_Park=Park_Yongpal_Low
# 하는 일:
#   1. 뼈대에 손 뼈(LeftHand·RightHand)를 더한다. 바르코 뼈대는 팔뚝에서 끝나서 총을 쥘 자리가 없었다.
#      → 북한군도 손 뼈를 넣어 다시 내보낸다(Enemy_NK.fbx).
#   2. 아군 저폴리 메시(8,000면, 텍스처 굽기 끝난 것)를 북한군의 기본 자세 메시에 키·위치를 맞춰 겹친다.
#      뼈 가중치는 북한군 메시에서 옮기되, 6cm 안의 같은 방향을 바라보는 면에서만 가져온다(팔 안쪽·몸통이 섞이지 않게).
#      그다음 한 점에 뼈 4개까지(Unity 한도), 합 1 로 정리한다.
#   3. 아군 메시 + 같은 뼈대 + 같은 동작 4개를 FBX 하나로 내보낸다. 텍스처는 메시 재질에 든 바르코 원본 색·법선을 JPG 로.
import bpy, bmesh, sys, os, math
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index("--") + 1:]
root, pairs = a[0], [p.split("=", 1) for p in a[1:]]
export = os.path.join(root, "Export")
os.makedirs(export, exist_ok=True)

rig = bpy.data.objects["Enemy_NK_Rig"]
enemy = bpy.data.objects["Enemy_NK"]

# ---- 1. 손 뼈 ----
bpy.context.view_layer.objects.active = rig
for o in bpy.context.view_layer.objects: o.select_set(o == rig)
bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones
for side in ("Left", "Right"):
    name = side + "Hand"
    if name in eb: continue
    fa = eb[side + "ForeArm"]
    h = eb.new(name)
    h.head = fa.tail.copy()
    h.tail = fa.tail + (fa.tail - fa.head) * 0.35
    h.roll = fa.roll
    h.parent = fa
    h.use_connect = True
bpy.ops.object.mode_set(mode='OBJECT')
print("REPORT bones", len(rig.data.bones))


def curl_hands(mesh_obj, finger=0.085, max_deg=95.0):
    """바르코 뼈대에는 손가락 뼈가 없어 손이 쫙 펴진 채로 총 주위에 떠 보인다. 쉬는 자세 메시에서 손가락을 접어 쥔 주먹으로 만든다.
    바르코 뼈대의 쉬는 자세는 팔뚝이 앞으로 뻗어 있고 메시의 팔은 아래로 늘어져 있어서, 손은 뼈가 아니라 메시에서 찾는다:
    팔뚝 가중치가 가장 큰 점 가운데 팔꿈치에서 가장 먼 쪽이 손, 그 끝 finger(m)가 손가락이다."""
    mw = mesh_obj.matrix_world; inv = mw.inverted()
    gn = {g.index: g.name for g in mesh_obj.vertex_groups}
    mid_x = sum((rig.matrix_world @ rig.data.bones[b].head_local).x for b in ("Hips", "Spine")) / 2
    moved = 0
    for side in ("Left", "Right"):
        elbow = rig.matrix_world @ rig.data.bones[side + "ForeArm"].head_local
        arm = [v for v in mesh_obj.data.vertices if v.groups and gn[max(v.groups, key=lambda g: g.weight).group] in (side + "ForeArm", side + "Hand")]
        if not arm: continue
        pw = {v.index: mw @ v.co for v in arm}
        far = sorted(arm, key=lambda v: (pw[v.index] - elbow).length)
        tip = sum((pw[v.index] for v in far[-max(5, len(far) // 20):]), Vector()) / max(5, len(far) // 20)
        d = (tip - elbow).normalized()
        amax = max((pw[v.index] - elbow).dot(d) for v in arm)
        knuck = amax - finger
        n_palm = Vector((mid_x - tip.x, 0.0, 0.0)); n_palm = (n_palm - d * n_palm.dot(d)).normalized()
        axis = d.cross(n_palm).normalized()
        pivot = elbow + d * knuck
        for v in arm:
            p = pw[v.index]; along = (p - elbow).dot(d)
            if along <= knuck: continue
            # 손가락을 마디 쪽으로 접어 짧게(35%) 하고 손바닥 쪽으로 당긴다 → 쥔 주먹 모양(구부리면 갈고리처럼 길게 늘어났다)
            rel = p - elbow; a_ = rel.dot(d); radial = rel - d * a_
            k = a_ - knuck
            q = elbow + d * (knuck + k * 0.35) + radial + n_palm * (k * 0.45)
            v.co = inv @ q; moved += 1
        print("REPORT", mesh_obj.name, side, "hand length from elbow", round(amax, 3), "m")
    print("REPORT", mesh_obj.name, "curled finger verts:", moved)


def place_hand_bones(mesh_obj):
    """손 뼈를 메시의 실제 손(쥔 주먹 가운데)으로 옮긴다. 바르코 뼈대의 쉬는 자세는 팔뚝이 앞으로 뻗어 있어
    팔뚝 끝에 둔 손 뼈가 메시 손과 떨어져 있었다(총이 손이 아닌 허리께에 떴다). 손 뼈는 가중치가 없고 팔뚝의 자식이라
    쉬는 자세의 손 위치에 두면 어떤 동작에서도 메시 손을 그대로 따라간다."""
    mw = mesh_obj.matrix_world
    gn = {g.index: g.name for g in mesh_obj.vertex_groups}
    palms = {}
    for side in ("Left", "Right"):
        elbow = rig.matrix_world @ rig.data.bones[side + "ForeArm"].head_local
        pts = [mw @ v.co for v in mesh_obj.data.vertices
               if v.groups and gn[max(v.groups, key=lambda g: g.weight).group] == side + "ForeArm"]
        pts.sort(key=lambda p: (p - elbow).length)
        far = pts[-40:]
        palms[side] = (sum(far, Vector()) / len(far), (sum(far, Vector()) / len(far) - elbow).normalized())
    inv = rig.matrix_world.inverted()
    bpy.context.view_layer.objects.active = rig
    for o in bpy.context.view_layer.objects: o.select_set(o == rig)
    bpy.ops.object.mode_set(mode='EDIT')
    for side, (palm, d) in palms.items():
        hb = rig.data.edit_bones[side + "Hand"]
        hb.use_connect = False
        hb.head = inv @ palm
        hb.tail = inv @ (palm + d * 0.06)
        print("REPORT", side + "Hand moved to palm", tuple(round(x, 3) for x in palm))
    bpy.ops.object.mode_set(mode='OBJECT')

def world_bounds(o):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = o.evaluated_get(dg)
    pts = [ev.matrix_world @ v.co for v in ev.data.vertices]
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])

def export_fbx(path, objs):
    for o in bpy.context.view_layer.objects: o.select_set(o in objs)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True, bake_anim_force_startend_keying=True,
                             path_mode='STRIP', embed_textures=False, mesh_smooth_type='FACE')

def save_jpg(src_path, dst_name, px):
    img = bpy.data.images.load(src_path)
    c = img.copy(); c.scale(px, px)
    p = os.path.join(export, dst_name)
    c.file_format = "JPEG"; c.save(filepath=p, quality=88)
    bpy.data.images.remove(c); bpy.data.images.remove(img)
    print("REPORT", dst_name, round(os.path.getsize(p) / 1024), "KB")

# 북한군도 손 뼈를 넣어 다시 내보낸다.
curl_hands(enemy)
place_hand_bones(enemy)
export_fbx(os.path.join(export, "Enemy_NK.fbx"), [rig, enemy])

# ---- 2·3. 아군 ----
rig.data.pose_position = 'REST'
emn, emx = world_bounds(enemy)
bake_blend = os.path.join(root, "Blender", "Characters_Bake.blend")
for out_name, low in pairs:
    with bpy.data.libraries.load(bake_blend) as (src, dst):
        dst.objects = [low]
    ally = dst.objects[0]
    bpy.context.scene.collection.objects.link(ally)
    ally.data = ally.data.copy()
    ally.name = ally.data.name = out_name
    ally.location = (0, 0, 0)
    bpy.context.view_layer.update()

    # 키를 북한군 기본 자세에 맞추고 발 밑·몸 가운데를 겹친다.
    amn, amx = world_bounds(ally)
    f = (emx.z - emn.z) / (amx.z - amn.z)
    ally.scale = ally.scale * f
    bpy.context.view_layer.update()
    amn, amx = world_bounds(ally)
    ally.location += Vector(((emn.x + emx.x) / 2 - (amn.x + amx.x) / 2, (emn.y + emx.y) / 2 - (amn.y + amx.y) / 2, emn.z - amn.z))
    for o in bpy.context.view_layer.objects: o.select_set(o == ally)
    bpy.context.view_layer.objects.active = ally
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # 뼈 가중치: 북한군 메시(같은 바르코 서 있는 자세, 겹쳐 보면 거의 같은 몸)에서 옮겨 온다.
    #   가까운 면만 보면 팔 안쪽처럼 몸통이 붙어 있는 자리에서 팔 점이 몸통 가중치를 받아, 팔을 들면 팔과 몸통 사이가 막처럼 찢어진다.
    #   그래서 6cm 안의 면 가운데 "면이 바라보는 방향이 같은 것"(법선 내적 > 0.3)만 골라 그 면의 무게 중심 비율로 가중치를 섞는다.
    #   (Blender 자동 가중치도 시험했지만 이 AI 메시에서는 늘어나는 면이 더 많았다.)
    from mathutils.bvhtree import BVHTree
    from mathutils.geometry import barycentric_transform
    dg = bpy.context.evaluated_depsgraph_get()
    ev = enemy.evaluated_get(dg); em = ev.to_mesh()
    ew = enemy.matrix_world
    everts = [ew @ v.co for v in em.vertices]
    epolys = [tuple(p.vertices) for p in em.polygons]
    enorm = [(ew.to_3x3() @ p.normal).normalized() for p in em.polygons]
    bvh = BVHTree.FromPolygons(everts, epolys)
    names = {g.index: g.name for g in enemy.vertex_groups}
    vw = [{names[g.group]: g.weight for g in enemy.data.vertices[i].groups} for i in range(len(enemy.data.vertices))]
    ev.to_mesh_clear()
    for g in enemy.vertex_groups:
        if g.name not in ally.vertex_groups: ally.vertex_groups.new(name=g.name)
    am = ally.data; aw = ally.matrix_world
    fallback = 0
    for v in am.vertices:
        co = aw @ v.co; n = (aw.to_3x3() @ v.normal).normalized()
        hits = bvh.find_nearest_range(co, 0.06)
        best = None
        for loc, nrm, idx, dist in sorted(hits, key=lambda h: h[3]):
            if enorm[idx].dot(n) > 0.3: best = (loc, idx); break
        if best is None:                                   # 방향이 맞는 면이 없으면 가장 가까운 면
            loc, nrm, idx, dist = bvh.find_nearest(co); best = (loc, idx); fallback += 1
        loc, idx = best
        poly = epolys[idx]
        # 다각형을 부채꼴 삼각형으로 나눠, loc 가 든 삼각형의 무게 중심 비율로 섞는다
        acc = {}
        tri = None
        for k in range(1, len(poly) - 1):
            a, b, c = everts[poly[0]], everts[poly[k]], everts[poly[k + 1]]
            bc = barycentric_transform(loc, a, b, c, Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)))
            if min(bc) > -0.05: tri = (poly[0], poly[k], poly[k + 1], bc); break
        if tri is None: tri = (poly[0], poly[1], poly[2], Vector((1 / 3, 1 / 3, 1 / 3)))
        for vi, w in zip(tri[:3], tri[3]):
            w = max(0.0, w)
            for bone, bw in vw[vi].items(): acc[bone] = acc.get(bone, 0.0) + bw * w
        tot = sum(acc.values()) or 1.0
        for bone, bw in acc.items():
            if bw / tot > 0.001: ally.vertex_groups[bone].add([v.index], bw / tot, 'REPLACE')
    # 뼈대에 붙인다.
    ally.parent = rig
    ally.matrix_parent_inverse = rig.matrix_world.inverted()
    m = ally.modifiers.new("Armature", 'ARMATURE'); m.object = rig
    # 붙은 팔 떼기: AI 로 만든 아군 메시는 한 덩어리 닫힌 면이라, 쉬는 자세에서 팔·손이 엉덩이·몸통에 면으로 붙어 있다.
    #   그 면은 어떤 가중치를 줘도 팔을 들면 막처럼 늘어나 찢어진다(북한군 메시는 바르코 자동 리깅이 이미 떼어 놓았다).
    #   점마다 가장 큰 가중치의 뼈를 정하고, 한 면 안에 뼈대에서 3칸 이상 떨어진 뼈들이 섞여 있으면(예: 팔뚝–엉덩이) 그 면을 지운다.
    parent = {b.name: (b.parent.name if b.parent else None) for b in rig.data.bones}
    def chain(b):
        out = []
        while b: out.append(b); b = parent.get(b)
        return out
    def hops(a, b):
        ca, cb = chain(a), chain(b)
        common = next((x for x in ca if x in cb), None)
        return len(ca) + len(cb) if common is None else ca.index(common) + cb.index(common)
    gname = {g.index: g.name for g in ally.vertex_groups}
    dom = {}
    for v in ally.data.vertices:
        if v.groups: dom[v.index] = gname[max(v.groups, key=lambda g: g.weight).group]
    bmb = bmesh.new(); bmb.from_mesh(ally.data)
    cut = []
    for f in bmb.faces:
        bs = {dom.get(v.index) for v in f.verts} - {None}
        if any(hops(a, b) >= 3 for a in bs for b in bs if a < b): cut.append(f)
    bmesh.ops.delete(bmb, geom=cut, context='FACES_ONLY')
    bmb.to_mesh(ally.data); bmb.free()
    print("REPORT", out_name, "bridging faces removed:", len(cut))
    # 정리: 부드럽게 → 한 점에 뼈 4개까지(Unity 한도) → 합 1.
    for o in bpy.context.view_layer.objects: o.select_set(o == ally)
    bpy.context.view_layer.objects.active = ally
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT")
    bpy.ops.object.vertex_group_smooth(group_select_mode="ALL", factor=0.5, repeat=4)  # 관절(엉덩이·어깨)에서 가중치가 급하게 바뀌지 않게. 붙은 면을 지운 뒤라 팔·몸통이 다시 섞이지 않는다
    bpy.ops.object.vertex_group_limit_total(group_select_mode="ALL", limit=4)
    bpy.ops.object.vertex_group_normalize_all(group_select_mode="ALL", lock_active=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    print("REPORT", out_name, "weights from enemy by facing faces; no facing face within 6cm:", fallback, "verts")
    tris = sum(len(p.vertices) - 2 for p in ally.data.polygons)
    amn, amx = world_bounds(ally)
    print("REPORT", out_name, "tris", tris, "height", round(amx.z - amn.z, 3), "groups", len(ally.vertex_groups))

    curl_hands(ally)
    rig.data.pose_position = 'POSE'
    export_fbx(os.path.join(export, out_name + ".fbx"), [rig, ally])
    rig.data.pose_position = 'REST'
    base = low.replace("_Low", "")
    # 텍스처: 이 저폴리 메시의 UV 에 맞는 것은 메시 재질에 들어 있는 바르코 원본 텍스처다.
    #   (URPLab_Baked 의 구운 텍스처는 UV 가 달라 옷·얼굴이 위장 무늬처럼 뒤섞여 보였다.)
    bsdf = next(n for n in ally.data.materials[0].node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    for label, sock, px in (("BaseColor", bsdf.inputs["Base Color"], 2048), ("Normal", bsdf.inputs["Normal"], 1024)):
        todo = [l.from_node for l in sock.links]; img = None
        while todo and img is None:
            nd = todo.pop()
            if nd.type == "TEX_IMAGE" and nd.image: img = nd.image
            todo.extend(l.from_node for i in nd.inputs for l in i.links)
        if img is None: print("REPORT", out_name, "텍스처 없음", label); continue
        c = img.copy(); c.scale(px, px); p = os.path.join(export, f"T_{out_name}_{label}.jpg")
        c.file_format = "JPEG"; c.save(filepath=p, quality=88); bpy.data.images.remove(c)
        print("REPORT", f"T_{out_name}_{label}.jpg from", img.name, round(os.path.getsize(p) / 1024), "KB")
    ally.hide_set(True)

rig.data.pose_position = 'POSE'
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root, "Blender", "Allies_Rigged.blend"))

# 확인 그림: 각 아군을 Fire 동작 중간 프레임에서 옆·앞으로 찍는다.
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'MATERIAL'
sc.render.resolution_x, sc.render.resolution_y = 1200, 600
cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = 2.4
cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
tracks = {t.name: t for t in rig.animation_data.nla_tracks} if rig.animation_data else {}
for out_name, _ in pairs + [("Enemy_NK", None)]:
    for o in bpy.data.objects:
        if o.type == 'MESH': o.hide_render = o.name != out_name
    for clip in ("Fire", "Cover", "Run"):
        for n, t in tracks.items(): t.mute = n != clip
        act = bpy.data.actions.get(clip)
        if act is None: continue
        sc.frame_set(int((act.frame_range[0] + act.frame_range[1]) / 2))
        for label, d in {"side": Vector((1, 0, 0)), "front": Vector((0, -1, 0))}.items():
            cam.location = Vector((0, 0, 0.85)) + d * 5
            cam.rotation_euler = (Vector((0, 0, 0.85)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = os.path.join(root, "Preview", f"{out_name}_{clip}_{label}.png")
            bpy.ops.render.render(write_still=True)
