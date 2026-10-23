#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// M1 시연 A안(직접 플레이) 도구. 2차 평가(Unity 게임 프로토타입) 제출물을 만든다.
/// 메뉴: FiveRiver > A안 검사 : 자동 조작으로 처음부터 끝까지 돌려 실패·승리 판정과 수치를 Logs/M01Demo/playA.txt 에 남긴다.
/// 메뉴: FiveRiver > A안 플레이 영상 녹화 : 자동 조작 플레이를 약 2분 MP4 로 Builds/Video/ 에 저장한다.
/// 메뉴: FiveRiver > A안 PC 빌드 : Builds/A/ 에 Windows 실행 파일을 만든다.
/// 배치: -executeMethod M01PlayTools.BatchCheck / BatchRecord / BatchBuild
/// </summary>
public static class M01PlayTools
{
    const string Scene = "Assets/_Project/Scenes/Missions/" + M01Demo.SceneName + ".unity";
    const string ModeKey = "M01PlayMode", BatchKey = "M01PlayBatch";
    public const string VideoPath = "Builds/Video/02_Unity_FiveRiver_M1_play.mp4";
    public const string BuildPath = "Builds/02_나민재/Build/FiveRiver_M1.exe"; // 평가지 제출 방법: 빌드는 02_이름 폴더로

    [MenuItem("FiveRiver/A안 검사 (자동 조작, 빠르게)")] public static void RunCheck() => Run("check");
    [MenuItem("FiveRiver/A안 플레이 영상 녹화")] public static void RunRecord() => Run("record");
    [MenuItem("FiveRiver/A안 PC 빌드")] public static void Build() => BuildPlayer();

    public static void BatchCheck() => Batch("check");
    /// <summary>손을 뗀 채로 두어 전사 → 실패 화면이 나오는지 본다.</summary>
    public static void BatchFail() => Batch("fail");
    /// <summary>손 뗀 채 죽지 않게 두고 공세 끝(89초)까지: 적이 참호 안으로 들어오지 않고 흉벽 바깥에서 쏘는지 본다.</summary>
    public static void BatchHold() => Batch("hold");
    public static void BatchSetup() => Batch("setup");
    /// <summary>대표 이미지 후보: 화면 UI 를 숨기고 장면이 좋은 시각에 찍는다(Shots/Rep/).</summary>
    public static void BatchRep() => Batch("rep");
    static readonly float[] RepTimes = { 12f, 18f, 23f, 44f, 52f, 66f, 120f, 124f, 141f, 153f, 160f };

    // 포트폴리오용 화면(Shots/A/): 조작 안내, 정찰병, 공세, 탄약 보충, T-34, 결과
    static readonly float[] ShotTimes = { 10f, 30f, 50f, 62f, 75f, 112f, 128f, 138f, 178f };
    static int shot;
    static double failSeenAt;
    public static void BatchRecord() => Batch("record");

    /// <summary>
    /// 모델 점검 화면(Shots/Look/): 조준한 1인칭 화면과, 분대원·적을 옆에서 본 화면을 정해진 시각에 찍는다.
    /// 자동 조작은 죽지 않게 한다. 배치: -executeMethod M01PlayTools.BatchLook
    /// </summary>
    public static void BatchLook() => Batch("look");
    static readonly (float t, string name, Vector3 pos, Vector3 look)[] LookViews =
    {
        (9f, "aim_start", Vector3.zero, Vector3.zero),                                   // pos 0 = 플레이어 화면(조준), pos 1 = 플레이어 화면(허리)
        (12f, "hip_start", Vector3.one, Vector3.zero),
        (31f, "hip_flare", Vector3.one, Vector3.zero),
        (52.2f, "pov_kim", new Vector3(0.5f, 1.6f, -0.3f), new Vector3(6f, 1.2f, 3.4f)),
        (53.6f, "pov_park", new Vector3(-1f, 1.6f, 0f), new Vector3(-15f, 1.2f, 1.2f)),
        (130f, "hip_bazooka", Vector3.one, Vector3.zero),
        (14f, "kim_side", new Vector3(9f, 1.5f, -2.5f), new Vector3(6f, 1.0f, 0f)),
        (15f, "park_side", new Vector3(-12f, 1.5f, -2.5f), new Vector3(-15f, 1.0f, 0f)),
        (16f, "trench_long", new Vector3(18f, 1.7f, -1.5f), new Vector3(0f, 1.0f, 0f)),
        (48f, "aim_assault", Vector3.zero, Vector3.zero),
        (50f, "kim_close_50", new Vector3(7.8f, 1.5f, 1.2f), new Vector3(6f, 1.0f, 3.4f)),
        (52f, "kim_fire", new Vector3(8.5f, 1.6f, -2.2f), new Vector3(6f, 1.1f, 0.5f)),
        (52.5f, "kim_close_52", new Vector3(7.8f, 1.5f, 1.2f), new Vector3(6f, 1.0f, 3.4f)),
        (53f, "park_fire", new Vector3(-12.5f, 1.6f, -2.2f), new Vector3(-15f, 1.1f, 0.5f)),
        (66f, "enemy_near1", new Vector3(-3f, 2.2f, 7f), new Vector3(0f, 2.5f, 22f)),
        (72f, "enemy_near2", new Vector3(5f, 2.2f, 7f), new Vector3(4f, 2.5f, 20f)),
        (58f, "enemy_slope", new Vector3(0f, 3.5f, 10f), new Vector3(0f, 4f, 45f)),
        (62f, "enemy_close", new Vector3(-6f, 6f, 22f), new Vector3(-2f, 6f, 40f)),
        (110f, "aim_tank", Vector3.zero, Vector3.zero),
        (158f, "dawn_trench", new Vector3(4f, 1.6f, -2.5f), new Vector3(0f, 1.0f, 0f)),
    };
    static int look;

    static void CaptureLook(string name, Vector3 pos, Vector3 target)
    {
        Directory.CreateDirectory("Shots/Look");
        var go = new GameObject("look_cam");
        var cam = go.AddComponent<Camera>();
        var main = Camera.main;
        if (main != null) { cam.CopyFrom(main); cam.fieldOfView = 55f; }
        cam.transform.position = pos;
        cam.transform.LookAt(target);
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        File.WriteAllBytes($"Shots/Look/{name}.png", tex.EncodeToPNG());
        RenderTexture.active = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }
    public static void BatchBuild()
    {
        bool ok = BuildPlayer();
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static void Batch(string mode)
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Scene);
        if (mode == "setup") { M01CharacterSetup.Build(); EditorApplication.Exit(0); return; }
        SessionState.SetBool(BatchKey, true);
        EditorApplication.delayCall += () => Run(mode);
    }

    static void Run(string mode)
    {
        if (EditorApplication.isPlaying) return;
        if (SceneManager.GetActiveScene().name != M01Demo.SceneName) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(Scene);
        EditorApplication.ExecuteMenuItem("Window/General/Game");
        double startAt = EditorApplication.timeSinceStartup + 3.0;
        void Wait()
        {
            if (EditorApplication.timeSinceStartup < startAt) return;
            EditorApplication.update -= Wait;
            SessionState.SetString(ModeKey, mode);
            EditorApplication.EnterPlaymode();
        }
        EditorApplication.update += Wait;
    }

    static M01Demo demo;
    static DemoRecorder recorder;
    static string mode;
    static readonly List<string> lines = new List<string>();
    static double waitUntil;
    static float minHealth;
    static float enemyMinZ = 999f; // 참호 앞(x -21.5~21.5)에서 살아 있는 적이 가장 가까이 온 z. 6.5 보다 작으면 흉벽을 넘은 것
    static string lastBeat;

    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredEditMode)
            {
                M01Demo.variant = M01Demo.Variant.A;
                if (SessionState.GetBool(BatchKey, false)) EditorApplication.Exit(0);
                return;
            }
            if (s != PlayModeStateChange.EnteredPlayMode) return;
            mode = SessionState.GetString(ModeKey, "");
            if (mode == "") return;
            SessionState.EraseString(ModeKey);
            M01Demo.variant = M01Demo.Variant.A;
            demo = null; recorder = null; lines.Clear(); minHealth = 100f; lastBeat = ""; enemyMinZ = 999f;
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
                if (EditorApplication.timeSinceStartup > waitUntil) { lines.Add("FAIL 시연이 시작되지 않았다"); Finish(); }
                return;
            }
            demo.Pilot.active = mode != "fail" && mode != "hold";
            if (mode == "hold") demo.SetInvincible(true);
            if (mode == "rep") { demo.SetUiHidden(true); demo.SetInvincible(true); }
            // 자동 조작은 엄폐를 할 줄 몰라 공세에서 죽는다. 흐름 검사·영상은 끝까지 보려고 자동 조작만 죽지 않게 한다(게임 규칙은 그대로, 사망·재시작은 fail 검사로 본다).
            if (mode == "check" || mode == "record" || mode == "look") demo.SetInvincible(true);
            look = 0;
            shot = 0; failSeenAt = 0;
            demo.Ended += Finish;
            lines.Add($"MODE A안 {mode}");
            if (mode == "record")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(VideoPath));
                recorder = demo.gameObject.AddComponent<DemoRecorder>();
                recorder.path = VideoPath;
                // 평가지 1~2분: 조작 안내 4초 → 첫 조명탄(고정 조명탄이 뜨는 순간부터)·공세 → T-34(멈추면 새벽으로 넘어간다) → 새벽 → 끝 화면
                recorder.segments.Add(new Vector2(8f, 11f));
                recorder.segments.Add(new Vector2(19.5f, 89.9f)); // 공세 전멸이면 정적(90)으로 넘어가며 끝난다
                recorder.segments.Add(new Vector2(105f, 138f));
                recorder.segments.Add(new Vector2(150f, 156.5f));
                recorder.segments.Add(new Vector2(172f, 181f));
            }
            else Time.timeScale = 5f;
        }
        var p = demo.Player;
        minHealth = Mathf.Min(minHealth, p.Health);
        foreach (var en in demo.Stage.enemies) if (en != null && en.gameObject.activeInHierarchy && !en.Down && Mathf.Abs(en.transform.position.x) < 21.5f) enemyMinZ = Mathf.Min(enemyMinZ, en.transform.position.z);
        string beat = demo.Beat != null ? demo.Beat.id : "";
        if (beat != lastBeat) { lines.Add($"BEAT {demo.Clock:F1}s {beat}  체력 {p.Health:F0}  점수 {demo.Score}"); lastBeat = beat; }
        if (mode == "rep" && shot < RepTimes.Length && demo.Clock >= RepTimes[shot])
        {
            Directory.CreateDirectory("Shots/Rep");
            ScreenCapture.CaptureScreenshot($"Shots/Rep/{shot + 1:00}_{(int)RepTimes[shot]}s.png", 2);
            shot++;
        }
        if (mode == "look" && look < LookViews.Length)
        {
            var v = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(LookViews, x => x.t))[look]; // 시각 순서대로
            demo.Player.ForceAim = v.pos == Vector3.zero && demo.Clock >= v.t - 2f; // 조준 화면은 2초 전부터 조준해 시야가 다 좁아진 뒤 찍는다
            if (demo.Clock >= v.t)
            {
                Directory.CreateDirectory("Shots/Look");
                if (v.pos == Vector3.zero || v.pos == Vector3.one) ScreenCapture.CaptureScreenshot($"Shots/Look/{v.name}.png");
                else CaptureLook(v.name, v.pos, v.look);
                if (v.name == "kim_fire") foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) { var c = r.bounds.center; if (r.enabled && r.gameObject.activeInHierarchy && c.x > -9f && c.x < 3f && c.z > -3f && c.z < 3.5f && r.bounds.size.y > 0.6f && r.bounds.size.y < 2.5f) lines.Add($"NEAR {r.name} root={r.transform.root.name} c={c:F1} size={r.bounds.size:F1}"); }
                foreach (var b in Object.FindObjectsByType<DemoBody>(FindObjectsSortMode.None)) lines.Add($"LOOK {v.name} {b.name} {b.transform.position:F1} active={b.gameObject.activeInHierarchy} footGap={b.FootGap:F2} anim={b.Playing}");
                look++;
            }
        }
        if (mode == "check" && shot < ShotTimes.Length && demo.Clock >= ShotTimes[shot])
        {
            Directory.CreateDirectory("Shots/A");
            ScreenCapture.CaptureScreenshot($"Shots/A/{shot + 1:00}_{(int)ShotTimes[shot]}s.png");
            shot++;
        }
        if (mode == "hold" && demo.Clock >= 70f && shot == 0) { Directory.CreateDirectory("Shots/Look"); ScreenCapture.CaptureScreenshot("Shots/Look/hold_70s.png"); shot = 1; }
        if (mode == "hold" && demo.Clock >= 89f) { Finish(); return; }
        if (demo.Failed)
        {
            // 실패 화면을 한 장 찍고 끝낸다.
            if (failSeenAt == 0) { failSeenAt = EditorApplication.timeSinceStartup; lines.Add($"FAILED {demo.Clock:F1}s {demo.FailReason}"); return; }
            if (EditorApplication.timeSinceStartup - failSeenAt < 1.5) return;
            Directory.CreateDirectory("Shots/A");
            ScreenCapture.CaptureScreenshot("Shots/A/99_fail.png");
            failSeenAt = double.MaxValue;
            EditorApplication.delayCall += Finish;
            return;
        }
        if (recorder != null && recorder.Done) Finish();
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        Time.timeScale = 1f;
        if (demo != null)
        {
            var p = demo.Player; var st = demo.Stage;
            int shots = 0, hits = 0;
            foreach (var e in st.enemies) { shots += e.ShotsAtPlayer; hits += e.HitsOnPlayer; }
            lines.Add($"RESULT {(demo.Failed ? "실패: " + demo.FailReason : demo.Won ? "성공" : "종료")}  시각 {demo.Clock:F1}s");
            lines.Add($"SCORE {demo.Score}  처치 {st.CountKills()}/{st.EnemiesSpawned} (머리 {demo.Headshots})  후퇴 {st.EnemiesAutoCleared}");
            lines.Add($"PLAYER 사격 {p.ShotsFired} 명중 {p.Hits}  최저 체력 {minHealth:F0}  적 사격 {shots} 중 맞음 {hits}");
            lines.Add($"T34 정면 {demo.Tank.FrontHits} 측면 {demo.Tank.SideHits} 격파 {(demo.Tank.DestroyedByPlayer ? "플레이어" : "아니오")}");
            lines.Add($"ENEMY 참호 앞에서 가장 가까이 온 z {enemyMinZ:F1} m (흉벽 바깥선 6.5)");
            if (recorder != null) lines.Add($"VIDEO {VideoPath} 프레임 {recorder.Frames} ({recorder.Frames / 30f:F0}초) 소리 {(recorder.HasAudio ? "있음" : "없음")}");
        }
        if (recorder != null) Object.DestroyImmediate(recorder);
        Directory.CreateDirectory("Logs/M01Demo");
        File.WriteAllLines("Logs/M01Demo/playA_" + mode + ".txt", lines, new UTF8Encoding(false));
        Debug.Log("[FiveRiver] A안 " + mode + " 끝");
        EditorApplication.ExitPlaymode();
    }

    static bool BuildPlayer()
    {
        M01Demo.variant = M01Demo.Variant.A;
        PlayerSettings.productName = "다섯 개의 강 - M1 다부동";
        PlayerSettings.companyName = "FiveRiver";
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        var s = report.summary;
        Directory.CreateDirectory("Logs/M01Demo");
        File.WriteAllText("Logs/M01Demo/buildA.txt", $"{s.result} {s.totalSize / 1e6:F0}MB {s.totalTime} 오류 {s.totalErrors} 경고 {s.totalWarnings}\n{BuildPath}\n", new UTF8Encoding(false));
        return s.result == BuildResult.Succeeded;
    }
}
#endif
