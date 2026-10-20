# 바르코3D 텍스처의 흰 얼룩(흩어진 글자 조각·점)을 지운다.
# 쓰는 법: blender -b --python clean_texture.py -- <입력 이미지> <출력 jpg> [밝기 차 0.10] [채도 상한 0.25]
# 하는 일: 주변(반지름 8px) 평균보다 확 밝고 색이 거의 없는 점만 골라, 그 자리를 주변 평균색으로 채운다.
#          칠 자체의 밝고 어두운 무늬는 그대로 두고, 튀는 흰 점만 없앤다.
import bpy, sys
import numpy as np

a = sys.argv[sys.argv.index("--") + 1:]
src, dst = a[0], a[1]
diff = float(a[2]) if len(a) > 2 else 0.10
sat_max = float(a[3]) if len(a) > 3 else 0.25

img = bpy.data.images.load(src)
w, h = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
rgb = px[:, :, :3]

def box(x, r):
    # 가로·세로로 나눠 평균(누적합)
    p = np.pad(x, ((r, r), (r, r), (0, 0)), mode='edge')
    c = np.cumsum(p, axis=0); c = np.concatenate([np.zeros_like(c[:1]), c], 0)
    v = (c[2 * r + 1:] - c[:-2 * r - 1]) / (2 * r + 1)
    c = np.cumsum(v, axis=1); c = np.concatenate([np.zeros_like(c[:, :1]), c], 1)
    return (c[:, 2 * r + 1:] - c[:, :-2 * r - 1]) / (2 * r + 1)

lum = rgb @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
mx, mn = rgb.max(2), rgb.min(2)
sat = np.where(mx > 1e-4, (mx - mn) / np.maximum(mx, 1e-4), 0)
blur = box(rgb, 8)
blum = blur @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
mask = (lum - blum > diff) & (sat < sat_max)
# 한 칸 넓혀 가장자리 테두리까지 덮는다
m = mask.copy()
m[1:] |= mask[:-1]; m[:-1] |= mask[1:]; m[:, 1:] |= mask[:, :-1]; m[:, :-1] |= mask[:, 1:]
# 메울 색: 얼룩을 뺀 주변 평균
keep = (~m).astype(np.float32)[:, :, None]
fill = box(rgb * keep, 10) / np.maximum(box(keep, 10), 1e-3)
rgb[m] = fill[m]
px[:, :, :3] = rgb
out = bpy.data.images.new("clean", w, h)
out.pixels[:] = px.ravel()
out.file_format = 'JPEG'
out.filepath_raw = dst
bpy.context.scene.render.image_settings.quality = 90
out.save(filepath=dst, quality=90)
print("REPORT", src.split("\\")[-1].split("/")[-1], "spots", int(mask.sum()), "->", int(m.sum()), "px of", w * h, f"({100 * m.sum() / (w * h):.2f}%)")
