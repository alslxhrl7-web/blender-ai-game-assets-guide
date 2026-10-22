# M1 개런드 — 게임용 최종 에셋

「다섯 개의 강」에 쓰는 M1 개런드를 실총 측면 사진 기준으로 Blender에서 조립·마무리해 Unity(URP)용 에셋으로 만든 결과다. 1차 과제의 `Prop_M1Garand`(5,000 삼각형)를 대체한다.

![3/4 시점](captures/M1_final_34.png)

## 들어 있는 것

| 파일 | 내용 |
|---|---|
| [`FiveRivers_M1Garand_Final.unitypackage`](FiveRivers_M1Garand_Final.unitypackage) | Unity 패키지 (107 MB, Git LFS) |
| [`captures/`](captures) | Blender 렌더(3/4·측면), Unity 씬 뷰, 게임 1인칭 화면 |

패키지를 불러오면 `Assets/_Project/Resources/Weapons/M1_Garand_Final/` 에 아래가 생긴다.

- `M1_Garand_Final.prefab` — LOD Group (LOD0 **41,139** / LOD1 **20,568** 삼각형), 총구 +Z · 위 +Y · 중심점 방아쇠, 재질 포함
- `SM_M1_Garand_LOD0.fbx`, `SM_M1_Garand_LOD1.fbx`
- URP Lit 재질 `M_M1_Wood`, `M_M1_Metal`
- 텍스처 4096×4096, 목재·금속 각 6장: BaseColor, Normal, AO, Roughness, Metallic, MetallicSmoothness(URP용, R = 금속성, A = 매끄러움)

## 쓰는 법

1. Unity에서 **Assets → Import Package → Custom Package** 로 패키지를 불러온다.
2. 프로젝트가 **URP** 여야 한다. Built-in·HDRP 프로젝트에서는 재질이 분홍색으로 보이므로 셰이더를 바꿔야 한다.
   - Built-in: Standard 셰이더로 바꾸고 BaseColor → Albedo, Normal → Normal Map, MetallicSmoothness → Metallic, AO → Occlusion.
3. 프리팹을 씬에 놓거나 `Resources.Load("Weapons/M1_Garand_Final/M1_Garand_Final")` 로 불러온다.

> 받는 방법: `.unitypackage` 와 PNG는 Git LFS다. 파일 화면의 다운로드 버튼으로 받거나 Git LFS가 있는 클라이언트(GitHub Desktop, SourceTree)로 받는다. "Download ZIP" 에는 실제 파일 대신 포인터 텍스트가 들어갈 수 있다.

## 만든 과정 (요약)

1. **원본**: VARCO 3D에서 파츠 6종(개머리판·총열·리시버·볼트/작동봉·방아쇠뭉치·클립)을 생성.
2. **조립**: 실총 측면 사진에 축척(1 px = 1.26 mm)을 맞추고, 파츠마다 단면 윤곽을 사진 실루엣에 맞춰 변형. 측면 실루엣 일치도(IoU) 0.935.
3. **치수 보정**: 사진에서 알 수 없는 폭은 실측 자료로 맞춤 — 개머리판 최대 51.7 mm, 손목 42.6 mm, 포엔드 43.8 mm, 끝판 47.8 mm. 사진에서 잰 길이(개머리판 756 mm, 끝판 127 mm, 뒤 핸드가드 248 mm)는 실측과 일치.
4. **재모델링**: AI 형태가 뭉개진 가스통·가늠쇠(보호 귀)·멜빵고리·총구(7.62 mm 총강)·가스통 잠금 나사·가늠자(받침·보호 귀·조준판·조절 손잡이)를 Blender에서 새로 모델링. Bevel·Weighted Normal로 금속 모서리 정리.
5. **재질**: 목재는 절차형 호두나무 결(긴 결·나이테·도관), 금속은 파커라이징 무광 + 모서리 칠 벗겨짐·잔긁힘. 마모 위치는 곡률(Pointiness)과 AO로 결정.
6. **게임용**: Decimate로 줄인 메시에 UV를 새로 펴고, 고해상도에서 BaseColor·Normal·AO·Roughness·Metallic을 Cycles로 베이킹. LOD1은 LOD0의 50 %.
7. **검수**: 중립·측면·역광 조명에서 전체·리시버·총구·개머리판·핸드가드를 확대 확인하고, Unity 1인칭 화면에서 방향·재질 확인.

![Unity 1인칭](captures/Unity_M1_firstperson.png)

## 리소스와 출처

- 기본 메시: 제작자가 VARCO 3D로 생성.
- 나뭇결·마모·긁힘: Blender 절차형 노드로 직접 생성 (외부 이미지 사용 안 함).
- 가스통·가늠쇠·가늠자 등: Blender에서 직접 모델링.
- 치수 참고(텍스처에는 쓰지 않음): ww2aa.proboards.com(개머리판·핸드가드 길이), m14forum.com(끝판), gunhub.com(개머리판 폭), myplace.frontier.com/~aleccorapinski(가스통 지름).

## 남은 점

- 리시버 표면은 VARCO 3D 원본 텍스처 한계로 실물보다 밝고 얼룩이 있다.
- UV 활용률이 목재 42 %, 금속 32 %로 낮다(AI 메시 조각이 많음). 정식 리토폴로지를 하면 좋아진다.
- 게임에 조준 확대(ADS) 기능이 없어 확대 화면은 1인칭 캡처를 키워서 확인했다.
- Blender 원본(.blend, 556 MB)은 이 저장소에 넣지 않고 따로 보관한다.
