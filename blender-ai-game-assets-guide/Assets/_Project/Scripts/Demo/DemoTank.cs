using UnityEngine;

/// <summary>
/// T-34 (기획서 4장 미션 전용 이벤트). 임시 도형은 실제 크기에 맞춘 눕힌 캡슐 세 개(차체·포탑·포신)다.
/// 1:45 도로 북쪽 끝에 등장 → 진지에서 보이는 곳까지 빠르게 내려온 뒤 2:10 까지 정지 지점으로 천천히 와서 진지 쪽으로 차체와 포탑을 돌린다.
/// 주포는 정해진 시각에 진지 앞으로 쏘고(연출), 차체 기관총은 둔덕으로 달리는 시연자 주변에 떨어진다.
/// 정면 명중은 불꽃과 튕기는 소리만, 측면 첫 발에 연기, 두 번째 발에 정지와 화염.
/// 실패는 없다. 정해진 시각까지 멈추지 않으면 자동으로 정지 연출이 나온다.
/// </summary>
public class DemoTank : MonoBehaviour
{
    public Transform Root { get; private set; }
    public bool Appeared => Root != null && Root.gameObject.activeSelf;
    public bool Destroyed { get; private set; }
    public bool DestroyedByPlayer { get; private set; }
    public float DestroyedAt { get; private set; } = -1f;
    public int FrontHits { get; private set; }
    public int SideHits { get; private set; }
    /// <summary>A안 누적 피해: 측면·후면 1, 정면 0.1. 2 가 되면 정지.</summary>
    public float Damage { get; private set; }
    public int CannonShots { get; private set; }
    public Vector3 AimPoint => Root.position + Vector3.up * 1.2f;

    M01Demo demo;
    M01DemoData.T34Data td;
    Transform turret;
    Vector3[] path;
    Vector3 trench;
    int nextCannon;
    float mgAcc;
    Light burn;
    ParticleSystem smoke;
    Material smokeMat;

    public void Init(M01Demo d)
    {
        demo = d;
        td = d.data.t34;
        if (!d.TryPath("T34_Path", out path) || path.Length < 2) { Debug.LogWarning("[FiveRiver] 경로 없음: T34_Path"); return; }
        trench = d.Marker("SPAWN_PLAYER");

        Root = d.Stage.New("T34").transform;
        turret = new GameObject("Turret").transform;
        turret.SetParent(Root, false);
        turret.localPosition = new Vector3(0f, 2.1f, 0.3f);
        // 바르코3D → 블렌더로 만든 T-34-85(포신 포함 길이 8.1 m). 한 덩어리 모델이라 포탑은 차체와 같이 돈다.
        var model = Resources.Load<GameObject>("Props/T34_85");
        if (model != null) Instantiate(model, Root, false).name = "T34_85_Model";
        else
        {
            var mat = DemoStage.Flat(new Color(0.62f, 0.14f, 0.10f)); // 모델이 없을 때: 짙은 빨강 캡슐
            var lying = Quaternion.Euler(90f, 0f, 0f);
            Part(Root, new Vector3(0f, 1f, 0f), new Vector3(3f, 3.3f, 2f), lying, mat);
            Part(turret, Vector3.zero, new Vector3(1.8f, 1.3f, 1.2f), lying, mat);
            Part(turret, new Vector3(0f, 0f, 2.6f), new Vector3(0.25f, 1.7f, 0.25f), lying, mat);
        }
        var box = Root.gameObject.AddComponent<BoxCollider>();                                  // 로켓이 맞는 범위
        box.center = new Vector3(0f, 1.3f, 0f);
        box.size = new Vector3(3f, 2.6f, 6.6f);
        if (model != null)
        {
            // 모델 크기에 맞춘다(포신은 빼고 차체·포탑만: 아래쪽 75% 높이, 길이는 모델의 80%).
            var b = new Bounds(Root.position, Vector3.zero);
            foreach (var r in Root.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var c = Root.InverseTransformPoint(b.center);
            box.center = new Vector3(c.x, b.size.y * 0.4f, c.z - b.size.z * 0.08f);
            box.size = new Vector3(b.size.x * 0.9f, b.size.y * 0.8f, b.size.z * 0.8f);
        }
        if (!d.IsA) d.Stage.Label(Root, "T-34", 3.6f); // A안 화면에는 기획서 4장 HUD 말고 다른 글씨를 띄우지 않는다
        Root.gameObject.SetActive(false);
        Place(0f, true);
    }

    public bool Owns(Collider c) => Root != null && c != null && c.transform.IsChildOf(Root);

    static void Part(Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localRotation = rot;
        g.GetComponent<Renderer>().sharedMaterial = mat;
    }

    public void Tick(float dt)
    {
        if (Root == null) return;
        float t = demo.Clock;
        bool appear = t >= td.appearAt;
        if (Root.gameObject.activeSelf != appear) Root.gameObject.SetActive(appear);
        if (!appear) return;
        if (smoke != null) smoke.transform.position = Root.position + Vector3.up * 1.8f;

        if (!Destroyed)
        {
            // 진지에서 보이는 곳까지는 빠르게, 그 뒤로는 정지 지점까지 천천히 내려온다.
            float move = t < td.fastUntil
                ? td.fastFraction * Mathf.InverseLerp(td.appearAt, td.fastUntil, t)
                : Mathf.Lerp(td.fastFraction, 1f, Mathf.InverseLerp(td.fastUntil, td.arriveAt, t));
            Place(move, false);
            if (demo.IsA && t > td.arriveAt)
            {
                // A안: 느리지만 멈추지 않는다. 도로 굽이에서 서쪽(진지 쪽)으로 계속 기어 와 실패선(등장 45초)에 닿는다.
                // 이때 D 둔덕에서는 차체 옆구리가 보인다.
                var end = path[path.Length - 1];
                Root.position = M01Demo.OnGround(end + Vector3.left * (t - td.arriveAt) * 0.7f);
                Root.rotation = Quaternion.Slerp(Root.rotation, Quaternion.LookRotation(Vector3.left), 1f - Mathf.Exp(-1.5f * dt));
            }
            if (move >= 1f)
            {
                // 멈춘 뒤 포탑을 진지 쪽으로 돌린다.
                var dir = trench - turret.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    turret.rotation = Quaternion.RotateTowards(turret.rotation, Quaternion.LookRotation(dir), 14f * dt);
            }

            // 주포(연출): 진지 앞에 떨어진다.
            while (nextCannon < td.cannonTimes.Count && td.cannonTimes[nextCannon] <= t)
            {
                if (t - td.cannonTimes[nextCannon] < 2f) FireCannon();
                nextCannon++;
            }

            // 차체 기관총: 시연자 주변에 떨어진다.
            // A안: 기관총은 플레이어가 진지를 나와 D 둔덕으로 갈 때 그 근처에 쏜다(한 발 10).
            bool outOfTrench = demo.Player != null && Vector3.Distance(demo.Player.transform.position, demo.Marker("SPAWN_PLAYER")) > 6f;
            if (t >= td.mgFrom && t < td.mgTo && demo.Player != null && (!demo.IsA || outOfTrench))
            {
                mgAcc += dt * 9f;
                for (; mgAcc >= 1f; mgAcc--)
                {
                    var from = Root.position + Vector3.up * 1.4f + Root.forward * 3f;
                    var near = demo.Player.EyePosition + Random.insideUnitSphere * 3.5f;
                    demo.Stage.Tracer(from, near, new Color(0.8f, 1f, 0.7f), 0.06f, 0.06f);
                    demo.Stage.Flash(from, 8f, 12f, 0.05f);
                    if (Random.value < 0.5f) demo.Play(DemoAudio.EnemyShot, 0.35f, 1.35f); // 차체 기관총
                    if (Random.value < 0.06f) demo.Player.Grazed();
                    // A안: 서 있으면 기관총에 맞을 수 있다(엎드리면 덜 맞는다).
                    if (demo.IsA && Random.value < (demo.Player.Prone ? 0.02f : 0.05f)) demo.Player.TakeHit(M01PlanA.MgDamage, from);
                }
            }

            if (t >= td.autoStopAt) Knockout(false);
        }

        if (burn != null)
        {
            float s = t - DestroyedAt;
            burn.intensity = 900f * Mathf.Exp(-s * 4f) + 60f * (0.7f + 0.3f * Mathf.PerlinNoise(s * 7f, 0f));
        }
    }

    void Place(float move, bool snap)
    {
        var pos = M01Demo.AlongPath(path, move, out var forward);
        Root.position = M01Demo.OnGround(pos);
        if (forward.sqrMagnitude < 0.0001f) return;
        var want = Quaternion.LookRotation(forward);
        Root.rotation = snap ? want : Quaternion.Slerp(Root.rotation, want, 1f - Mathf.Exp(-2f * Time.deltaTime));
    }

    void FireCannon()
    {
        CannonShots++;
        var muzzle = turret.position + turret.forward * 4.5f;
        var impact = M01Demo.OnGround(trench + new Vector3(Random.Range(-9f, 9f), 0f, Random.Range(11f, 16f)));
        demo.Stage.Flash(muzzle, 400f, 60f, 0.12f);
        demo.Stage.Flash(impact + Vector3.up, 500f, 45f, 0.3f);
        demo.Stage.Tracer(muzzle, impact, new Color(1f, 0.8f, 0.5f), 0.2f, 0.08f);
        demo.Play(DemoAudio.Cannon, 1f);
        if (demo.Player != null) { demo.Player.AddShake(1f); demo.Player.Grazed(); }
    }

    /// <summary>
    /// 바주카 로켓이 맞았다. 측면 판정은 각도로 한다: 쏜 자리가 차체 앞쪽에서 sideAngle(60도) 넘게 벗어나 있으면 측면, 아니면 정면이다.
    /// 전차는 정지 지점에서 진지 쪽으로 차체를 돌리므로, 진지에서는 정면이고 둔덕(D)에서는 옆구리가 보인다.
    /// </summary>
    public void RocketHit(Vector3 point, Vector3 shooterFeet)
    {
        if (Destroyed) return;
        var d = shooterFeet - Root.position;
        d.y = 0f;
        var bow = Root.forward;
        bow.y = 0f;
        float angle = Vector3.Angle(bow, d);
        bool side = angle >= td.sideAngle;
        if (demo.IsA) Damage += side ? 1f : M01PlanA.FrontEffect; // A안: 정면은 피해 10%만
        if (!side)
        {
            FrontHits++;
            if (demo.IsA && Damage >= M01PlanA.StopDamage) Knockout(true);
            demo.Stage.Flash(point, 200f, 25f, 0.15f); // 불꽃만 튄다
            demo.Play(DemoAudio.Ricochet, 0.9f);
            Debug.Log($"[FiveRiver] {M01Demo.Stamp(demo.Clock)} T-34 정면 명중: 튕김 (차체 앞에서 {angle:F0}도)");
            return;
        }
        SideHits++;
        demo.Stage.Flash(point, 500f, 40f, 0.25f);
        demo.Play(DemoAudio.Boom, 0.9f);
        Debug.Log($"[FiveRiver] {M01Demo.Stamp(demo.Clock)} T-34 측면 명중 {SideHits}발째 (차체 앞에서 {angle:F0}도)");
        if (demo.IsA ? Damage >= M01PlanA.StopDamage : SideHits >= td.sideHitsToStop) Knockout(true);
        else StartSmoke(6f, 2.5f);
    }

    void Knockout(bool byPlayer)
    {
        if (Destroyed) return;
        Destroyed = true;
        DestroyedByPlayer = byPlayer;
        DestroyedAt = demo.Clock;
        if (byPlayer) demo.AddTankScore();
        turret.localRotation *= Quaternion.Euler(-6f, 22f, 5f); // 포탑이 틀어진 채 멈춘다
        burn = demo.Stage.New("T34_Fire").AddComponent<Light>();
        burn.transform.position = Root.position + Vector3.up * 2.5f;
        burn.type = LightType.Point; burn.range = 60f; burn.color = new Color(1f, 0.55f, 0.25f); burn.shadows = LightShadows.None;
        StartSmoke(16f, 4f);
        demo.Play(DemoAudio.Boom, 1f, 0.7f);
        if (demo.Player != null) demo.Player.AddShake(0.7f);
        Debug.Log($"[FiveRiver] {M01Demo.Stamp(demo.Clock)} T-34 정지와 화염 ({(byPlayer ? "측면 명중" : "자동")})");
    }

    void StartSmoke(float rate, float size)
    {
        if (smoke == null)
        {
            var go = demo.Stage.New("T34_Smoke");
            go.transform.position = Root.position + Vector3.up * 1.8f;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            smoke = go.AddComponent<ParticleSystem>();
            var main = smoke.main;
            main.startLifetime = 10f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.07f, 0.07f, 0.07f, 0.75f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var shape = smoke.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 10f; shape.radius = 0.8f;
            var force = smoke.forceOverLifetime; force.enabled = true; force.space = ParticleSystemSimulationSpace.World;
            force.x = new ParticleSystem.MinMaxCurve(-0.5f); // 계곡 바람에 서쪽으로 흐른다
            var grow = smoke.sizeOverLifetime; grow.enabled = true; grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.8f));
            var col = smoke.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SmokeMat();
        }
        var m = smoke.main; m.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
        var emission = smoke.emission; emission.rateOverTime = rate;
    }

    Material SmokeMat()
    {
        if (smokeMat != null) return smokeMat;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n - 1, n - 1) * 0.5f) / (n * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                px[y * n + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels32(px); tex.Apply();
        smokeMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        return smokeMat;
    }
}
