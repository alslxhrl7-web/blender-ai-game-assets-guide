# Blender로 AI 3D 에셋을 게임용으로 정리하기 — 「다섯 개의 강」

제1차 성취도 평가 (블렌더 기초) 포트폴리오 — 나민재. VARCO 3D로 만든 리소스 3종을 Blender와 Claude로 게임에 바로 쓸 수 있게 정리하고, Unity에서 결과를 확인했다.

- 게임: 《다섯 개의 강: The Forgotten War》 — 1950년 낙동강 방어선 배경의 1인칭 FPS 싱글 캠페인
- 도구: VARCO 3D, Blender 5.2, Unity 6000.6 (URP), Claude Code
- 포트폴리오 PDF: [`portfolio/01_blender/01_블렌더기초_나민재.pdf`](portfolio/01_blender/01_블렌더기초_나민재.pdf)

## 결과 요약

| 항목 | 오석구 (캐릭터) | M1 개런드 (소품) | 모래주머니 진지 (배경) |
|---|---|---|---|
| 삼각형 수 | 200,000 → **20,143** (−89.9%) | 20,000 → **5,000** (−75.0%) | 500,000 → **40,258** (−91.9%) |
| 크기 (m) | 키 1.00 → **1.70** | 길이 1.00 → **1.10** | 폭 1.00 → **6.0** |
| 원점 | 몸 중앙 → 두 발 사이 바닥 | 총 중앙 → 개머리판 끝 바닥 | 중앙 → 바닥 중앙 |
| Unity Rot / Scale | X 270° / 1 → **0 / 1** | X 270° / 1 → **0 / 1** | X 270° / 1 → **0 / 1** |
| FBX 용량 | 45.1 → **14.4 MB** | 8.3 → **7.0 MB** | 75.4 → **14.6 MB** |
| 편집 | 철모 분리 | — | 앞·좌·우 벽 분리 |
| 애니메이션 | 없음 → **Idle 268f · Walk 67f** (Loop) | — | — |

자세한 표: [`stats_compare.md`](portfolio/01_blender/stats_compare.md) · 원자료: [`stats_before.csv`](portfolio/01_blender/stats_before.csv), [`stats_after.csv`](portfolio/01_blender/stats_after.csv)

## 평가 항목별로 볼 곳

| 평가 영역 | 근거 |
|---|---|
| AI 리소스 생성 | [`varco_raw/`](portfolio/01_blender/varco_raw) 원본 FBX 3종(+ Rig·Animate FBX), [`captures/before/`](portfolio/01_blender/captures/before), [`captures/varco_flow_rig_animate.png`](portfolio/01_blender/captures/varco_flow_rig_animate.png) |
| 게임 규격 정리 | [`scripts/s3_spec.py`](portfolio/01_blender/scripts/s3_spec.py), [`claude_log/01_규격정리.md`](portfolio/01_blender/claude_log/01_규격정리.md) |
| 최적화 | [`scripts/s4_s6_process.py`](portfolio/01_blender/scripts/s4_s6_process.py) `optimize()`, [`claude_log/02_조각정리.md`](portfolio/01_blender/claude_log/02_조각정리.md), [`03_Decimate.md`](portfolio/01_blender/claude_log/03_Decimate.md), `captures/*/…_wire.png` |
| 스타일 통일 | `style()`, [`claude_log/05_스타일통일.md`](portfolio/01_blender/claude_log/05_스타일통일.md), `captures/before/style.png` ↔ `captures/after/style.png` |
| 게임용 편집·검수 | [`claude_log/04_노멀_구멍_파츠.md`](portfolio/01_blender/claude_log/04_노멀_구멍_파츠.md), `captures/*/…_faceori.png`, `captures/after/*_outliner.png` |
| 애니메이션 정리 | [`scripts/s9_rig_transfer.py`](portfolio/01_blender/scripts/s9_rig_transfer.py), [`claude_log/06_애니메이션_VARCO리깅.md`](portfolio/01_blender/claude_log/06_애니메이션_VARCO리깅.md), `captures/after/anim_*.png` |
| Claude 활용 | [`claude_log/`](portfolio/01_blender/claude_log) 6건 (요청문 원문 · 코드 · 출력 · 확인 · 실패와 수정) |
| 결과 검증 | [`unity/`](portfolio/01_blender/unity) 스크립트, [`captures/unity/`](portfolio/01_blender/captures/unity), [`unity_compare.md`](portfolio/01_blender/unity_compare.md), [`unity_anim.md`](portfolio/01_blender/unity_anim.md) |

## 폴더

```
portfolio/01_blender/
├─ varco_raw/      VARCO 3D 원본 FBX (수정하지 않음). mixamo/ = Rig·Animate 결과 FBX
├─ blender/        최종 .blend (각 리소스 04_final, 석구 05_rigged)
├─ export/         최종 FBX 3종 (Hero_Seokgu / Prop_M1Garand / Env_SandbagPosition)
│                  + Mixamo 업로드 시도본 (Seokgu_for_mixamo, mixamo_alt/)
├─ captures/       before / after / unity 캡처
├─ claude_log/     Claude 활용 기록 6건 + process_log.json (스크립트 원본 출력)
├─ scripts/        Blender 배경 모드 스크립트 (단계 순서: s2 → s3 → s4_s6 → s8 → s9)
├─ unity/          Unity 에디터 스크립트 (비교 씬 생성, 애니메이션 확인)
└─ stats_*.csv/md, unity_*.md, 포트폴리오 HTML·PDF
```

## 다시 실행하기

```powershell
$b = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
& $b -b --factory-startup --python portfolio/01_blender/scripts/s2_stats_before.py
& $b -b --factory-startup --python portfolio/01_blender/scripts/s3_spec.py
& $b -b --factory-startup --python portfolio/01_blender/scripts/s4_s6_process.py
& $b -b --factory-startup --python portfolio/01_blender/scripts/s8_export.py
& $b -b --factory-startup --python portfolio/01_blender/scripts/s9_rig_transfer.py
```

중간 단계 .blend(00_raw ~ 03_style)는 용량 때문에 올리지 않았다. 위 순서로 실행하면 `blender/`에 다시 만들어진다.

## 알아 둘 점

- VARCO에서 받은 FBX의 삼각형 수가 생성 설정과 달랐다(석구 설정 20,000 → 실제 200,000).
- Mixamo Auto-Rigger가 뼈대 없는 파일(FBX·OBJ·VARCO 원본)에도 "기존 골격을 매핑할 수 없습니다"로 4회 실패해, 같은 워크플로의 VARCO Rig(Humanoid)·Animate로 대체했다. VARCO 리그는 Head 본이 없어 Unity에서 Generic으로 가져온다.
- 큰 파일(FBX·PNG·blend·PDF·zip)은 Git LFS로 관리한다.
