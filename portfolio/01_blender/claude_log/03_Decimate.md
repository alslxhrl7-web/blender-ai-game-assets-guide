# 요청 2 — 목표 삼각형 수로 Decimate

## 요청문 (원문)
> 그다음 Merge by Distance로 겹친 정점을 정리하고, Decimate(Collapse)로 목표 삼각형 수(석구 20,000, 소총 5,000, 진지 40,000)까지 줄여줘. 전·후 삼각형 수와 Decimate 비율을 출력해줘.

## 실행한 코드 (핵심)
```python
ratio = min(1.0, target / tris(obj))
mod = obj.modifiers.new("Decimate_Game", 'DECIMATE')
mod.decimate_type = 'COLLAPSE'; mod.ratio = ratio
bpy.ops.object.modifier_apply(modifier=mod.name)
```
전체는 `scripts/s4_s6_process.py` `optimize()`.

## 출력
```
seokgu  {'tris_raw': 200000, 'tris_before_decimate': 199850, 'ratio': 0.1001, 'tris_after': 20000}
garand  {'tris_raw': 20000,  'tris_before_decimate': 20000,  'ratio': 0.25,   'tris_after': 5000}
sandbag {'tris_raw': 500000, 'tris_before_decimate': 497772, 'ratio': 0.0804, 'tris_after': 40000}
```

## 확인한 내용
- 생성 설정과 받은 파일이 달랐다: 석구는 '20,000 폴리곤'으로 생성했지만 FBX는 200,000 삼각형이었다. 그래서 석구 감소율이 89.9%로 가장 크다.
- 정리 후 정면·측면 와이어프레임에서 실루엣(철모 챙, 손가락, 각반, 총열·가늠쇠)이 유지되는 것을 확인했다(`captures/after/*_wire.png`).
- 철모 분리와 구멍 메우기로 석구 최종값은 20,143 삼각형이다.
