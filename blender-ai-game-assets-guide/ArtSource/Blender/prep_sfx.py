# Freesound(CC0)에서 받은 소리를 게임용 WAV 로 다듬는다: 소리가 시작하는 곳부터 자르고, 길이를 맞추고, 끝을 줄이고, 크기를 맞춘다.
# 쓰는 법: blender -b --python prep_sfx.py -- <받은 폴더> <내보낼 폴더>
import aud, sys, os, numpy as np
src, dst = sys.argv[sys.argv.index("--") + 1:][:2]
R = 44100
# 이름, freesound 번호, 시작 찾기(onset)/고정 시작(초), 길이(초, 0=끝까지), 끝 줄이기(초), 크기(최대값), 고리(loop)
SPEC = [
    ("SFX_M1_Fire", 386842, "onset", 1.6, 0.4, 0.95, False),
    ("SFX_M1_Ping", 725398, "onset", 0.9, 0.2, 0.8, False),
    ("SFX_M1_Reload", 725399, "onset", 2.2, 0.3, 0.75, False),
    ("SFX_Click", 725402, "onset", 0.4, 0.1, 0.7, False),
    ("SFX_Mosin_Fire", 541164, "onset", 2.4, 0.6, 0.85, False),
    ("SFX_Bullet_Pass", 505238, "onset", 1.2, 0.3, 0.8, False),
    ("SFX_T34_Cannon", 127845, "onset", 4.0, 1.0, 1.0, False),
    ("SFX_Bazooka_Hit", 151076, "onset", 1.0, 0.3, 1.0, False),
    ("SFX_Bazooka_Fire", 182794, "onset", 2.2, 0.8, 0.95, False),
    ("SFX_T34_Ricochet", 30932, "onset", 2.0, 0.5, 0.85, False),
    ("SFX_T34_Engine", 329800, 1.0, 10.0, 0, 0.8, True),
    ("SFX_Truck_Engine", 478130, 0.5, 7.0, 0, 0.7, True),
    ("FOLEY_Footsteps_Dirt", 223154, "onset", 0.5, 0.15, 0.6, False),
    ("SFX_Wounded", 344682, "onset", 4.0, 0.8, 0.6, False),
    ("AMB_Night", 826093, 0.0, 28.0, 0, 0.5, True),
    ("AMB_Dawn_Wind", 117610, 5.0, 24.0, 0, 0.45, True),
    ("AMB_Distant_Artillery", 209769, "onset", 6.0, 2.0, 0.6, False),
    ("SFX_Flare_Launch", 450836, "onset", 2.5, 0.8, 0.7, False),
    ("SFX_Flare_Burn", 348766, 1.0, 8.0, 0, 0.5, True),
    ("SFX_Player_Breath", 554907, 0.0, 0, 0, 0.6, True),
]
files = {int(f.split("_")[0]): os.path.join(src, f) for f in os.listdir(src) if f.endswith(".mp3")}
for name, sid, start, length, fade, peak, loop in SPEC:
    s = aud.Sound(files[sid]).resample(R, False).rechannel(1)
    d = s.data()[:, 0].astype(np.float32)
    m = float(np.abs(d).max()) or 1.0
    if start == "onset":
        i0 = int(np.argmax(np.abs(d) > m * 0.12))
        i0 = max(0, i0 - int(0.008 * R))
    else:
        i0 = int(start * R)
    i1 = len(d) if length == 0 else min(len(d), i0 + int(length * R))
    x = d[i0:i1].copy()
    if fade > 0:
        n = min(len(x), int(fade * R)); x[-n:] *= np.linspace(1, 0, n) ** 2
    if loop:
        n = int(0.5 * R)  # 끝과 처음을 겹쳐 이음매를 없앤다
        head = x[:n].copy(); x = x[n:]; x[-n:] = x[-n:] * np.linspace(1, 0, n) + head * np.linspace(0, 1, n)
    x *= peak / max(1e-6, float(np.abs(x).max()))
    out = aud.Sound.buffer(x.reshape(-1, 1), R)
    out.write(os.path.join(dst, name + ".wav"), aud.RATE_44100, aud.CHANNELS_MONO, aud.FORMAT_S16, aud.CONTAINER_WAV, aud.CODEC_PCM)
    print("SFX", name, sid, f"{len(x)/R:.2f}s", "onset" if start == "onset" else start)
