"""공용 함수: FBX 불러오기, 통계 뽑기. Blender 5.2 배경 모드에서 실행한다."""
import bpy, bmesh, os
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

RES = {
    # 키: (리소스명, 원본 FBX, 기준 축, 목표 길이 m, 목표 tris, 출력 파일)
    "seokgu":  ("오석구",        "varco_raw/Seokgu_varco.fbx",   "Z", 1.70, 20000, "export/Hero_Seokgu.fbx"),
    "garand":  ("M1 개런드",     "varco_raw/M1Garand_varco.fbx", "LONG", 1.10, 5000, "export/Prop_M1Garand.fbx"),
    "sandbag": ("모래주머니 진지", "varco_raw/Sandbag_varco.fbx",  "LONG", 6.00, 40000, "export/Env_SandbagPosition.fbx"),
}


def p(*parts):
    return os.path.join(ROOT, *parts)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def tris(o):
    me = o.data
    me.calc_loop_triangles()
    return len(me.loop_triangles)


def loose_parts(o):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bm.verts.ensure_lookup_table()
    seen = set(); count = 0
    for v in bm.verts:
        if v.index in seen:
            continue
        count += 1
        stack = [v]; seen.add(v.index)
        while stack:
            cur = stack.pop()
            for e in cur.link_edges:
                w = e.other_vert(cur)
                if w.index not in seen:
                    seen.add(w.index); stack.append(w)
    bm.free()
    return count


def world_bbox(objs):
    # bound_box는 배경 모드에서 갱신이 늦을 수 있어 정점 좌표로 직접 잰다
    import numpy as np
    arrs = []
    for o in objs:
        n = len(o.data.vertices)
        co = np.empty(n * 3, dtype=np.float64); o.data.vertices.foreach_get("co", co)
        co = co.reshape(n, 3)
        m = np.array(o.matrix_world)
        arrs.append(co @ m[:3, :3].T + m[:3, 3])
    a = np.vstack(arrs)
    mn, mx = a.min(axis=0), a.max(axis=0)
    return Vector(mn.tolist()), Vector(mx.tolist())


def textures(objs):
    out = []
    for o in objs:
        for slot in o.material_slots:
            m = slot.material
            if not m or not m.use_nodes:
                continue
            for n in m.node_tree.nodes:
                if n.type == 'TEX_IMAGE' and n.image:
                    img = n.image
                    entry = f"{os.path.basename(img.filepath) or img.name}({img.size[0]}x{img.size[1]})"
                    if entry not in out:
                        out.append(entry)
    return out


def stats_rows(key, objs, fbx_path):
    name = RES[key][0]
    meshes = [o for o in objs if o.type == 'MESH']
    rows = []
    size_mb = round(os.path.getsize(fbx_path) / 1048576, 2)
    mn, mx = world_bbox(meshes)
    for o in meshes:
        d = o.dimensions
        rows.append({
            "리소스": name, "오브젝트": o.name, "삼각형": tris(o),
            "머티리얼": len([s for s in o.material_slots if s.material]),
            "조각": loose_parts(o),
            "크기X": round(d.x, 3), "크기Y": round(d.y, 3), "크기Z": round(d.z, 3),
            "원점": tuple(round(c, 3) for c in o.matrix_world.translation),
            "회전": tuple(round(c, 3) for c in o.matrix_world.to_euler()),
            "스케일": tuple(round(c, 4) for c in o.matrix_world.to_scale()),
            "텍스처": " / ".join(textures([o])),
            "FBX_MB": size_mb,
        })
    total = {
        "리소스": name, "오브젝트": "(합계)", "삼각형": sum(r["삼각형"] for r in rows),
        "머티리얼": len({s.material.name for o in meshes for s in o.material_slots if s.material}),
        "조각": sum(r["조각"] for r in rows),
        "크기X": round(mx.x - mn.x, 3), "크기Y": round(mx.y - mn.y, 3), "크기Z": round(mx.z - mn.z, 3),
        "원점": "bbox min " + str(tuple(round(c, 3) for c in mn)),
        "회전": "", "스케일": "", "텍스처": " / ".join(textures(meshes)), "FBX_MB": size_mb,
    }
    return rows + [total]


def write_csv(path, rows):
    import csv
    with open(path, "w", newline="", encoding="utf-8-sig") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader(); w.writerows(rows)
