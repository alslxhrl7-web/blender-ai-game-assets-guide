using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시연 빌드 기획서 A안(직접 플레이)의 흐름표·수치표·대사를 시연 데이터 사본에 덮어쓴다(원본 에셋은 B안 그대로 둔다).
/// 기준 문서: Docs/A안_기획서.txt (「다섯 개의 강」 시연 빌드 기획서 A안, 3·4·5·6장).
///
/// A안은 시간이 아니라 조건으로 넘어간다. 아래 시각은 "그 비트가 길어야 이만큼"이라는 기준이고,
/// 조건이 먼저 채워지면 시계가 다음 비트로 건너뛰고(정찰병 전멸, 공세 전멸, T-34 정지),
/// 조건이 안 채워지면 그 자리에서 기다린다(공세가 남아 있으면 정적으로 넘어가지 않는다). 이 판단은 M01Demo 가 한다.
/// </summary>
public static class M01PlanA
{
    // 흐름표 (3장)
    public const float Start = 0f, FirstFlare = 20f, Assault = 40f, Silence = 90f, T34 = 105f, Dawn = 150f, End = 170f, Total = 184f;
    /// <summary>체크포인트: 진지와 D 둔덕 사이, T-34 등장 직전 (2장)</summary>
    public static readonly Vector3 CheckpointOffset = new Vector3(12f, 0f, -1.2f);

    // 플레이어 (4장)
    public const float EnemyRifleDamage = 30f;       // 네 발이면 사망
    public const float RegenDelay = 5f, RegenCap = 50f, RegenPerSecond = 10f;
    public const float SprintSeconds = 5f;           // 달리기 지속
    public const float ProneSteadyFactor = 0.6f;     // 엎드리면 반동·흔들림 40% 감소
    public const float MgDamage = 10f;               // 전차 차체 기관총 한 발

    // M1 개런드 (4장)
    public const int StartSpareClips = 5;            // 장전된 1개 + 예비 5개 = 클립 6개(48발)
    public const float ReloadEmpty = 2.2f, ReloadPartial = 3.5f;

    // 적 (4장 EnemyData)
    public const float DetectDark = 20f, DetectLit = 60f, Reaction = 0.8f;
    public const float FireMin = 2f, FireMax = 3f, HitDark = 0.15f, HitLit = 0.35f;
    public const float CoverMin = 3f, CoverMax = 5f, ChargeRange = 15f;
    public const int ChargeWhenLeft = 2;

    // 공세 (4장 WaveData)
    public const float SpawnGap = 1.5f;
    public const int MaxActive = 6;

    // 조명탄 (4장 FlareData): 20~40초 무작위 + 고정(첫 조명탄, 공세 시작). T-34 등장 연출에도 한 발(5장).
    public const float FlareGapMin = 20f, FlareGapMax = 40f;

    // T-34 (4장)
    public const float TankLimit = 45f, CannonEvery = 8f, FrontEffect = 0.1f, StopDamage = 2f;

    public static void Apply(M01DemoData d)
    {
        d.totalSeconds = Total;
        d.beats = new List<M01DemoData.Beat>
        {
            Beat("START", "시작", Start, FirstFlare),
            Beat("FIRST_FLARE", "첫 조명탄", FirstFlare, Assault),
            Beat("ASSAULT", "공세", Assault, Silence),
            Beat("SILENCE", "정적", Silence, T34),
            Beat("T34", "T-34", T34, Dawn),
            Beat("DAWN", "새벽", Dawn, End),
            Beat("END", "미션 종료 화면", End, Total),
        };

        // 정찰 2명(진입로 2, 동시 등장), 공세 7명(진입로 1·2·3 에 2·3·2명). 등장 간격 1.5초, 동시에 최대 6명은 DemoStage 가 지킨다.
        var beside = new Vector3(1.5f, 0f, -0.5f);
        d.waves = new List<M01DemoData.WaveData>
        {
            Wave(FirstFlare + 1f, "정찰병", 2, ("CP06", beside), ("CP13", beside)),
            Wave(Assault + 1f, "소총수", 2, ("CP14", beside), ("CP15", beside), ("CP16", beside)),
            Wave(Assault + 1f, "소총수", 1, ("CP03", beside), ("CP17", beside)),
            Wave(Assault + 1f, "소총수", 3, ("CP09", new Vector3(-1.5f, 0f, -1f)), ("CP18", beside)),
        };
        d.enemy.health = 100f;

        d.flare.shots = new List<M01DemoData.FlareShot>(); // A안 조명탄은 DemoStage 가 그때그때 띄운다
        d.flare.riseSeconds = 2f; d.flare.brightSeconds = 8f; d.flare.fadeSeconds = 3f;

        d.weapon.m1SpareClips = StartSpareClips;
        d.weapon.m1ReloadSeconds = ReloadEmpty;
        d.weapon.bazookaRockets = 3;
        d.weapon.bazookaReloadSeconds = 4.5f;

        var t = d.t34;
        t.appearAt = T34; t.arriveAt = T34 + 25f; t.autoStopAt = T34 + TankLimit;         // 25초에 도로 굽이, 그 뒤 진지 쪽으로 계속 기어 와 45초에 실패선
        t.fastUntil = T34; t.fastFraction = 0f;                                          // 일정한 속도로 느리게(멈추지 않는다)
        t.cannonTimes = new List<float>();
        for (float c = T34 + 4f; c < T34 + TankLimit; c += CannonEvery) t.cannonTimes.Add(c);
        t.mgFrom = T34; t.mgTo = T34 + TankLimit;
        t.sideHitsToStop = 2;

        d.player.invincibleByDefault = false;
        d.player.walkSpeed = 4f;      // 일반 FPS 의 85~90%
        d.player.sprintSpeed = 6.5f;

        d.dawn.fadeOutAt = Dawn; d.dawn.blackUntil = Dawn + 2.5f; d.dawn.fadeInSeconds = 2.5f;
        d.dawn.taeseokSitAt = Dawn + 8f;

        var e = d.endScreen;
        e.datePlaceSeconds = 2f; e.killedSeconds = 3f; e.letterSeconds = 4f;

        // 6장 대사 (자막). 트리거를 비트 안의 시각으로 옮겼다. VO_Park_002(탄약 부족)는 DemoStage 가 그때 띄운다.
        d.lines = new List<M01DemoData.Line>
        {
            Line("UI_001", "", 0.5f, "1950년 8월, 경북 칠곡 다부동", 4f, true),
            Line("VO_Kim_001", "김덕만", 6f, "조명탄 뜨면 고개 숙이라우. 밝을 땐 쏘고, 어두울 땐 움직이라우.", 5f),
            Line("VO_Kim_002", "김덕만", FirstFlare + 1f, "저기, 능선 아래! 둘이다!", 3f),
            Line("VO_Kim_003", "김덕만", Assault + 0.5f, "온다! 다들 자리 지키라우!", 3f),
            Line("VO_Enemy_001", "북한군", Assault + 2f, "동무들, 앞으로!", 2f),
            Line("VO_Park_001", "박용팔", Assault + 4f, "온다, 온다잉!", 2.5f),
            Line("VO_Enemy_002", "북한군", Assault + 20f, "돌격 앞으로!", 2f),
            Line("VO_Park_003", "박용팔", Silence + 5f, "만수야? …만수야.", 5f),
            Line("VO_Kim_004", "김덕만", T34 + 2f, "전차다! 석구야, 둔덕으로 가라우! 옆구리를 쏘라우!", 5f),
            Line("VO_Kim_005", "김덕만", Dawn + 3f, "…기래, 학생들이구만. 저 어린 것들을.", 3.5f),
            Line("VO_Seokgu_N01", "석구 (내레이션)", Dawn + 7f, "여보, 날이 밝았소. 오늘 아침 학생들이 왔는데, 그중 하나는 포항에서 왔다 하오.", 6.5f),
            Line("VO_Seokgu_N02", "석구 (내레이션)", Dawn + 14f, "이름이 태석이라 했소. 내 막냇동생뻘이오.", 3f),
            Line("VO_Seokgu_N03", "석구 (내레이션)", Dawn + 17.2f, "만수는 끝내 아침을 못 봤소.", 2.8f),
        };
    }

    public const string ParkAmmoLine = "석구야, 탄약 상자 뒤에 있당께!";

    static M01DemoData.Beat Beat(string id, string title, float s, float e) => new M01DemoData.Beat { id = id, title = title, start = s, end = e };

    static M01DemoData.WaveData Wave(float time, string label, int route, params (string marker, Vector3 offset)[] stops)
    {
        var w = new M01DemoData.WaveData { time = time, label = label, route = route };
        foreach (var s in stops) w.stops.Add(new M01DemoData.StopPoint { marker = s.marker, offset = s.offset });
        return w;
    }

    static M01DemoData.Line Line(string id, string speaker, float time, string text, float seconds, bool caption = false) =>
        new M01DemoData.Line { id = id, speaker = speaker, time = time, text = text, seconds = seconds, caption = caption };
}
