#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// M1 시연(B안) 도구.
/// 메뉴: Five Rivers > M1 시연 데이터 만들기 : 기획서 3·4·6장의 수치로 Resources/M01_DemoData.asset 을 만든다(이미 있으면 건드리지 않는다).
/// 메뉴: Five Rivers > M1 시연 검사 ... : 재생 모드로 3분을 돌려 보고 결과를 Logs/M01Demo/ 에, 화면을 Shots/Demo/ 에 남긴 뒤 멈춘다. 씬은 저장하지 않는다.
///   - 자동 동선(빠르게/실제 속도): 시연자 동선을 자동으로 따라 하며 확인한다.
///   - 손 뗀 채로: 아무 조작도 하지 않아도 비트가 제시간에 넘어가는지(남은 적 자동 처리, T-34 자동 정지) 확인한다.
/// </summary>
public static class M01DemoTools
{
    const string AssetPath = "Assets/_Project/Resources/" + M01Demo.ResourceName + ".asset";
    const string ModeKey = "M01DemoCheckMode";
    const string BatchKey = "M01DemoBatch";
    const string LogDir = "Logs/M01Demo";
    const string ShotDir = "Shots/Demo";
    const float FastSpeed = 6f;
    static readonly float[] ShotTimes = { 2f, 7f, 14f, 22f, 26f, 31f, 36f, 46f, 52f, 58f, 67f, 72f, 80f, 95f, 108f, 112f, 118f, 124f, 133f, 139f, 144f, 151f, 157f, 165f, 171f, 173f, 176f, 179f };

    // =====================================================================
    //  데이터: 기획서의 표를 그대로 옮긴다
    // =====================================================================
    [MenuItem("Five Rivers/M1 시연 데이터 만들기")]
    public static void BuildData()
    {
        if (AssetDatabase.LoadAssetAtPath<M01DemoData>(AssetPath) != null)
        {
            Debug.Log("[Five Rivers] 시연 데이터가 이미 있어 그대로 둡니다: " + AssetPath);
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Resources")) AssetDatabase.CreateFolder("Assets/_Project", "Resources");
        var d = ScriptableObject.CreateInstance<M01DemoData>();

        // 3장 흐름표
        Beat(d, "INTRO", "도입", 0, 25);
        Beat(d, "FIRST_FLARE", "첫 조명탄", 25, 40);
        Beat(d, "ASSAULT", "공세", 40, 85);
        Beat(d, "SILENCE", "정적", 85, 105);
        Beat(d, "T34", "T-34", 105, 145);
        Beat(d, "DAWN", "새벽과 태석", 145, 170);
        Beat(d, "END", "미션 종료 화면", 170, 180);

        // 4장 공세 순서. 큰 화면에서 실루엣이 보이도록 진지에서 20~40m 인 엄폐 지점(CP13~CP18 포함)에 멈춘다. 처음 보이는 능선은 80m.
        // 엄폐물 바로 뒤가 아니라 옆에 서서 조명탄 아래 실루엣이 보이게 한다.
        var beside = new Vector3(1.5f, 0f, -0.5f);
        Wave(d, 28, "정찰병", 2, ("CP06", beside), ("CP13", beside));                    // 경사면 중간
        Wave(d, 40, "소총수", 2, ("CP14", beside), ("CP15", beside), ("CP16", beside));  // 경사면 아래쪽 3곳
        Wave(d, 60, "소총수", 1, ("CP03", beside), ("CP17", beside));                    // 왼쪽 엄폐 2곳
        Wave(d, 70, "소총수", 3, ("CP09", new Vector3(-1.5f, 0f, -1f)), ("CP18", beside)); // 오른쪽 엄폐 2곳 (CP09 는 동쪽이 나무에 가려 서쪽 옆에 선다)

        // 4장 조명탄 타이밍 (0:54~1:08 은 조명탄 없음: 어둠 속 이동과 탄약 보충)
        Flare(d, 27, "FLARE_W", 0f, "정찰병 2명");
        Flare(d, 41, "FLARE_E", 0f, "공세 시작");
        Flare(d, 68, "FLARE_W", 0f, "남은 적 4명");
        Flare(d, 82, "FLARE_E", 9f, "공세 마무리. 정적 구간으로 넘어가며 천천히 꺼진다");
        Flare(d, 107, "FLARE_E", 0f, "T-34 등장");

        // 6장 대사·문구. 미션 종료 화면 문구(END_001~004)는 endScreen 에 있다.
        Line(d, "UI_001", "", 0, "1950년 8월, 경북 칠곡 다부동", 4f, true, "시작 자막");
        Line(d, "VO_Kim_000", "김덕만", 8, "신병들 내려라. 고개 숙이고 따라오라우.", 3.5f, false, "평안도 말씨, 감수 필요");
        Line(d, "VO_Kim_001", "김덕만", 20, "조명탄 뜨면 고개 숙이라. 밝을 때 쏘고, 어두울 때 움직이라.", 5f, false, "");
        Line(d, "VO_Kim_002", "김덕만", 28, "저기, 능선 아래! 둘이다!", 3f, false, "");
        Line(d, "VO_Kim_003", "김덕만", 40, "온다! 각자 자리 지키라!", 3f, false, "");
        Line(d, "VO_Enemy_001", "북한군", 41, "앞으로!", 2f, false, "고증 확인 필요");
        Line(d, "VO_Park_001", "박용팔", 43, "온다, 온다잉!", 3f, false, "전라도 말씨, 감수 필요");
        Line(d, "VO_Enemy_002", "북한군", 60, "돌격!", 2f, false, "고증 확인 필요");
        Line(d, "VO_Park_002", "박용팔", 64, "석구야, 탄약 상자 뒤에 있당께!", 3.5f, false, "");
        Line(d, "VO_Park_003", "박용팔", 90, "만수야? …만수야.", 5f, false, "대답 없음. 사람 목소리");
        Line(d, "VO_Kim_004", "김덕만", 110, "전차다! 석구야, 둔덕으로 가라! 옆구리를 쏘라우!", 5f, false, "사람 목소리");
        Line(d, "VO_Taeseok_001", "윤태석", 152, "포항에서 왔습니다.", 3f, false, "앳된 목소리. 사람 목소리 권장");
        Line(d, "VO_Seokgu_N01", "석구 (내레이션)", 155, "여보, 날이 밝았소. 오늘 아침 학생들이 왔는데, 그중 하나는 포항에서 왔다 하오.", 6.5f, false, "");
        Line(d, "VO_Seokgu_N02", "석구 (내레이션)", 162, "이름이 태석이라 했소. 내 막냇동생뻘이오.", 3.8f, false, "");
        Line(d, "VO_Seokgu_N03", "석구 (내레이션)", 166, "만수는 끝내 아침을 못 봤소.", 3.8f, false, "사람 목소리");

        // 무기·T-34·플레이어·미션 종료 화면 수치는 M01DemoData 의 기본값이 기획서 4·5장 값이다.
        AssetDatabase.CreateAsset(d, AssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[Five Rivers] 시연 데이터를 만들었습니다: " + AssetPath);
    }

    static void Beat(M01DemoData d, string id, string title, float start, float end) =>
        d.beats.Add(new M01DemoData.Beat { id = id, title = title, start = start, end = end });

    static void Wave(M01DemoData d, float time, string label, int route, params (string marker, Vector3 offset)[] stops)
    {
        var w = new M01DemoData.WaveData { time = time, label = label, route = route };
        foreach (var s in stops) w.stops.Add(new M01DemoData.StopPoint { marker = s.marker, offset = s.offset });
        d.waves.Add(w);
    }

    static void Flare(M01DemoData d, float time, string marker, float fade, string note) =>
        d.flare.shots.Add(new M01DemoData.FlareShot { time = time, marker = marker, fadeSeconds = fade, note = note });

    static void Line(M01DemoData d, string id, string speaker, float time, string text, float seconds, bool caption, string note) =>
        d.lines.Add(new M01DemoData.Line { id = id, speaker = speaker, time = time, text = text, seconds = seconds, caption = caption, note = note });

    // =====================================================================
    //  검사
    // =====================================================================
    [MenuItem("Five Rivers/M1 시연 검사: 자동 동선 (빠르게)")]
    public static void RunCheck() => Run("check");

    [MenuItem("Five Rivers/M1 시연 검사: 손 뗀 채로 (빠르게)")]
    public static void RunIdle() => Run("idle");

    [MenuItem("Five Rivers/M1 시연 검사: 자동 동선 (실제 속도 3분)")]
    public static void RunLive() => Run("live");

    // 배치 모드: -batchmode -executeMethod M01DemoTools.BatchCheck / BatchIdle / BatchLive
    public static void BatchCheck() => Batch("check");
    public static void BatchIdle() => Batch("idle");
    public static void BatchLive() => Batch("live");

    static void Batch(string mode)
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Missions/" + M01Demo.SceneName + ".unity");
        SessionState.SetBool(BatchKey, true);
        EditorApplication.delayCall += () => Run(mode);
    }

    static void Run(string mode)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Five Rivers] 재생을 멈춘 뒤 실행하세요."); return; }
        if (SceneManager.GetActiveScene().name != M01Demo.SceneName)
        {
            Debug.LogWarning("[Five Rivers] " + M01Demo.SceneName + " 씬을 연 상태에서 실행하세요.");
            return;
        }
        BuildData();
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        // 게임 창이 제 크기로 자리 잡은 뒤에 시작한다(바로 시작하면 처음 몇 장이 작게 찍힌다).
        double startAt = EditorApplication.timeSinceStartup + 3.0;
        void Wait()
        {
            if (EditorApplication.timeSinceStartup < startAt) return;
            EditorApplication.update -= Wait;
            M01Demo.autoBoot = true;
            M01Demo.startWithAutoPilot = mode != "idle";
            M01Demo.startWithDebug = true;
            SessionState.SetString(ModeKey, mode);
            EditorApplication.EnterPlaymode();
        }
        EditorApplication.update += Wait;
    }

    static M01Demo demo;
    static string mode;
    static readonly List<string> lines = new List<string>();
    static int failures, shot;
    static double waitUntil;

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredEditMode)
            {
                M01Demo.startWithAutoPilot = false;
                M01Demo.startWithDebug = false;
                if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(0);
                return;
            }
            if (s != PlayModeStateChange.EnteredPlayMode) return;
            mode = SessionState.GetString(ModeKey, "");
            if (mode == "") return;
            SessionState.EraseString(ModeKey);
            // 재생에 들어오면 정적 값이 지워질 수 있어 다시 넣는다. 이 검사는 B안(관람형) 기준이다.
            M01Demo.variant = M01Demo.Variant.B;
            M01Demo.startWithAutoPilot = mode != "idle";
            M01Demo.startWithDebug = true;
            demo = null; lines.Clear(); failures = shot = 0;
            waitUntil = EditorApplication.timeSinceStartup + 10.0;
            Application.runInBackground = true;
            EditorApplication.update += Tick;
        };
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
        if (demo == null)
        {
            demo = Object.FindAnyObjectByType<M01Demo>();
            if (demo == null || demo.Pilot == null)
            {
                demo = null;
                if (EditorApplication.timeSinceStartup > waitUntil) { Fail("시연이 시작되지 않았다(데이터 에셋 확인)"); Finish(); }
                return;
            }
            demo.Pilot.active = mode != "idle";
            demo.SetDebug(true);
            CheckNames(demo);
            demo.BeatStarted += b =>
            {
                lines.Add($"BEAT {demo.Clock:F1}s (예정 {b.start:F0}s) {b.title}");
                if (Mathf.Abs(demo.Clock - b.start) > 1f) Fail($"비트 '{b.title}' 시작이 {demo.Clock - b.start:F1}초 어긋남");
            };
            demo.Ended += Finish;
            lines.Add($"MODE {mode}");
            lines.Add($"BEAT 0.0s (예정 0s) {(demo.Beat != null ? demo.Beat.title : "?")}");
            Time.timeScale = mode == "live" ? 1f : FastSpeed;
        }
        if (shot < ShotTimes.Length && demo.Clock >= ShotTimes[shot])
        {
            string dir = Path.Combine(ShotDir, mode);
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{shot + 1:00}_{(int)ShotTimes[shot]}s.png"));
            shot++;
        }
    }

    static void CheckNames(M01Demo d)
    {
        var data = d.data;
        foreach (var id in new[] { "SPAWN_PLAYER", "SEAT_TAESEOK", "NPC_KIM", "NPC_PARK", "TRUCK_STOP", "REINF_START", "A_EAST_ENTRY", "PROP_MANSU", data.weapon.ammoBoxMarker })
            if (!d.TryMarker(id, out _)) Fail($"마커 '{id}' 없음");
        foreach (var name in new[] { "Intro_Path", "Reinforcement_Path", "T34_Path", data.t34.moundPath })
            if (!d.TryPath(name, out _)) Fail($"경로 '{name}' 없음");
        foreach (var w in data.waves)
        {
            if (!d.TryPath("Route_" + w.route, out _)) Fail($"공세 {w.time}s: 경로 'Route_{w.route}' 없음");
            foreach (var s in w.stops) if (!d.TryMarker(s.marker, out _)) Fail($"공세 {w.time}s: 마커 '{s.marker}' 없음");
        }
        foreach (var f in data.flare.shots) if (!d.TryMarker(f.marker, out _)) Fail($"조명탄 {f.time}s: 마커 '{f.marker}' 없음");
    }

    static void Fail(string msg) { failures++; lines.Add("FAIL " + msg); }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Time.timeScale = 1f;
        if (demo != null)
        {
            var data = demo.data;
            var stage = demo.Stage; var tank = demo.Tank; var p = demo.Player;
            int expected = 0, standing = 0;
            foreach (var w in data.waves) expected += w.stops.Count;
            foreach (var e in stage.enemies) if (!e.Down) standing++;
            int kills = stage.CountKills();
            foreach (var e in stage.enemies) if (!e.KilledByPlayer) lines.Add($"NOT_SHOT {e.name} at ({e.transform.position.x:F0}, {e.transform.position.z:F0})");

            if (demo.Clock < data.totalSeconds) Fail($"끝까지 재생되지 않음: {demo.Clock:F1}s");
            lines.Add($"ENEMY 등장 {stage.EnemiesSpawned}/{expected}  시연자가 쓰러뜨림 {kills}  비트 종료로 자동 처리 {stage.EnemiesAutoCleared}  끝까지 서 있음 {standing}");
            if (stage.EnemiesSpawned != expected) Fail("등장하지 않은 적이 있다");
            if (standing != 0) Fail("끝까지 쓰러지지 않은 적이 있다");
            lines.Add($"FLARE {stage.FlaresFired}/{data.flare.shots.Count}");
            if (stage.FlaresFired != data.flare.shots.Count) Fail("쏘지 않은 조명탄이 있다");
            lines.Add($"LINES {demo.Hud.LinesShown}/{data.lines.Count}");
            if (demo.Hud.LinesShown != data.lines.Count) Fail("나오지 않은 대사·자막이 있다");
            lines.Add($"T34 주포 {tank.CannonShots}/{data.t34.cannonTimes.Count}  정면 명중 {tank.FrontHits}  측면 명중 {tank.SideHits}  정지 {(tank.Destroyed ? $"{tank.DestroyedAt:F1}s ({(tank.DestroyedByPlayer ? "측면 명중" : "자동")})" : "안 함")}");
            if (!tank.Destroyed) Fail("T-34 가 멈추지 않았다");
            if (tank.CannonShots != data.t34.cannonTimes.Count) Fail("쏘지 않은 주포가 있다");
            lines.Add($"WEAPON_MODELS {(p.HasWeaponModels ? "있음" : "없음")}  인물·적이 든 총 {stage.WeaponsCarried}");
            if (stage.WeaponsCarried != expected + 2 + data.dawn.reinforcements) Fail("총을 들지 못한 인물이나 적이 있다");
            if (!p.HasWeaponModels) Fail("손에 든 총 모델을 읽지 못했다");
            lines.Add($"PLAYER M1 사격 {p.ShotsFired}  핑 {p.Pings}  탄약 보충 {p.Resupplies}  로켓 {p.RocketsFired}  스침 {p.Grazes}");

            if (mode == "idle")
            {
                // 손을 떼고 있어도 모든 비트가 제시간에 넘어가야 한다.
                if (kills != 0 || p.ShotsFired != 0) Fail("손 뗀 검사인데 사격이 있었다");
                if (tank.DestroyedByPlayer || Mathf.Abs(tank.DestroyedAt - data.t34.autoStopAt) > 1f) Fail("T-34 자동 정지가 예정 시각에 나오지 않았다");
            }
            else
            {
                // 시연자 동선대로 했을 때 보여주려는 것이 다 나왔는지.
                if (kills != expected) Fail($"동선대로 쏴서 쓰러뜨린 적이 {kills}/{expected}명");
                if (p.Pings < 1) Fail("M1 '핑'이 한 번도 나지 않았다");
                if (p.Resupplies != 1) Fail("탄약 상자 보충이 한 번 일어나지 않았다");
                if (tank.FrontHits != 1) Fail($"정면 튕김이 한 번이 아니다({tank.FrontHits})");
                if (tank.SideHits != data.t34.sideHitsToStop || !tank.DestroyedByPlayer) Fail("측면 2발로 멈추지 않았다");
            }
        }
        lines.Add($"SHOTS {shot}/{ShotTimes.Length}");
        lines.Add($"COMPLETE failures={failures}");
        Directory.CreateDirectory(LogDir);
        string file = Path.Combine(LogDir, (string.IsNullOrEmpty(mode) ? "check" : mode) + ".txt");
        File.WriteAllLines(file, lines, new UTF8Encoding(false));
        Debug.Log($"[Five Rivers] 시연 검사 끝: failures={failures} ({file})");
        EditorApplication.ExitPlaymode();
    }
}
#endif
