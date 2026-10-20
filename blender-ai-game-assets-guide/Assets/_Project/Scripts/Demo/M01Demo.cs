using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// M1 다부동 시연(B안, 관람형)의 중심. 시계(비트 타이머)를 돌리고 플레이어·무대·전차·화면을 순서대로 움직인다.
/// M01_Dabudong 씬을 재생하면 Resources/M01_DemoData 를 읽어 자동으로 생긴다(씬 파일은 고치지 않는다).
/// 모든 비트는 매번 같은 시각에 일어난다. 시연자 도구: F9 다음 비트, F10 무적, F11 화면 UI 숨김, F12 처음부터.
/// (F1 은 연습용 표시, F8 은 시연자 동선 자동 재생. 기획서에는 없는 확인용 키다.)
/// </summary>
public class M01Demo : MonoBehaviour
{
    public const string SceneName = "M01_Dabudong";
    public const string ResourceName = "M01_DemoData";

    /// <summary>A안: 보는 사람이 직접 플레이(적 AI, 체력, 실패·재시작, 점수). B안: 관람형(스크립트 적, 실패 없음).</summary>
    public enum Variant { A, B }
    /// <summary>어느 안으로 돌릴지. 빌드는 A안이다. 검사 도구가 B안으로 바꿀 수 있다.</summary>
    public static Variant variant = Variant.A;
    public bool IsA => variant == Variant.A;
    /// <summary>체크포인트에서 다시 할 때 시작 시각(초). 씬을 다시 읽은 뒤 이 시각부터 시작한다.</summary>
    public static float startAt;

    /// <summary>false 로 두면 재생을 시작해도 시연이 자동으로 생기지 않는다(맵만 볼 때).</summary>
    public static bool autoBoot = true;
    /// <summary>검사 도구가 켠다. 시연자 동선을 자동으로 따라 한다.</summary>
    public static bool startWithAutoPilot;
    public static bool startWithDebug;

    public M01DemoData data;
    public Camera cam;

    public float Clock { get; private set; }
    public bool Finished { get; private set; }

    // ---- A안 진행 상태 ----
    /// <summary>실패했으면 그 까닭(전사, 전차 돌파). 비어 있으면 진행 중.</summary>
    public string FailReason { get; private set; } = "";
    public bool Failed => FailReason != "";
    /// <summary>A안 성공: 플레이어가 T-34 를 멈췄다.</summary>
    public bool Won => Tank != null && Tank.DestroyedByPlayer;
    public int Score { get; private set; }
    public int Headshots { get; private set; }
    /// <summary>다시 시작할 체크포인트 시각(초). 첫 조명탄·공세·T-34 비트에 닿을 때마다 바뀐다.</summary>
    public float Checkpoint { get; private set; }
    public float CheckpointShownAt { get; private set; } = -10f;
    public static readonly string[] CheckpointBeats = { "FIRST_FLARE", "ASSAULT", "T34" };
    public M01DemoData.Beat Beat => beatIndex >= 0 ? beats[beatIndex] : null;
    public bool Invincible { get; private set; }
    public bool UiHidden { get; private set; }
    public bool ShowDebug { get; private set; }
    /// <summary>손에 든 총을 숨길지. 지금은 쓰지 않고 항상 보인다(F11 은 화면 UI 만 숨긴다).</summary>
    public bool UiHiddenWeapons => false;
    public void SetDebug(bool on) => ShowDebug = on;
    public void SetUiHidden(bool on) => UiHidden = on; // 대표 이미지 촬영용(F11 과 같다)
    public void SetInvincible(bool on) => Invincible = on; // F10 과 같다

    public DemoPlayer Player { get; private set; }
    public DemoStage Stage { get; private set; }
    public DemoTank Tank { get; private set; }
    public DemoHud Hud { get; private set; }
    public DemoAutoPilot Pilot { get; private set; }

    public event Action<M01DemoData.Beat> BeatStarted;
    public event Action<M01DemoData.Beat> BeatEnded;
    public event Action Ended;

    readonly Dictionary<string, Transform> markers = new Dictionary<string, Transform>();
    readonly Dictionary<string, Vector3[]> paths = new Dictionary<string, Vector3[]>();
    List<M01DemoData.Beat> beats;
    int beatIndex = -1;
    AudioSource[] voices; // 효과음 여러 개가 겹쳐 나도록 돌려 쓴다(높낮이가 서로 섞이지 않게)
    int nextVoice;
    public DemoSound Sound { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryBoot(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryBoot(scene);

    static void TryBoot(Scene scene)
    {
        if (!autoBoot || scene.name != SceneName) return;
        if (FindAnyObjectByType<M01Demo>() != null) return;
        var d = Resources.Load<M01DemoData>(ResourceName);
        if (d == null) return;
        new GameObject("M01Demo").AddComponent<M01Demo>().data = d;
    }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (data == null || cam == null)
        {
            Debug.LogWarning("[Five Rivers] 시연: 데이터나 카메라가 없어 시작하지 않습니다.");
            enabled = false;
            return;
        }
        foreach (var m in FindObjectsByType<GreyboxMarker>())
            if (!string.IsNullOrEmpty(m.id)) markers[m.id] = m.transform;
        foreach (var l in FindObjectsByType<LineRenderer>())
        {
            var pts = new Vector3[l.positionCount];
            l.GetPositions(pts);
            paths[l.name] = pts;
        }
        // A안: 기획서 A안의 흐름표·수치표·대사를 사본에 덮어쓴다(원본 에셋은 B안 그대로).
        if (IsA) { data = Instantiate(data); M01PlanA.Apply(data); }
        beats = new List<M01DemoData.Beat>(data.beats);
        beats.Sort((a, b) => a.start.CompareTo(b.start));

        Invincible = !IsA && data.player.invincibleByDefault; // A안은 맞으면 죽는다
        ShowDebug = startWithDebug;
        voices = new AudioSource[12];
        for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].spatialBlend = 0f; voices[i].playOnAwake = false; }

        Stage = gameObject.AddComponent<DemoStage>();
        Tank = gameObject.AddComponent<DemoTank>();
        Player = new GameObject("DemoPlayer").AddComponent<DemoPlayer>();
        Hud = gameObject.AddComponent<DemoHud>();
        Pilot = gameObject.AddComponent<DemoAutoPilot>();
        Sound = gameObject.AddComponent<DemoSound>();
        Stage.Init(this);
        Tank.Init(this);
        Player.Init(this);
        Hud.Init(this);
        Pilot.Init(this);
        Sound.Init(this);
        Pilot.active = startWithAutoPilot;
        BeatStarted += OnBeat;
        // 체크포인트에서 다시 하는 중이면 그 시각으로 건너뛴다. 지나간 공세·조명탄은 내보내지 않는다.
        Clock = Checkpoint = startAt;
        // A안: 처음 켰을 때와 미션 종료 화면 뒤에는 시작 화면(타이틀 + 시작 버튼)에서 기다린다. 죽고 다시 할 때는 바로 시작한다.
        Waiting = IsA && !skipStartScreen && !Application.isBatchMode && !startWithAutoPilot;
        GuideSkipped = IsA && guideSeen;
        skipStartScreen = false;
        startAt = 0f;
        Step(0f);
    }

    void OnBeat(M01DemoData.Beat b)
    {
        if (!IsA || Array.IndexOf(CheckpointBeats, b.id) < 0 || Checkpoint >= b.start) return;
        Checkpoint = b.start;
        CheckpointShownAt = Clock;
    }

    // =====================================================================
    //  A안 진행 상태
    // =====================================================================
    /// <summary>시작 화면에서 '시작'을 기다리는 중이다(시계가 멈춰 있다).</summary>
    public bool Waiting { get; private set; }
    /// <summary>두 번째 시도부터는 조작 안내를 생략한다(기획서 3장 실패와 재시작).</summary>
    public bool GuideSkipped { get; private set; }
    static bool guideSeen, skipStartScreen;
    float failedAt;

    public void BeginPlay()
    {
        if (!Waiting) return;
        Waiting = false;
        if (!Application.isBatchMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    /// <summary>점수는 화면에 나오지 않는다(기획서 5장 HUD: 점수·킬 카운트 없음). 검사 기록용으로만 센다.</summary>
    public void AddKill(bool head)
    {
        Score += head ? 150 : 100;
        if (head) Headshots++;
    }

    public void AddTankScore() => Score += 500;

    /// <summary>
    /// A안 실패(전사, T-34 실패선). 화면이 어두워진 뒤 저절로 다시 시작한다.
    /// 1:45(T-34) 전이면 처음부터(두 번째부터 조작 안내 생략 = 첫 조명탄부터), 그 뒤면 체크포인트(T-34 직전, 진지와 D 둔덕 사이)부터.
    /// </summary>
    public void Fail(string reason)
    {
        if (!IsA || Failed || Finished) return;
        FailReason = reason;
        failedAt = Time.unscaledTime;
        Debug.Log($"[Five Rivers] {Stamp(Clock)} 실패: {reason}");
        Player.SetMode(DemoPlayer.Mode.None, Player.Dead ? 0.3f : data.player.eyeStand);
    }

    /// <summary>실패한 뒤 다시 시작할 시각.</summary>
    public float RetryTime => Clock >= M01PlanA.T34 - 0.01f ? M01PlanA.T34 : (guideSeen ? M01PlanA.FirstFlare : 0f);

    public void RestartFrom(float time)
    {
        startAt = time;
        Restart();
    }

    void Update()
    {
        if (data == null || Stage == null) return;
        Tools();
        if (IsA && Failed && Time.unscaledTime - failedAt > 3f)
        {
            skipStartScreen = true;
            RestartFrom(RetryTime);
            return;
        }
        Step(Finished || Failed || Waiting ? 0f : Time.deltaTime);
    }

    void Step(float dt)
    {
        Clock += dt;
        if (IsA) FlowA();
        while (beatIndex + 1 < beats.Count && beats[beatIndex + 1].start <= Clock)
        {
            if (beatIndex >= 0) BeatEnded?.Invoke(beats[beatIndex]);
            beatIndex++;
            Debug.Log($"[Five Rivers] {Stamp(Clock)} 비트 시작: {beats[beatIndex].title}");
            BeatStarted?.Invoke(beats[beatIndex]);
        }
        Pilot.Tick(dt);
        Sound.Tick(dt);
        Stage.Tick(dt);
        Tank.Tick(dt);
        DirectPlayer();
        Player.Tick(dt);

        // A안: T-34 가 등장 후 45초 안에 멈추지 않으면 실패선에 닿는다.
        if (IsA && !Failed && InBeat("T34") && Tank.Appeared && !Tank.Destroyed && Clock >= data.t34.autoStopAt - 0.05f)
            Fail("T-34 가 실패선을 넘었습니다");

        if (!Finished && Clock >= data.totalSeconds)
        {
            Finished = true;
            Debug.Log("[Five Rivers] 시연 끝: " + Stamp(Clock));
            Ended?.Invoke();
            // A안: 미션 종료 화면이 끝나면 시작 화면으로 돌아간다(검사 도구가 돌릴 때는 그대로 둔다).
            if (IsA && !startWithAutoPilot && !Application.isBatchMode) { guideSeen = false; RestartFrom(0f); }
        }
    }

    /// <summary>
    /// A안 흐름표의 "넘어가는 조건"(기획서 3장).
    /// 시작: 20초 경과 / 첫 조명탄: 정찰병 전멸(놓치면 공세에 합류) / 공세: 공세 전멸 / 정적: 15초 경과 / T-34: 정지 / 새벽: 연출 종료.
    /// 조건이 먼저 채워지면 다음 비트로 건너뛰고, 공세가 남았으면 정적으로 넘어가지 않고 기다린다.
    /// </summary>
    void FlowA()
    {
        string id = Beat != null ? Beat.id : "";
        if (Clock >= M01PlanA.FirstFlare) guideSeen = true;
        switch (id)
        {
            case "FIRST_FLARE":
                if (Stage.WaveDone("FIRST_FLARE") && Clock > M01PlanA.FirstFlare + 3f) Clock = Mathf.Max(Clock, M01PlanA.Assault);
                break;
            case "ASSAULT":
                if (Stage.AllEnemiesDown && Stage.WavesSpawned) Clock = Mathf.Max(Clock, M01PlanA.Silence);
                else if (Clock >= M01PlanA.Silence - 0.05f) Clock = M01PlanA.Silence - 0.05f; // 남은 적이 있으면 기다린다
                break;
            case "T34":
                if (Tank.Destroyed && Clock - Tank.DestroyedAt > 4f) Clock = Mathf.Max(Clock, M01PlanA.Dawn);
                break;
        }
    }

    // =====================================================================
    //  비트에 따라 조작할 수 있는 것을 정한다
    //  B안 도입: 이동은 자동, 시점만 / A안 시작: 참호에서 직접 조작 / 새벽: 시점만 / 미션 종료 화면: 조작 없음
    // =====================================================================
    string directed = "";

    public Vector3 CheckpointPosition => OnGround(Marker("SPAWN_PLAYER") + M01PlanA.CheckpointOffset);

    void DirectPlayer()
    {
        string id = Beat != null ? Beat.id : "";
        var spawn = OnGround(Marker("SPAWN_PLAYER"));
        float stand = data.player.eyeStand;
        if (Waiting)
        {
            if (directed != "WAIT") { directed = "WAIT"; Player.SetMode(DemoPlayer.Mode.None, stand); Player.Place(spawn); Player.Face(spawn + new Vector3(0f, 2f, 30f)); }
            return;
        }
        if (id == "INTRO")
        {
            var feet = Stage.IntroPlayerFeet(out bool onTruck);
            Player.SetMode(DemoPlayer.Mode.LookOnly, onTruck ? 1.1f : stand);
            Player.Place(feet);
            if (directed != id) { directed = id; Player.Face(feet + new Vector3(-6f, 1.5f, 10f)); }
        }
        else if (id == "DAWN")
        {
            // 화면이 어두워진 뒤에 새벽의 진지 자리로 옮긴다. 그 전까지는 계속 직접 조작한다.
            if (directed != id && Clock >= data.dawn.fadeOutAt + 1.2f)
            {
                directed = id;
                Player.SetMode(DemoPlayer.Mode.LookOnly, 1.05f);
                Player.Place(spawn);
                // A안: 보충병이 도로로 걸어오는 쪽을 본다. B안: 옆에 앉는 태석.
                Player.Face(IsA ? Marker("A_EAST_ENTRY") + new Vector3(0f, 1.2f, 0f) : Marker("SEAT_TAESEOK") + new Vector3(2f, 0.9f, 3f));
            }
        }
        else if (id == "END")
        {
            if (directed != id) { directed = id; Player.SetMode(DemoPlayer.Mode.None, 1.05f); }
        }
        else if (directed != "FREE")
        {
            directed = "FREE";
            Player.SetMode(DemoPlayer.Mode.Free, stand);
            // A안 체크포인트(T-34 직전)에서 다시 하면 진지와 D 둔덕 사이에서 시작한다.
            Player.Place(IsA && Clock >= M01PlanA.T34 - 0.01f ? CheckpointPosition : spawn);
            Player.Face((IsA && Clock >= M01PlanA.T34 - 0.01f ? CheckpointPosition : spawn) + new Vector3(0f, 1.5f, 20f));
        }
    }

    // =====================================================================
    //  시연자 도구 (숨은 키)
    // =====================================================================
    void Tools()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (Waiting && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) BeginPlay();
        if (kb.f9Key.wasPressedThisFrame) SkipToNextBeat();
        if (kb.f10Key.wasPressedThisFrame) Invincible = !Invincible;
        if (kb.f11Key.wasPressedThisFrame) UiHidden = !UiHidden;
        if (kb.f12Key.wasPressedThisFrame) Restart();
        if (kb.f1Key.wasPressedThisFrame) ShowDebug = !ShowDebug;
        if (kb.f8Key.wasPressedThisFrame) Pilot.active = !Pilot.active;
    }

    /// <summary>시계를 앞으로 옮긴다(녹화 도구가 정적·새벽 구간을 건너뛸 때).</summary>
    public void JumpTo(float time)
    {
        if (time <= Clock) return;
        if (IsA) Stage.ClearEnemies(); // A안: 남은 적이 있으면 공세에서 기다리므로, 건너뛸 때는 물러나게 한다(녹화용)
        Clock = time;
    }

    public void SkipToNextBeat()
    {
        if (beatIndex + 1 >= beats.Count) return;
        if (IsA) Stage.ClearEnemies(); // A안: 남은 적은 물러나게 해야 공세에서 기다리지 않고 넘어간다
        Clock = beats[beatIndex + 1].start;
        Debug.Log("[Five Rivers] 다음 비트로 건너뜀: " + beats[beatIndex + 1].title);
    }

    public void Restart()
    {
        var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.buildIndex);
#endif
    }

    // =====================================================================
    //  도우미
    // =====================================================================
    public bool TryMarker(string id, out Vector3 pos)
    {
        bool found = !string.IsNullOrEmpty(id) && markers.TryGetValue(id, out var tr) && tr != null;
        pos = found ? markers[id].position : Vector3.zero;
        return found;
    }

    public Vector3 Marker(string id)
    {
        if (!TryMarker(id, out var pos)) Debug.LogWarning("[Five Rivers] 마커 없음: " + id);
        return pos;
    }

    public bool TryPath(string name, out Vector3[] pts) => paths.TryGetValue(name, out pts) && pts.Length > 0;

    public float BeatStart(string id) { var b = data.FindBeat(id); return b != null ? b.start : float.MaxValue; }
    public float BeatEnd(string id) { var b = data.FindBeat(id); return b != null ? b.end : float.MaxValue; }
    public bool InBeat(string id) => Beat != null && Beat.id == id;

    public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null || voices == null) return;
        var src = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, volume);
    }

    public static float Ground(float x, float z)
    {
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : 0f;
    }

    public static Vector3 OnGround(Vector3 p) => new Vector3(p.x, Ground(p.x, p.z), p.z);

    // 경로 전체 길이의 t(0~1) 지점과 그 자리의 진행 방향
    public static Vector3 AlongPath(Vector3[] pts, float t, out Vector3 forward)
    {
        forward = Vector3.forward;
        if (pts.Length == 1) return pts[0];
        float total = 0f;
        for (int i = 1; i < pts.Length; i++) total += Vector3.Distance(pts[i - 1], pts[i]);
        float want = total * Mathf.Clamp01(t);
        for (int i = 1; i < pts.Length; i++)
        {
            float len = Vector3.Distance(pts[i - 1], pts[i]);
            if (want <= len || i == pts.Length - 1)
            {
                forward = pts[i] - pts[i - 1];
                forward.y = 0f;
                return Vector3.Lerp(pts[i - 1], pts[i], len > 0f ? Mathf.Clamp01(want / len) : 1f);
            }
            want -= len;
        }
        return pts[pts.Length - 1];
    }

    public static string Stamp(float sec) => $"{(int)sec / 60}:{(int)sec % 60:00}";
}
