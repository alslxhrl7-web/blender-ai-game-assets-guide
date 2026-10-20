# 게임에 들어간 인물 FBX 를 동작별로 점검한다: 텍스처를 입혀 앞·옆 모습을 찍고, 쉬는 자세 대비 면이 늘어난 정도(모서리 길이 비율)를 잰다.
# 쓰는 법: blender -b --python check_characters.py -- <출력 폴더> <이름=fbx=색텍스처> ...
# 늘어남 비율이 2 를 넘는 모서리가 많으면 뼈 가중치가 깨진 것(형태가 찢어지거나 뭉개짐)으로 본다.
import bpy, sys, os
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
out, items = a[0], [x.split("=", 2) for x in a[1:]]
os.makedirs(out, exist_ok=True)

def setup_render():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
    sc.render.resolution_x, sc.render.resolution_y = 360, 480
    cd = bpy.data.cameras.new("cam"); cd.type = 'ORTHO'; cd.ortho_scale = 2.1
    cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
    return sc, cam

for name, fbx, tex in items:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx, ignore_leaf_bones=True)
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    arm = next((o for o in bpy.data.objects if o.type == 'ARMATURE'), None)
    img = bpy.data.images.load(tex)
    m = bpy.data.materials.new("m"); m.use_nodes = True
    t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = img
    m.node_tree.links.new(t.outputs[0], m.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    for o in meshes:
        o.data.materials.clear(); o.data.materials.append(m)
    sc, cam = setup_render()
    # 쉬는 자세 모서리 길이
    rest = {}
    if arm: arm.data.pose_position = 'REST'
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    for o in meshes:
        ev = o.evaluated_get(dg); me = ev.to_mesh()
        rest[o.name] = [(me.vertices[e.vertices[0]].co - me.vertices[e.vertices[1]].co).length for e in me.edges]
        ev.to_mesh_clear()
    if arm: arm.data.pose_position = 'POSE'
    acts = [x for x in bpy.data.actions] if arm else [None]
    for act in acts:
        clip = act.name.split("|")[-1] if act else "static"
        if arm:
            arm.animation_data_create(); arm.animation_data.action = act
            if hasattr(arm.animation_data, "action_slot") and act.slots: arm.animation_data.action_slot = act.slots[0]
            f0, f1 = act.frame_range
            frames = [int(f0 + (f1 - f0) * i / 3) for i in range(4)]
        else:
            frames = [1]
        worst = 0.0; bad = 0
        for fi, fr in enumerate(frames):
            sc.frame_set(fr)
            dg = bpy.context.evaluated_depsgraph_get()
            pts = []
            for o in meshes:
                ev = o.evaluated_get(dg); me = ev.to_mesh()
                r = rest[o.name]
                for k, e in enumerate(me.edges):
                    if k >= len(r) or r[k] < 1e-5: continue
                    ratio = (me.vertices[e.vertices[0]].co - me.vertices[e.vertices[1]].co).length / r[k]
                    worst = max(worst, ratio)
                    if ratio > 2.0 and (me.vertices[e.vertices[0]].co - me.vertices[e.vertices[1]].co).length - r[k] > 0.03: bad += 1  # 2배 넘게, 3cm 넘게 늘어난 모서리(0mm 에 가까운 모서리의 비율 착시는 뺀다)
                pts += [o.matrix_world @ v.co for v in me.vertices]
                ev.to_mesh_clear()
            mn = Vector([min(p[i] for p in pts) for i in range(3)]); mx = Vector([max(p[i] for p in pts) for i in range(3)])
            c = (mn + mx) / 2
            if fi in (1, 2) or not arm:
                for label, d in (("front", Vector((0, -1, 0))), ("side", Vector((1, 0, 0)))):
                    cam.location = c + d * 5
                    cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
                    sc.render.filepath = os.path.join(out, f"{name}_{clip}_{fi}_{label}.png")
                    bpy.ops.render.render(write_still=True)
            if fi == 2:
                print(f"REPORT {name} {clip} frame {fr} size {tuple(round(x, 2) for x in (mx - mn))}")
        print(f"REPORT {name} {clip}: torn edges (x2 and +3cm) = {bad}")
