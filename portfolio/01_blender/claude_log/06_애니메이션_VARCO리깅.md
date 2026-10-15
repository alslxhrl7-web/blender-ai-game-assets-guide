# 요청 6 — 애니메이션 (Mixamo 대체: VARCO Rig · Animate)

## 요청문 (원문)
> Mixamo 결과 정리: `varco_raw/mixamo/`의 Idle FBX와 Walk FBX를 가져와서, Walk 쪽은 액션만 남기고 추가 아마추어는 지워줘. `Armature|mixamo.com|Layer0`처럼 들어온 액션 이름을 프레임 수가 긴 쪽은 Idle, 짧은 쪽은 Walk로 바꾸고 둘 다 Fake User를 켜서 NLA 트랙으로 올려줘. 각 액션의 첫 프레임과 마지막 프레임 자세가 같은지 본별 회전 차이로 확인해서 알려주고, 아마추어 스케일이 0.01이면 적용한 뒤 자세가 깨지지 않는지 확인해줘.

## Mixamo 실패 기록
| 올린 파일 | 결과 |
|---|---|
| `export/Seokgu_for_mixamo.fbx` (몸+철모, 텍스처 포함 14 MB) | "기존 골격을 매핑할 수 없습니다" |
| `export/mixamo_alt/Seokgu_mixamo_v2.fbx` (한 메시, 텍스처 제외 0.7 MB, 축 굽기 끔) | 같은 오류 |
| `export/mixamo_alt/Seokgu_mixamo_obj.zip` (OBJ+MTL+텍스처) | 같은 오류 |
| 바르코3D 원본 `다섯개의강-Generate 3D-WWII Soldier.fbx` | 같은 오류 |

FBX를 열어 확인한 결과 LimbNode·Skeleton·Deformer가 모두 0개였다. 뼈대를 담을 수 없는 OBJ와 바르코 원본도 실패해서 파일 문제가 아니라 Mixamo 쪽 문제로 판단했다. 같은 워크플로의 VARCO Rig(Humanoid, 26f6a53b) → Animate(대기1, 걷기, In place)로 대체했다(사람이 실행).

## 실행한 코드
`scripts/probe_anim.py` (동작 판별), `scripts/s9_rig_transfer.py` (옮겨 붙이기·이름·NLA·내보내기), `scripts/loop_find.py` (반복 구간), Unity `SeokguAnimCheck.cs`.

## 출력
```
…39d54f….fbx  Armature|Take 001|BaseLayer 3–270  LeftUpLeg 2.2° RightUpLeg 1.9°  root 0.0 m   → Idle
…3343f0….fbx  Armature|Take 001|BaseLayer 3–85   허벅지 크게 흔들림                root 0.026 m → Walk (제자리)
본 18개: Root, Hips, Spine, Spine1, Spine2, Neck, Left/Right Shoulder·Arm·ForeArm, Left/Right UpLeg·Leg·Foot (Head 없음)
MATCH scale 1.7009 (바르코 0.591×0.218×1.0 → 정리 1.005×0.352×1.701), 아마추어 스케일 적용 → 1.0, 위치 키 ×1.7009
HEAD BONE Neck (철모 100%)
LOOP Idle {'frames': 268, 'max_deg': 2.23, 'max_bone': 'RightShoulder', 'mean_deg': 1.16}
LOOP Walk {'frames': 83, 'max_deg': 74.19, 'max_bone': 'RightFoot', 'mean_deg': 30.68}
SEGMENT Walk 7–74: max 7.62°, mean 4.95°
EXPORT Hero_Seokgu.fbx 14.45 MB
Unity: takes Idle [0-267], Walk [0-82] @30fps → clip Idle 8.90 s loop, Walk 2.23 s(67f) loop+loopPose, root rot 0 / scale 1
```

## 실패하고 고친 것
| 문제 | 원인 | 해결 |
|---|---|---|
| Walk에서 다리 면이 길게 찢어지고 신발 하나가 제자리에 남음 | 웨이트를 옮길 때 원본 메시가 걷기 첫 자세(보폭)로 변형돼 있어 반대 다리 웨이트가 들어감. 웨이트 합·0 웨이트는 정상이었고, 원본 메시는 같은 프레임에서 정상 | 옮기는 동안 아마추어를 Rest 자세로 → 원본과 같은 변형 |
| Walk 첫·끝 차이가 327°로 출력 | 쿼터니언 차이를 0~180°로 접지 않음 | min(d, 360−d)로 수정 → 74° |
| Walk 첫·끝 74° | 원본 3–85에 걷기 시작 동작이 들어 있음 | 모든 구간을 비교해 7–74(최대 7.6°)를 Unity 클립 범위로 |
| Unity 캡처 3장이 같은 자세 | 배치 모드에서 같은 오브젝트를 다시 샘플링하면 스킨이 갱신되지 않음 | 시점마다 새로 만들어 샘플링 |
| Humanoid 아바타 불가 | 바르코 리그에 Head 본이 없음 | Generic으로 가져옴 |
