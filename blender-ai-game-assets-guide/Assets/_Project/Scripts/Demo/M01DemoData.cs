using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M1 다부동 시연(B안, 관람형) 수치 모음. 기획서(Docs/B안_기획서.md) 3·4·6장의 표를 그대로 옮긴 것이다.
/// 시각은 모두 시작부터의 초. 기획이 Inspector 에서 고치면 코드 수정 없이 바뀐다.
/// 위치는 좌표 대신 씬의 GreyboxMarker id(CP05 등)와 경로 오브젝트 이름(Route_2 등)으로 적는다.
/// </summary>
[CreateAssetMenu(menuName = "Five Rivers/M1 시연 데이터", fileName = "M01_DemoData")]
public class M01DemoData : ScriptableObject
{
    [Tooltip("전체 길이(초)")] public float totalSeconds = 180f;

    [Header("비트 (기획서 3장 흐름표)")]
    public List<Beat> beats = new List<Beat>();

    [Header("스크립트 적 (EnemyData)")]
    public EnemyData enemy = new EnemyData();

    [Header("공세 순서 (WaveData)")]
    public List<WaveData> waves = new List<WaveData>();

    [Header("조명탄 (FlareData)")]
    public FlareData flare = new FlareData();

    [Header("무기 (WeaponData)")]
    public WeaponData weapon = new WeaponData();

    [Header("T-34 (미션 전용 이벤트)")]
    public T34Data t34 = new T34Data();

    [Header("플레이어")]
    public PlayerData player = new PlayerData();

    [Header("도입 (트럭 도착)")]
    public IntroData intro = new IntroData();

    [Header("새벽과 태석")]
    public DawnData dawn = new DawnData();

    [Header("대사·문구 (기획서 6장)")]
    [Tooltip("목소리 녹음이 아직 없어 자막으로 대신 보여 준다")] public bool showLinesAsSubtitles = true;
    public List<Line> lines = new List<Line>();

    [Header("미션 종료 화면 (EndScreenData)")]
    public EndScreenData endScreen = new EndScreenData();

    [Serializable]
    public class Beat
    {
        [Tooltip("코드가 알아보는 이름. 바꾸지 않는다.")] public string id;
        public string title;
        public float start;
        public float end;
    }

    [Serializable]
    public class EnemyData
    {
        [Tooltip("체력. M1 몸통 두 발, 머리 한 발에 쓰러지게 맞춘 값")] public float health = 100f;
        [Tooltip("엄폐 지점까지 뛰는 속도(m/s)")] public float moveSpeed = 6.5f;
        [Tooltip("사격 간격(초) 최소")] public float fireIntervalMin = 2f;
        [Tooltip("사격 간격(초) 최대")] public float fireIntervalMax = 3f;
        [Tooltip("총알이 플레이어에게서 빗나가는 거리(m)")] public float missRadius = 1.6f;
        [Tooltip("스치는 확률(0~1). 스치면 화면 가장자리 연출만 나온다.")] public float grazeChance = 0.2f;
    }

    [Serializable]
    public class StopPoint
    {
        [Tooltip("멈추는 엄폐 지점 마커 id")] public string marker;
        [Tooltip("마커에서 옮기는 값(m). 엄폐물 옆으로 나와 실루엣이 보이게 한다.")] public Vector3 offset;
    }

    [Serializable]
    public class WaveData
    {
        public float time;
        public string label;
        [Tooltip("진입로 번호(1~3). 경로 Route_N 의 능선 마루에서 나타난다.")] public int route = 2;
        [Tooltip("한 줄에 한 명. 줄 수가 인원이다.")] public List<StopPoint> stops = new List<StopPoint>();
    }

    [Serializable]
    public class FlareData
    {
        [Tooltip("올라가는 시간(초)")] public float riseSeconds = 2f;
        [Tooltip("밝은 시간(초)")] public float brightSeconds = 8f;
        [Tooltip("꺼지는 시간(초)")] public float fadeSeconds = 3f;
        [Tooltip("가장 밝을 때 빛 세기")] public float lightIntensity = 1.9f;
        public Color color = new Color(1f, 0.93f, 0.8f);
        public List<FlareShot> shots = new List<FlareShot>();
    }

    [Serializable]
    public class FlareShot
    {
        public float time;
        [Tooltip("조명탄 마커 id (FLARE_W, FLARE_E)")] public string marker = "FLARE_W";
        [Tooltip("이 발만 꺼지는 시간을 다르게 할 때(초). 0이면 기본값.")] public float fadeSeconds;
        public string note;
    }

    [Serializable]
    public class WeaponData
    {
        [Header("M1 개런드")]
        public float m1BodyDamage = 60f;
        public float m1HeadDamage = 100f;
        [Tooltip("사격 간격(초)")] public float m1FireInterval = 0.25f;
        [Tooltip("클립 한 개의 탄 수")] public int m1ClipSize = 8;
        [Tooltip("시작할 때 예비 클립 수")] public int m1SpareClips = 1;
        [Tooltip("장전 시간(초)")] public float m1ReloadSeconds = 2.2f;
        [Tooltip("탄약 상자에서 F 로 채워지는 예비 클립 수")] public int ammoBoxClips = 3;
        [Tooltip("탄약 상자에 닿는 거리(m)")] public float interactRange = 2.5f;
        [Tooltip("탄약 상자 마커 id")] public string ammoBoxMarker = "AMMO_01";

        [Header("바주카")]
        [Tooltip("로켓 수. 정면 1발 + 측면 2발")] public int bazookaRockets = 3;
        [Tooltip("재장전 시간(초)")] public float bazookaReloadSeconds = 4.5f;
    }

    [Serializable]
    public class T34Data
    {
        [Tooltip("등장 시각. 도로 북쪽 끝(T34_Path 시작)")] public float appearAt = 105f;
        [Tooltip("정지 지점(T34_Path 끝)에 닿아 멈추는 시각")] public float arriveAt = 130f;
        [Tooltip("주포를 쏘는 시각들(연출)")] public List<float> cannonTimes = new List<float> { 110f, 122f };
        [Tooltip("차체 기관총 시작")] public float mgFrom = 120f;
        [Tooltip("차체 기관총 끝")] public float mgTo = 130f;
        [Tooltip("이 횟수만큼 측면에 맞으면 정지·화염. 첫 발에는 연기만 난다.")] public int sideHitsToStop = 2;
        [Tooltip("이 시각까지 멈추지 않았으면 자동으로 정지 연출이 나온다(실패 없음)")] public float autoStopAt = 145f;
        [Tooltip("이 시각까지는 빠르게 내려온다. 진지에서 전차가 보이기 시작하는 곳까지 오는 시각이다.")] public float fastUntil = 115f;
        [Tooltip("fastUntil 에 경로의 몇 할까지 와 있는지(0~1). 그 뒤로는 정지 지점까지 천천히 내려온다.")] public float fastFraction = 0.82f;
        [Tooltip("측면 판정 각도(도). 차체 앞쪽에서 이만큼 넘게 벗어난 자리에서 쏜 로켓이 측면 명중이다.")] public float sideAngle = 60f;
        [Tooltip("둔덕(D)으로 가는 경로 이름. 끝점이 둔덕이다.")] public string moundPath = "Run_To_D";
    }

    [Serializable]
    public class PlayerData
    {
        public float walkSpeed = 4f;
        public float sprintSpeed = 6.5f;
        public float proneSpeed = 1.2f;
        [Tooltip("서 있을 때 눈높이(m)")] public float eyeStand = 1.6f;
        [Tooltip("엎드렸을 때 눈높이(m)")] public float eyeProne = 0.45f;
        [Tooltip("마우스 감도")] public float mouseSensitivity = 0.08f;
        [Tooltip("시연자 무적. 기본은 켜짐(F10 으로 끈다)")] public bool invincibleByDefault = true;
    }

    [Serializable]
    public class IntroData
    {
        [Tooltip("검은 화면에 자막만 보이는 시간(초)")] public float captionSeconds = 4f;
        [Tooltip("트럭이 멈추는 시각")] public float truckStopAt = 8f;
        [Tooltip("짐칸에서 내려 걷기 시작하는 시각")] public float walkFrom = 10f;
        [Tooltip("플레이어 시작 위치에 닿는 시각. 이때부터 직접 조작한다.")] public float arriveAt = 25f;
    }

    [Serializable]
    public class DawnData
    {
        [Tooltip("어두워지기 시작하는 시각")] public float fadeOutAt = 145f;
        [Tooltip("완전히 어두운 채로 있는 끝 시각. 이 사이에 새벽 라이팅으로 바뀐다.")] public float blackUntil = 147.5f;
        [Tooltip("다시 밝아지는 데 걸리는 시간(초)")] public float fadeInSeconds = 2.5f;
        [Tooltip("보충병 수(태석 제외)")] public int reinforcements = 4;
        [Tooltip("태석이 옆자리에 앉는 시각")] public float taeseokSitAt = 151.5f;
    }

    [Serializable]
    public class Line
    {
        public string id;
        public string speaker;
        public float time;
        [TextArea] public string text;
        [Tooltip("화면에 떠 있는 시간(초)")] public float seconds = 3.5f;
        [Tooltip("검은 화면 가운데에 크게 띄우는 자막(시작 자막)")] public bool caption;
        public string note;
    }

    [Serializable]
    public class EndScreenData
    {
        public string datePlace = "1950년 8월, 경북 칠곡 다부동";
        public float datePlaceSeconds = 2f;
        public string killedInAction = "전사: 최만수 이병";
        public float killedSeconds = 3f;
        public string letter = "\"여보, 오늘 밤에도 하늘에 해가 몇 번이나 떴다 졌소.\"";
        public float letterSeconds = 3f;
        public string title = "다섯 개의 강";
        public string subtitle = "The Forgotten War";
    }

    public Beat FindBeat(string id)
    {
        foreach (var b in beats) if (b.id == id) return b;
        return null;
    }
}
