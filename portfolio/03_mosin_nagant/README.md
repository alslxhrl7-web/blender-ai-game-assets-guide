# 모신나강 M1891/30 — 게임용 에셋

「다섯 개의 강」에서 북한군이 드는 모신나강을 실물 사진을 참조해 Blender에서 형태·재질을 맞추고 Unity(URP)용으로 정리한 결과다.

![3/4 시점](captures/Mosin_final_34.png)

## 들어 있는 것

| 파일 | 내용 |
|---|---|
| [`FiveRivers_MosinNagant.unitypackage`](FiveRivers_MosinNagant.unitypackage) | Unity 패키지 (49 MB, Git LFS) |
| [`captures/`](captures) | Blender 렌더(3/4·측면) |

패키지를 불러오면 아래가 생긴다.

- `Assets/_Project/Resources/Weapons/Mosin_Nagant_ReferenceMatch_v2.prefab` — 총구 +Z · 위 +Y · 중심점 방아쇠, 재질 포함
- `Assets/_Project/Art/Weapons/Mosin_ReferenceMatch_v2/`
  - `Mosin_Nagant_ReferenceMatch_v2.fbx` — 객체 7개, **194,645** 삼각형, 길이 약 1.26 m(꽂을대 포함)
  - URP Lit 재질 5개: 목재 `M_Mosin_v2_Wood`, 강철 `M_Mosin_v2_Steel`, 가죽 `M_Mosin_v2_Leather`, 멜빵 천 `M_Mosin_v2_Canvas`, 홈 부분 어두운 금속 `M_Mosin_v2_Recess`
  - 텍스처 12장(소재별 BaseColor · Normal · MetallicSmoothness): 목재 8192×2048, 강철 4096×4096, 가죽·천 2048×1024

> 프리팹과 `Art/Weapons/Mosin_ReferenceMatch_v2/` 폴더는 GUID 로 서로 연결돼 있다. 따로 옮기면 재질이 끊기므로 패키지로 함께 옮긴다.

## 쓰는 법

1. Unity에서 **Assets → Import Package → Custom Package** 로 패키지를 불러온다.
2. 프로젝트가 **URP** 여야 한다. Built-in·HDRP 프로젝트에서는 재질이 분홍색으로 보이므로 셰이더를 바꿔야 한다.
   - Built-in: Standard 셰이더로 바꾸고 BaseColor → Albedo, Normal → Normal Map, MetallicSmoothness → Metallic(R = 금속성, A = 매끄러움).
3. 프리팹을 씬에 놓거나 `Resources.Load("Weapons/Mosin_Nagant_ReferenceMatch_v2")` 로 불러온다.

> 받는 방법: `.unitypackage` 와 PNG는 Git LFS다. 파일 화면의 다운로드 버튼으로 받거나 Git LFS가 있는 클라이언트(GitHub Desktop, SourceTree)로 받는다. "Download ZIP" 에는 실제 파일 대신 포인터 텍스트가 들어갈 수 있다.

![측면](captures/Mosin_final_side.png)

## 리소스와 출처

- 모델과 텍스처: 제작자가 VARCO 3D로 생성하고, 실물 사진을 참조해 Blender에서 형태와 소재별 텍스처를 직접 맞췄다. 사진 원본은 이 저장소에 넣지 않았다.

## 남은 점

- 삼각형이 194,645개로 1인칭 무기로도 무거운 편이다. 적 9명이 동시에 들기 때문에 LOD를 만들거나 줄이는 것이 좋다(M1 개런드 최종본은 LOD0 41k / LOD1 20k).
- LOD Group 이 없다.
- 목재 텍스처가 8192 px 로 크다. 대상 플랫폼에 맞춰 Unity 가져오기 설정의 Max Size 를 조정한다.
