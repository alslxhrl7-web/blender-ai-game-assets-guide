using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시연 무대. 시계에 맞춰 밤→조명탄→새벽 밝기, 조명탄, 공세(스크립트 적), 인물(김덕만·박용팔·보충병·태석), 트럭을 움직인다.
/// 사람과 차량은 모두 임시 도형(캡슐·상자)과 이름표다. 모델이 정해지면 이 자리에서 바꿔 끼운다.
/// 밝기는 씬에 저장된 새벽 라이팅을 기준으로, 시작할 때 달밤으로 내렸다가 새벽 비트에서 되돌린다.
/// </summary>
public class DemoStage : MonoBehaviour
{
    [Header("밤 밝기")]
    public float nightSunIntensity = 0.9f;
    public Color nightSunColor = new Color(0.55f, 0.65f, 1f);
    public Color nightSky = new Color(0.30f, 0.38f, 0.58f);
    public Color nightEquator = new Color(0.24f, 0.28f, 0.40f);
    public Color nightGround = new Color(0.12f, 0.13f, 0.17f);
    public Color nightFog = new Color(0.06f, 0.075f, 0.115f);
    public float nightFogDensity = 0.0055f;
    public float nightSkyExposure = 0.14f;

    // 기록(검사용)
    public int EnemiesSpawned { get; private set; }
    public int EnemiesAutoCleared { get; private set; }
    public int FlaresFired { get; private set; }
    public int WavesSkipped { get; private set; }
    public readonly List<DemoEnemy> enemies = new List<DemoEnemy>();

    M01Demo demo;
    M01DemoData data;

    // 라이팅
    Light sun;
    float daySunIntensity, dayFogDensity, dayReflection, daySkyExposure;
    Color daySunColor, daySky, dayEquator, dayGround, dayFog;
    Material daySkybox, skybox;

    // 조명탄
    class Flare { public Transform ball; public Light light; public float seed; public bool done; }
    // (조명탄 목록은 아래 조명탄 절에 있다)

    // 공세
    List<M01DemoData.WaveData> waves;
    int nextWave;

    // 인물
    class Person { public Transform tr; public bool fires; public float nextShot; public float burstGap; public Vector3 post; }
    Person kim, park, taeseok;
    readonly List<Person> reinforcements = new List<Person>();
    Vector3[] kimIntroPath, introPath, reinforcePath;
    Transform truck;
    public Transform Truck => truck;
    Vector3 truckFrom, truckTo;
    LineRenderer moundArrow;
    TextMesh mansuLabel;
    float mansuNamedAt;

    // 불빛·예광선
    Light[] flashes;
    float[] flashLeft;
    LineRenderer[] tracers;
    float[] tracerLeft;
    int nextFlash, nextTracer;

    class NameTag { public Transform tr, target; public float height; }
    readonly List<NameTag> labels = new List<NameTag>();
    Font labelFont;
    Material enemyMat, friendMat, propMat, glowMat, lineMat;

    public void Init(M01Demo d)
    {
        demo = d;
        data = d.data;
        enemyMat = Flat(new Color(0.85f, 0.22f, 0.14f));   // 적: 빨강
        friendMat = Flat(new Color(0.20f, 0.48f, 0.95f));  // 아군: 파랑
        propMat = Flat(new Color(0.35f, 0.38f, 0.30f));    // 트럭: 국방색
        var hot = data.flare.color * 6f; hot.a = 1f;
        glowMat = Flat(hot);
        lineMat = new Material(Shader.Find("Sprites/Default"));

        // ---- 라이팅 ----
        sun = RenderSettings.sun;
        if (sun == null)
            foreach (var l in FindObjectsByType<Light>()) if (l.type == LightType.Directional) sun = l;
        if (sun != null) { daySunIntensity = sun.intensity; daySunColor = sun.color; }
        daySky = RenderSettings.ambientSkyColor;
        dayEquator = RenderSettings.ambientEquatorColor;
        dayGround = RenderSettings.ambientGroundColor;
        dayFog = RenderSettings.fogColor;
        dayFogDensity = RenderSettings.fogDensity;
        dayReflection = RenderSettings.reflectionIntensity;
        daySkybox = RenderSettings.skybox;
        if (daySkybox != null && daySkybox.HasProperty("_Exposure"))
        {
            // 원본 재질을 고치면 에셋 파일이 바뀌므로 사본을 쓴다.
            skybox = new Material(daySkybox);
            daySkyExposure = daySkybox.GetFloat("_Exposure");
            RenderSettings.skybox = skybox;
        }

        // ---- 불빛·예광선 ----
        flashes = new Light[8];
        flashLeft = new float[flashes.Length];
        for (int i = 0; i < flashes.Length; i++)
        {
            var l = New("Flash").AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.4f); l.shadows = LightShadows.None; l.enabled = false;
            flashes[i] = l;
        }
        tracers = new LineRenderer[20];
        tracerLeft = new float[tracers.Length];
        for (int i = 0; i < tracers.Length; i++)
        {
            var lr = New("Tracer").AddComponent<LineRenderer>();
            lr.useWorldSpace = true; lr.positionCount = 2; lr.sharedMaterial = lineMat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.enabled = false;
            tracers[i] = lr;
        }

        shots.AddRange(data.flare.shots); // B안: 데이터 표의 고정 시각. A안: 비어 있고 그때그때 더한다
        waves = new List<M01DemoData.WaveData>(data.waves);
        waves.Sort((a, b) => a.time.CompareTo(b.time));

        BuildCast();
        demo.BeatEnded += OnBeatEnded;
        if (demo.IsA) demo.BeatStarted += b => { if (demo.Clock - b.start < 1f) OnBeatStartedA(b); }; // 체크포인트에서 시작할 때 지나간 비트의 조명탄은 띄우지 않는다
        ApplyLook();
    }

    void OnDestroy()
    {
        if (demo != null) demo.BeatEnded -= OnBeatEnded;
        if (skybox != null)
        {
            RenderSettings.skybox = daySkybox;
            Destroy(skybox);
        }
    }

    public void Tick(float dt)
    {
        TickFlares(dt);
        TickWaves(dt);
        TickCast(dt);
        TickFx(dt);
        ApplyLook();
        TickCarried();
        TickLabels();
    }

    // =====================================================================
    //  밝기: 밤(0) ~ 새벽(1). 새벽 비트의 어두운 화면 사이에 바뀐다.
    // =====================================================================
    float flareGlow;
    /// <summary>지금 떠 있는 조명탄 밝기의 합(0 = 어둠). A안 적이 플레이어를 알아보는 데 쓴다.</summary>
    public float FlareLevel => flareGlow;

    public float DawnK() => Mathf.InverseLerp(data.dawn.fadeOutAt + 1f, data.dawn.blackUntil, demo.Clock);

    void ApplyLook()
    {
        float k = DawnK();
        Color glow = data.flare.color * Mathf.Clamp01(flareGlow) * (1f - k);
        if (sun != null)
        {
            sun.intensity = Mathf.Lerp(nightSunIntensity, daySunIntensity, k);
            sun.color = Color.Lerp(nightSunColor, daySunColor, k);
        }
        RenderSettings.ambientSkyColor = Color.Lerp(nightSky, daySky, k) + glow * 0.12f;
        RenderSettings.ambientEquatorColor = Color.Lerp(nightEquator, dayEquator, k) + glow * 0.08f;
        RenderSettings.ambientGroundColor = Color.Lerp(nightGround, dayGround, k) + glow * 0.03f;
        RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, k) + glow * 0.05f;
        RenderSettings.fogDensity = Mathf.Lerp(nightFogDensity, dayFogDensity, k);
        RenderSettings.reflectionIntensity = Mathf.Lerp(dayReflection * 0.1f, dayReflection, k);
        if (skybox != null) skybox.SetFloat("_Exposure", Mathf.Lerp(nightSkyExposure, daySkyExposure, k));
    }

    // =====================================================================
    //  조명탄 (FlareData): 올라가는 2초, 밝은 8초, 꺼지는 3초.
    //  B안: 시각이 모두 고정(데이터 표). A안: 20~40초 무작위 간격 + 고정 발(첫 조명탄, 공세 시작, T-34 등장 연출).
    //  A안은 공세가 길어져 시계가 멈춰 있어도 조명탄이 계속 떠야 하므로 따로 흐르는 시계(flareClock)를 쓴다.
    // =====================================================================
    readonly List<M01DemoData.FlareShot> shots = new List<M01DemoData.FlareShot>();
    readonly List<Flare> flareObjs = new List<Flare>();
    float flareClock, nextRandomFlare = float.MaxValue;
    bool flareWest;
    float FlareNow => demo.IsA ? flareClock : demo.Clock;

    void FireFlareNow()
    {
        flareWest = !flareWest;
        shots.Add(new M01DemoData.FlareShot { time = flareClock, marker = flareWest ? "FLARE_W" : "FLARE_E" });
        nextRandomFlare = flareClock + Random.Range(M01PlanA.FlareGapMin, M01PlanA.FlareGapMax);
    }

    void OnBeatStartedA(M01DemoData.Beat b)
    {
        if (b.id == "FIRST_FLARE" || b.id == "ASSAULT" || b.id == "T34") FireFlareNow(); // 중요한 순간은 운에 맡기지 않는다
    }

    void TickFlares(float dt)
    {
        var fd = data.flare;
        if (demo.IsA)
        {
            flareClock += dt;
            string id = demo.Beat != null ? demo.Beat.id : "";
            bool combat = id == "FIRST_FLARE" || id == "ASSAULT" || id == "SILENCE" || id == "T34";
            if (combat && flareClock >= nextRandomFlare && flareGlow < 0.05f) FireFlareNow();
        }
        float now = FlareNow;
        flareGlow = 0f;
        while (flareObjs.Count < shots.Count) flareObjs.Add(null);
        for (int i = 0; i < shots.Count; i++)
        {
            var shot = shots[i];
            float fade = shot.fadeSeconds > 0f ? shot.fadeSeconds : fd.fadeSeconds;
            float age = now - shot.time;
            float total = fd.riseSeconds + fd.brightSeconds + fade;
            var f = flareObjs[i];
            if (age < 0f || (f != null && f.done)) continue;
            if (age >= total)
            {
                if (f == null) flareObjs[i] = new Flare { done = true }; // 건너뛰어서 지나간 조명탄
                else { Destroy(f.ball.gameObject); Destroy(f.light.gameObject); f.done = true; }
                continue;
            }
            if (f == null)
            {
                f = flareObjs[i] = NewFlare(shot.marker);
                FlaresFired++;
                demo.Play(DemoAudio.FlareLaunch, 0.6f);
            }
            var top = demo.Marker(shot.marker);
            Vector3 pos;
            float level;
            if (age < fd.riseSeconds)
            {
                // 아군 진지 뒤에서 쏘아 올린다.
                float r = age / fd.riseSeconds;
                var from = new Vector3(top.x * 0.3f, M01Demo.Ground(top.x * 0.3f, -25f) + 2f, -25f);
                pos = Vector3.Lerp(from, top, 1f - (1f - r) * (1f - r));
                level = 0.12f + 0.88f * r * r * r;
            }
            else
            {
                float t = age - fd.riseSeconds;
                pos = top + new Vector3(Mathf.Sin(t * 0.4f + f.seed) * 3f, -2.2f * t, t * 0.3f);
                level = t < fd.brightSeconds ? 1f : 1f - (t - fd.brightSeconds) / fade;
                level *= 0.88f + 0.12f * Mathf.PerlinNoise(t * 9f, f.seed);
            }
            f.ball.position = pos;
            f.ball.localScale = Vector3.one * (2.6f * Mathf.Max(0.08f, level));
            f.light.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 5f, 40f) - pos); // 비탈 한가운데를 비춘다
            f.light.intensity = fd.lightIntensity * level;
            flareGlow += level;
        }
    }

    Flare NewFlare(string markerId)
    {
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(ball.GetComponent<Collider>());
        ball.name = "Flare_" + markerId;
        ball.transform.SetParent(transform, false);
        var r = ball.GetComponent<Renderer>();
        r.sharedMaterial = glowMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var l = New("Flare_Light_" + markerId).AddComponent<Light>();
        // 타는 소리: 조명탄 자리에서 난다(높이 떠 있어 멀리서 작게 들린다).
        var burn = ball.AddComponent<AudioSource>();
        burn.clip = DemoAudio.FlareBurn; burn.loop = true; burn.spatialBlend = 1f; burn.minDistance = 25f; burn.maxDistance = 300f; burn.volume = 0.7f; burn.dopplerLevel = 0f;
        if (burn.clip != null) burn.Play();
        l.type = LightType.Directional; l.color = data.flare.color; l.intensity = 0f; l.shadows = LightShadows.None;
        return new Flare { ball = ball.transform, light = l, seed = Random.value * 100f };
    }

    // =====================================================================
    //  공세 (WaveData): 매번 같은 시각, 같은 진입로, 같은 엄폐 지점
    // =====================================================================
    void TickWaves(float dt)
    {
        while (nextWave < waves.Count && waves[nextWave].time <= demo.Clock)
        {
            var w = waves[nextWave++];
            // F9 로 건너뛰어 이미 지나간 비트의 공세는 내보내지 않는다.
            if (demo.Beat != null && w.time < demo.Beat.start - 0.01f) { WavesSkipped++; continue; }
            SpawnWave(w);
        }
        int active = 0, alive = 0;
        foreach (var e in enemies) { if (e.Down) continue; alive++; if (e.gameObject.activeSelf) active++; }
        ActiveEnemies = active; AliveEnemies = alive;
        spawnQueue = Mathf.Max(0f, spawnQueue - dt);
        foreach (var e in enemies) e.Tick(dt);
    }

    void SpawnWave(M01DemoData.WaveData w)
    {
        if (!demo.TryPath("Route_" + w.route, out var pts) || pts.Length < 2)
        {
            Debug.LogWarning("[FiveRiver] 공세 경로 없음: Route_" + w.route);
            return;
        }
        // 경로의 1번 점이 능선 마루다. 마루 바로 뒤에서 넘어오게 한다.
        var crest = pts[1];
        for (int i = 0; i < w.stops.Count; i++)
        {
            var s = w.stops[i];
            if (!demo.TryMarker(s.marker, out var stop)) { Debug.LogWarning("[FiveRiver] 엄폐 지점 마커 없음: " + s.marker); continue; }
            stop += s.offset;
            float side = (i - (w.stops.Count - 1) * 0.5f) * 3f;
            var route = new[] { crest + new Vector3(side, 0f, 4f), crest + new Vector3(side, 0f, -2f), stop };
            var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            g.name = $"Enemy_R{w.route}_{w.label}";
            g.transform.SetParent(transform, false);
            g.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
            g.GetComponent<Renderer>().sharedMaterial = enemyMat;
            var e = g.AddComponent<DemoEnemy>();
            float delay = 0.01f + i * 0.7f;
            if (demo.IsA && w.label != "정찰병") { delay = Mathf.Max(0.01f, spawnQueue + 0.01f); spawnQueue = delay + M01PlanA.SpawnGap; } // A안: 1.5초 간격으로 한 명씩(정찰병은 동시에)
            e.Init(demo, route, delay, demo.Beat != null ? demo.Beat.id : "");
            enemies.Add(e);
            Carry(g.transform, DemoWeapons.Mosin, true);
            DemoBody.Attach(g.transform, true, demo); // 1차 과제 인물 모델(군복 색만 바꿈)
            EnemiesSpawned++;
        }
        Debug.Log($"[FiveRiver] {M01Demo.Stamp(demo.Clock)} 공세: {w.label} {w.stops.Count}명, 진입로 {w.route}");
    }

    // B안: 비트가 끝나면 그 비트에 나온 적 가운데 남은 적을 쓰러뜨린다(다음 비트가 제시간에 시작되도록).
    // A안: 남은 적은 그대로 싸운다. 첫 조명탄의 정찰병을 놓치면 공세에 합류한다(기획서 3장).
    void OnBeatEnded(M01DemoData.Beat beat)
    {
        if (demo.IsA) return;
        foreach (var e in enemies)
        {
            if (e.Down || e.beatId != beat.id) continue;
            e.Fall();
            EnemiesAutoCleared++;
        }
    }

    float spawnQueue; // A안: 아직 능선 뒤에서 기다리는 적의 등장 순서(초)

    /// <summary>그 비트에 나온 적이 있고, 모두 쓰러졌다.</summary>
    public bool WaveDone(string beatId)
    {
        bool any = false;
        foreach (var e in enemies) { if (e.beatId != beatId) continue; any = true; if (!e.Down) return false; }
        return any;
    }

    public bool AllEnemiesDown { get { foreach (var e in enemies) if (!e.Down && !e.Leaving) return false; return true; } }
    public bool WavesSpawned => nextWave >= waves.Count;
    /// <summary>능선을 넘어 싸우고 있는 적 수(동시에 최대 6명).</summary>
    public int ActiveEnemies { get; private set; }
    public int AliveEnemies { get; private set; }

    /// <summary>시연자 건너뛰기(F9): 남은 적을 물러나게 한다.</summary>
    public void ClearEnemies()
    {
        foreach (var e in enemies) if (!e.Down) { e.BeginRetreat(); EnemiesAutoCleared++; }
    }

    public int CountKills()
    {
        int n = 0;
        foreach (var e in enemies) if (e.KilledByPlayer) n++;
        return n;
    }

    public DemoEnemy NearestVisibleEnemy(Vector3 from, DemoEnemy except = null, bool onlyAtCover = false)
    {
        DemoEnemy best = null;
        float bestD = float.MaxValue;
        foreach (var e in enemies)
        {
            if (!e.Visible || e == except || (onlyAtCover && !e.AtCover)) continue;
            float dist = (e.transform.position - from).sqrMagnitude;
            if (dist < bestD) { bestD = dist; best = e; }
        }
        return best;
    }

    // =====================================================================
    //  인물과 트럭
    // =====================================================================
    void BuildCast()
    {
        var post = new Vector3(0f, 0f, 3.4f); // 북쪽 흙벽 바로 뒤(사격 자리)
        // 김덕만·박용팔은 바르코3D 인물에 북한군 뼈대·동작을 옮겨 붙인 모델(쏠 때 사격 동작). 박용팔은 1차 과제 모래주머니 진지 안, 앞벽 바로 뒤에 선다.
        kim = NewPerson("김덕만", demo.Marker("NPC_KIM") + post, true, 2.2f, true, "Characters/Ally_Kim");
        park = NewPerson("박용팔", demo.Marker("NPC_PARK") + new Vector3(0f, 0f, 1.2f), true, 0.9f, true, "Characters/Ally_Park"); // BAR 라 더 자주 쏜다
        Carry(kim.tr, DemoWeapons.M1, false);   // 김덕만의 총은 기획서에 없어 M1 개런드로 두었다(손 뼈에 쥔다)
        Carry(park.tr, DemoWeapons.Bar, false);
        // 최만수: 기획서대로 살아 있는 모습도, 쓰러지는 장면도 나오지 않는다. 처음부터 참호 구석에 담요를 덮고 누운 형체로만 보인다.
        // 누구인지는 정적 구간에서 박용팔이 "만수야?" 하고 부를 때 처음 드러나므로, 이름표도 그때 이름으로 바뀐다.
        var mansu = NewPerson("담요를 덮은 형체", demo.Marker("PROP_MANSU"), false, 0f, false);
        mansuLabel = demo.IsA || labels.Count == 0 ? null : labels[labels.Count - 1].tr.GetComponent<TextMesh>();
        mansuNamedAt = float.MaxValue;
        foreach (var line in data.lines) if (line.id == "VO_Park_003") mansuNamedAt = line.time;
        var lying = mansu.tr.position;
        mansu.tr.SetPositionAndRotation(new Vector3(lying.x, M01Demo.Ground(lying.x, lying.z) + 0.3f, lying.z), Quaternion.Euler(90f, 0f, 0f));
        mansu.tr.localScale = new Vector3(0.5f, 0.85f, 0.5f);
        mansu.tr.GetComponent<Renderer>().sharedMaterial = Flat(new Color(0.42f, 0.47f, 0.56f)); // 누운 형체: 회청색(모델이 없을 때)
        // 바르코3D 로 만든 "담요를 덮고 누운 사람"(얼굴은 보이지 않는다). 캡슐 자리에 눕힌다.
        var blanket = Resources.Load<GameObject>("Props/Mansu_Blanket");
        if (blanket != null)
        {
            mansu.tr.GetComponent<Renderer>().enabled = false;
            Instantiate(blanket, M01Demo.OnGround(lying), Quaternion.identity, transform).name = "Mansu_Blanket_Model";
        }

        taeseok = NewPerson("윤태석", demo.Marker("SEAT_TAESEOK"), false, 0f);
        taeseok.tr.gameObject.SetActive(false);
        for (int i = 0; i < data.dawn.reinforcements; i++)
        {
            var r = NewPerson("보충병", demo.Marker("REINF_START"), false, 0f);
            r.tr.gameObject.SetActive(false);
            reinforcements.Add(r);
            Carry(r.tr, DemoWeapons.M1Prop, false);
        }

        demo.TryPath("Intro_Path", out introPath);
        demo.TryPath("Reinforcement_Path", out reinforcePath);
        if (introPath != null && !demo.IsA) // A안은 트럭 도입 없이 참호에서 시작한다(김덕만은 처음부터 제자리)
        {
            // 김덕만은 도입 경로를 앞장서 걷고 자기 자리까지 간다.
            kimIntroPath = new Vector3[introPath.Length + 1];
            introPath.CopyTo(kimIntroPath, 0);
            kimIntroPath[introPath.Length] = kim.post;
        }

        // 1차 과제의 모래주머니 진지(벽에 총알이 막힌다).
        var env = Resources.Load<GameObject>("Props/Env_SandbagPosition");
        if (env != null)
        {
            // 박용팔(BAR 사수) 자리, 그리고 참호 남쪽 흙둑 뒤(흙둑·흉벽과 겹치지 않게, 둔덕으로 달리는 길은 막지 않는다).
            foreach (var (id, nm, off) in new[] { ("NPC_PARK", "Park", new Vector3(0f, 0f, 0.6f)), ("NPC_KIM", "East", new Vector3(3f, 0f, -9f)) })
            {
                var g = Instantiate(env, M01Demo.OnGround(demo.Marker(id) + off), Quaternion.identity, transform);
                g.name = "Env_SandbagPosition_" + nm;
                g.transform.localScale = new Vector3(0.85f, 0.62f, 0.85f); // 벽 높이 약 1.0 m: 허리까지. 안에 선 박용팔의 상체가 참호에서 보인다
            }
        }

        // 트럭(임시 상자). 도로를 따라 올라와 하차 지점 옆에 선다.
        var stop = demo.Marker("TRUCK_STOP");
        truckTo = M01Demo.OnGround(stop + new Vector3(2.5f, 0f, 3f));
        truckFrom = demo.IsA ? truckTo : M01Demo.OnGround(truckTo + new Vector3(-1f, 0f, -22f)); // A안: 트럭은 하차 지점에 서 있기만 한다
        truck = New("Truck").transform;
        // 바르코3D → 블렌더로 만든 GMC 2.5톤 트럭(길이 6.9 m). 모델이 없으면 상자로 대신한다.
        var truckModel = Resources.Load<GameObject>("Props/GMC_Truck");
        if (truckModel != null) Instantiate(truckModel, truck, false).name = "GMC_Truck_Model";
        else
        {
            Box(truck, new Vector3(0f, 0.75f, 0f), new Vector3(2.2f, 0.5f, 6.4f));    // 차대
            Box(truck, new Vector3(0f, 1.75f, 2.2f), new Vector3(2.2f, 1.5f, 1.8f));  // 운전석
            Box(truck, new Vector3(-1.05f, 1.5f, -1f), new Vector3(0.1f, 1f, 4.2f)); // 짐칸 옆판
            Box(truck, new Vector3(1.05f, 1.5f, -1f), new Vector3(0.1f, 1f, 4.2f));
        }
        Label(truck, "트럭", 3.2f);

        // 김덕만이 둔덕을 가리키는 것을 대신하는 임시 화살표.
        moundArrow = New("Kim_Points_To_Mound").AddComponent<LineRenderer>();
        moundArrow.useWorldSpace = true; moundArrow.positionCount = 2; moundArrow.sharedMaterial = lineMat;
        moundArrow.startWidth = 0.25f; moundArrow.endWidth = 0.02f;
        moundArrow.startColor = moundArrow.endColor = new Color(1f, 0.9f, 0.3f, 0.9f);
        moundArrow.enabled = false;
    }

    Person NewPerson(string name, Vector3 post, bool fires, float burstGap, bool body = true, string prefab = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(g.GetComponent<Collider>());
        g.name = "Actor_" + name;
        g.transform.SetParent(transform, false);
        g.GetComponent<Renderer>().sharedMaterial = friendMat;
        var p = new Person { tr = g.transform, fires = fires, burstGap = burstGap, post = post };
        Pose(p, post, 0.85f);
        Label(g.transform, name, 1.15f);
        if (body) DemoBody.Attach(g.transform, false, demo, prefab); // prefab 이 없으면 1차 과제의 오석구 모델(Idle·Walk)
        return p;
    }

    static void Pose(Person p, Vector3 xz, float half)
    {
        p.tr.position = new Vector3(xz.x, M01Demo.Ground(xz.x, xz.z) + half, xz.z);
        p.tr.localScale = new Vector3(0.5f, half, 0.5f);
    }

    /// <summary>도입 동안 플레이어 발 위치(트럭 짐칸 → 하차 → 진지까지 걷기). 이동은 자동이다.</summary>
    public Vector3 IntroPlayerFeet(out bool onTruck)
    {
        var intro = data.intro;
        float t = demo.Clock;
        var bed = truck.position + Vector3.up * 1.0f - truck.forward * 1.2f;
        var start = introPath != null ? M01Demo.OnGround(introPath[0]) : truckTo;
        onTruck = t < intro.truckStopAt;
        if (onTruck) return bed;
        if (t < intro.walkFrom) return Vector3.Lerp(bed, start, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(intro.truckStopAt, intro.walkFrom, t)));
        if (introPath == null) return start;
        float f = Mathf.InverseLerp(intro.walkFrom, intro.arriveAt, t);
        return M01Demo.OnGround(M01Demo.AlongPath(introPath, f, out _));
    }

    bool parkToldAmmo;

    void TickCast(float dt)
    {
        // A안: 플레이어 탄약이 떨어지면 박용팔이 탄약 상자를 알려 준다(VO_Park_002, 트리거 "플레이어 탄약 부족").
        var pl = demo.Player;
        if (demo.IsA && !parkToldAmmo && pl != null && pl.mode == DemoPlayer.Mode.Free && pl.SpareClips == 0 && pl.Clip <= 2)
        {
            parkToldAmmo = true;
            demo.Hud.ShowEventLine("박용팔", M01PlanA.ParkAmmoLine, 3.5f);
        }
        var intro = data.intro;
        float t = demo.Clock;

        // 트럭
        truck.position = Vector3.Lerp(truckFrom, truckTo, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, intro.truckStopAt, t)));
        truck.rotation = Quaternion.LookRotation(truckTo - truckFrom);

        // 김덕만: 트럭에서 함께 내려 앞장서 걷는다.
        if (kimIntroPath != null && t < intro.arriveAt)
        {
            float f = Mathf.InverseLerp(intro.walkFrom - 1.5f, intro.arriveAt, t);
            var p = t < intro.walkFrom - 1.5f ? kimIntroPath[0] + new Vector3(-1.5f, 0f, 1f) : M01Demo.AlongPath(kimIntroPath, f, out _);
            Pose(kim, p, 0.85f);
        }
        else Pose(kim, kim.post, 0.85f);

        // 아군 사격: 적이 보이는 동안 제자리에서 쏜다(연출. 적을 쓰러뜨리지는 않는다).
        Shoot(kim);
        Shoot(park);

        // 정적 구간의 부상병 신음은 만수의 것이다(2026-10-13 결정). 소리는 나중에 넣고, 지금은 이름표로만 표시한다.
        if (mansuLabel != null)
            mansuLabel.text = t >= mansuNamedAt ? "최만수 (대답 없음)" : t >= demo.BeatStart("SILENCE") ? "담요를 덮은 형체 (신음)" : "담요를 덮은 형체";

        // 김덕만이 둔덕을 가리킨다(전차가 나타난 뒤 정지할 때까지).
        bool point = demo.InBeat("T34") && t >= data.t34.appearAt + 5f && !demo.Tank.Destroyed;
        if (moundArrow.enabled != point) moundArrow.enabled = point;
        if (point && demo.TryPath(data.t34.moundPath, out var toMound))
        {
            var a = kim.tr.position + Vector3.up * 0.6f;
            var b = M01Demo.OnGround(toMound[toMound.Length - 1]) + Vector3.up * 1.5f;
            moundArrow.SetPosition(0, a);
            moundArrow.SetPosition(1, a + (b - a).normalized * 6f);
        }

        // 새벽: 보충병이 내려오고 태석이 석구 옆에 앉는다.
        var dawn = data.dawn;
        bool dawnOn = t >= dawn.fadeOutAt && reinforcePath != null;
        if (taeseok.tr.gameObject.activeSelf != dawnOn) taeseok.tr.gameObject.SetActive(dawnOn);
        if (dawnOn)
        {
            float f = Mathf.Lerp(0.6f, 1f, Mathf.InverseLerp(dawn.fadeOutAt, dawn.taeseokSitAt, t));
            if (demo.IsA) f = Mathf.Min(f, 0.9f); // A안: 태석은 보충병 무리 속 실루엣으로만(앉지 않는다)
            Pose(taeseok, M01Demo.AlongPath(reinforcePath, f, out _), f >= 1f ? 0.45f : 0.85f); // B안: 다 오면 앉는다
        }
        for (int i = 0; i < reinforcements.Count; i++)
        {
            var r = reinforcements[i];
            if (r.tr.gameObject.activeSelf != dawnOn) r.tr.gameObject.SetActive(dawnOn);
            if (!dawnOn) continue;
            float arrive = dawn.taeseokSitAt + 2f + i * 1.5f;
            float f = Mathf.Lerp(0.35f - i * 0.08f, 0.93f - i * 0.07f, Mathf.InverseLerp(dawn.fadeOutAt, arrive, t));
            var p = M01Demo.AlongPath(reinforcePath, f, out _) + new Vector3(0f, 0f, i % 2 == 0 ? -1.5f : 1.2f);
            Pose(r, p, 0.85f);
        }
    }

    void Shoot(Person p)
    {
        if (!p.fires || Time.time < p.nextShot) return;
        if (!demo.InBeat("FIRST_FLARE") && !demo.InBeat("ASSAULT")) return;
        if ((p.tr.position - new Vector3(p.post.x, p.tr.position.y, p.post.z)).sqrMagnitude > 1f) return; // 자리에 닿기 전
        var target = NearestVisibleEnemy(p.tr.position);
        if (target == null) return;
        p.nextShot = Time.time + p.burstGap * Random.Range(0.7f, 1.4f);
        var shooter = p.tr.GetComponent<DemoBody>();
        if (shooter != null) shooter.LastFired = Time.time; // 사격 동작
        var from = p.tr.position + Vector3.up * 0.55f;
        Flash(from, 6f, 10f, 0.06f);
        Tracer(from, target.Chest + Random.insideUnitSphere * 2.2f, new Color(1f, 0.85f, 0.5f), 0.05f, 0.07f);
        demo.Play(DemoAudio.Shot, 0.3f, 0.9f);
    }

    // =====================================================================
    //  불빛·예광선
    // =====================================================================
    public void Flash(Vector3 pos, float intensity, float range, float seconds)
    {
        var l = flashes[nextFlash];
        l.transform.position = pos;
        l.intensity = intensity; l.range = range; l.enabled = true;
        flashLeft[nextFlash] = seconds;
        nextFlash = (nextFlash + 1) % flashes.Length;
    }

    public void Tracer(Vector3 from, Vector3 to, Color color, float width, float seconds)
    {
        // 눈앞을 스치는 예광선은 화면을 가로지르는 굵은 띠로 보인다. 눈에서 2.5 m 안쪽 부분은 잘라 낸다.
        var cam = Camera.main;
        if (cam != null)
        {
            var eye = cam.transform.position;
            const float keep = 2.5f;
            for (int i = 0; i < 12 && (to - eye).sqrMagnitude < keep * keep && (to - from).sqrMagnitude > 0.25f; i++) to = Vector3.Lerp(to, from, 0.25f);
            for (int i = 0; i < 12 && (from - eye).sqrMagnitude < keep * keep && (to - from).sqrMagnitude > 0.25f; i++) from = Vector3.Lerp(from, to, 0.25f);
            if ((to - eye).sqrMagnitude < keep * keep && (from - eye).sqrMagnitude < keep * keep) return; // 전부 눈앞이면 그리지 않는다
        }
        var lr = tracers[nextTracer];
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startColor = lr.endColor = color;
        lr.widthMultiplier = width;
        lr.enabled = true;
        tracerLeft[nextTracer] = seconds;
        nextTracer = (nextTracer + 1) % tracers.Length;
    }

    void TickFx(float dt)
    {
        for (int i = 0; i < flashes.Length; i++)
            if (flashLeft[i] > 0f && (flashLeft[i] -= dt) <= 0f) flashes[i].enabled = false;
        for (int i = 0; i < tracers.Length; i++)
            if (tracerLeft[i] > 0f && (tracerLeft[i] -= dt) <= 0f) tracers[i].enabled = false;
    }

    // =====================================================================
    //  도우미
    // =====================================================================
    public GameObject New(string name)
    {
        var g = new GameObject(name);
        g.transform.SetParent(transform, false);
        return g;
    }

    void Box(Transform parent, Vector3 pos, Vector3 size)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = propMat;
    }

    // 임시 도형은 밤에도 편이 구분되도록 빛을 받지 않는 단색으로 칠한다.
    public static Material Flat(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
        return m;
    }

    /// <summary>이름표: 카메라를 바라보고, 멀어도 읽히게 거리에 맞춰 커진다. F11 로 함께 숨는다.</summary>
    public void Label(Transform target, string text, float height)
    {
        if (demo != null && demo.IsA) return; // A안 화면에는 이름표를 띄우지 않는다(기획서 5장 HUD)
        if (labelFont == null) labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (labelFont == null) return;
        var tm = New("Label_" + text).AddComponent<TextMesh>();
        tm.font = labelFont;
        tm.GetComponent<MeshRenderer>().sharedMaterial = labelFont.material;
        tm.text = text;
        tm.fontSize = 64; tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.LowerCenter; tm.alignment = TextAlignment.Center;
        labels.Add(new NameTag { tr = tm.transform, target = target, height = height });
    }

    // =====================================================================
    //  들고 있는 총: 캡슐 옆에 실제 무기 모델을 붙여 보여 준다
    //  (캡슐은 위아래로 눌려 있어서 자식으로 달지 않고 매 프레임 자리를 맞춘다)
    // =====================================================================
    class Carried { public Transform weapon, owner; public bool enemy; public Vector3 last, face; }
    readonly List<Carried> carried = new List<Carried>();
    public int WeaponsCarried => carried.Count;

    void Carry(Transform owner, string resource, bool enemy)
    {
        var w = DemoWeapons.Load(resource);
        if (w == null) return;
        w.SetParent(transform, false);
        carried.Add(new Carried { weapon = w, owner = owner, enemy = enemy, last = owner.position, face = enemy ? Vector3.back : Vector3.forward });
    }

    void TickCarried()
    {
        foreach (var c in carried)
        {
            bool show = c.owner != null && c.owner.gameObject.activeInHierarchy;
            if (c.weapon.gameObject.activeSelf != show) c.weapon.gameObject.SetActive(show);
            if (!show) continue;
            var p = c.owner.position;
            var body = c.owner.GetComponent<DemoBody>();
            if (body != null && body.Hand != null)
            {
                // 적·김덕만·박용팔: 동작(돌격·엄폐·사격)을 따라 오른손 뼈에 총을 쥔다. 쓰러지면 아래(땅에 놓기)로 간다.
                var e = c.owner.GetComponent<DemoEnemy>();
                if (e == null || !e.Down)
                {
                    // 두 손으로 쥔다: 손잡이(총의 중심점)는 오른손바닥에, 총열은 오른손 → 왼손 방향으로.
                    // 손 뼈는 Blender 에서 메시의 쥔 손 가운데로 옮겨 두었다(rig_allies.py place_hand_bones).
                    Vector3 Palm(Transform h) => h.position;
                    var right = Palm(body.Hand);
                    var fwd = body.Facing;
                    if (body.LeftHand != null)
                    {
                        var toLeft = Palm(body.LeftHand) - right;
                        if (toLeft.magnitude > 0.12f && Vector3.Dot(toLeft.normalized, fwd) > 0.2f)
                            fwd = Vector3.Slerp(fwd, toLeft.normalized, 0.8f);
                    }
                    c.weapon.SetPositionAndRotation(right, Quaternion.LookRotation(fwd, Vector3.up));
                    continue;
                }
            }
            if (c.owner.up.y < 0.5f)
            {
                // 쓰러졌다: 총은 옆 땅에 놓인다.
                var side = p + c.owner.right * 0.45f;
                c.weapon.SetPositionAndRotation(new Vector3(side.x, M01Demo.Ground(side.x, side.z) + 0.05f, side.z), Quaternion.LookRotation(c.face) * Quaternion.Euler(0f, 25f, 90f));
                continue;
            }
            var move = p - c.last; move.y = 0f;
            c.last = p;
            if (move.sqrMagnitude > 0.000004f) c.face = move.normalized;                       // 걷는 중: 가는 쪽
            else if (c.enemy && demo.Player != null) { var to = demo.Player.transform.position - p; to.y = 0f; if (to.sqrMagnitude > 1f) c.face = to.normalized; } // 멈춘 적: 진지 쪽
            else if (!c.enemy) c.face = Vector3.forward;                                       // 멈춘 아군: 북쪽 비탈 쪽
            var rot = Quaternion.LookRotation(c.face);
            c.weapon.SetPositionAndRotation(p + rot * new Vector3(0.24f, 0.12f, 0.22f), rot * Quaternion.Euler(-4f, 0f, 0f));
        }
    }

    void TickLabels()
    {
        var c = demo.cam != null ? demo.cam.transform : null;
        if (c == null) return;
        foreach (var l in labels)
        {
            bool show = !demo.UiHidden && l.target != null && l.target.gameObject.activeInHierarchy;
            if (l.tr.gameObject.activeSelf != show) l.tr.gameObject.SetActive(show);
            if (!show) continue;
            var pos = l.target.position + Vector3.up * l.height;
            l.tr.position = pos;
            l.tr.rotation = c.rotation;
            l.tr.localScale = Vector3.one * Mathf.Clamp(Vector3.Distance(c.position, pos) * 0.045f, 0.25f, 6f);
        }
    }
}
