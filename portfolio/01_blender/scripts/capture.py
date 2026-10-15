"""블렌더 화면(뷰포트 오버레이 포함) 캡처. GUI 모드로 실행한다.
blender <file.blend> --window-geometry 0 0 1600 1000 --python capture.py -- <spec.json>
spec: [{"out": "...png", "view": "FRONT|RIGHT|TOP|PERSP", "shading": "SOLID|MATERIAL|WIREFRAME",
        "wire": bool, "stats": bool, "faceori": bool, "window": bool, "hide": [names], "only": [names]}]
"""
import bpy, sys, json, os

spec = json.load(open(sys.argv[sys.argv.index("--") + 1], encoding="utf-8"))
state = {"i": 0, "phase": 0}


def view3d():
    for win in bpy.context.window_manager.windows:
        for area in win.screen.areas:
            if area.type == 'VIEW_3D':
                return win, area, area.spaces.active, [r for r in area.regions if r.type == 'WINDOW'][0]


def setup(shot):
    win, area, sp, reg = view3d()
    if shot.get("action"):
        arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
        a = bpy.data.actions[shot["action"]]
        for tr in arm.animation_data.nla_tracks:
            tr.mute = True
        arm.animation_data.action = a
        if hasattr(a, "slots") and a.slots:
            arm.animation_data.action_slot = a.slots[0]
    if shot.get("frame") is not None:
        bpy.context.scene.frame_set(shot["frame"])
    if shot.get("nla"):
        for a2 in win.screen.areas:
            if a2.type in ('DOPESHEET_EDITOR', 'TIMELINE') or a2.ui_type == 'TIMELINE':
                a2.ui_type = 'NLA_EDITOR'
    for o in bpy.context.scene.objects:
        hidden = (o.name in shot.get("hide", [])) or (shot.get("only") and o.name not in shot["only"])
        o.hide_set(bool(hidden))
    sp.shading.type = shot.get("shading", "SOLID")
    if sp.shading.type == 'SOLID':
        sp.shading.color_type = shot.get("color", "TEXTURE")
        sp.shading.light = 'STUDIO'
    sp.overlay.show_overlays = True
    sp.overlay.show_wireframes = shot.get("wire", False)
    sp.overlay.wireframe_threshold = 1.0
    sp.overlay.show_stats = shot.get("stats", False)
    sp.overlay.show_face_orientation = shot.get("faceori", False)
    th = bpy.context.preferences.themes[0].view_3d
    th.face_front = (0.1, 0.3, 1.0, 0.75)   # 5.x 기본값은 앞면이 투명이라 파랑으로 보이게
    th.face_back = (1.0, 0.05, 0.05, 0.9)
    sp.overlay.show_floor = True
    sp.overlay.show_extras = False
    sp.show_region_toolbar = False
    sp.show_region_ui = False
    with bpy.context.temp_override(window=win, area=area, region=reg):
        v = shot.get("view", "FRONT")
        if v == "PERSP":
            bpy.ops.view3d.view_axis(type='FRONT')
            r3 = sp.region_3d
            from mathutils import Euler
            r3.view_rotation = Euler((1.15, 0, 0.6), 'XYZ').to_quaternion()
            r3.view_perspective = 'PERSP'
        else:
            bpy.ops.view3d.view_axis(type=v)
        bpy.ops.object.select_all(action='DESELECT')
        for o in bpy.context.visible_objects:
            o.select_set(True)
        bpy.ops.view3d.view_selected()
        bpy.ops.object.select_all(action='DESELECT')
        if shot.get("zoom"):
            sp.region_3d.view_distance *= shot["zoom"]
    if shot.get("select"):
        for n in shot["select"]:
            bpy.data.objects[n].select_set(True)
            bpy.context.view_layer.objects.active = bpy.data.objects[n]


def shoot(shot):
    win, area, sp, reg = view3d()
    os.makedirs(os.path.dirname(shot["out"]), exist_ok=True)
    if shot.get("window"):
        for a in win.screen.areas:
            if a.type == 'OUTLINER':
                with bpy.context.temp_override(window=win, area=a, region=[r for r in a.regions if r.type == 'WINDOW'][0]):
                    bpy.ops.outliner.show_one_level(open=True)
                    bpy.ops.outliner.show_one_level(open=True)
        with bpy.context.temp_override(window=win):
            bpy.ops.screen.screenshot(filepath=shot["out"])
    else:
        with bpy.context.temp_override(window=win, area=area):
            bpy.ops.screen.screenshot_area(filepath=shot["out"])
    print("SHOT", shot["out"])


def tick():
    if state["i"] >= len(spec):
        bpy.ops.wm.quit_blender()
        return None
    shot = spec[state["i"]]
    if state["phase"] == 0:
        setup(shot); state["phase"] = 1
        return shot.get("wait", 1.5)
    shoot(shot); state["phase"] = 0; state["i"] += 1
    return 0.3


def first():
    # 3D 뷰를 크게: 타임라인/속성 영역은 그대로 두되 화면은 Layout
    return tick()

bpy.app.timers.register(first, first_interval=3.0)
