# Poly Haven(CC0, 출처 표기 불필요) 자료 받기: blender -b --python fetch_polyhaven.py -- <저장 폴더> <해상도> <이름...>
# 모델은 glTF(+텍스처), 텍스처만 있는 것은 Diffuse·nor_gl·arm JPG 를 받는다.
import sys, os, json, urllib.request
a = sys.argv[sys.argv.index("--") + 1:]
out, res, names = a[0], a[1], a[2:]
UA = {"User-Agent": "FiveRiver-asset-fetch/1.0"}
def open_(url): return urllib.request.urlopen(urllib.request.Request(url, headers=UA))
def get(url, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if not os.path.exists(path):
        with open_(url) as r, open(path, "wb") as f: f.write(r.read())
    return os.path.getsize(path)
for n in names:
    j = json.load(open_("https://api.polyhaven.com/files/" + n))
    total = 0
    if "gltf" in j:
        e = j["gltf"][res]["gltf"]
        total += get(e["url"], os.path.join(out, n, os.path.basename(e["url"])))
        for rel, v in e["include"].items(): total += get(v["url"], os.path.join(out, n, rel))
    else:
        for key in ("Diffuse", "nor_gl", "arm", "Displacement"):
            if key in j and res in j[key]:
                v = j[key][res]["jpg"]
                total += get(v["url"], os.path.join(out, n, os.path.basename(v["url"])))
    print("GOT", n, round(total / 1e6, 1), "MB")
