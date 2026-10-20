using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 시연자가 조작하는 오석구(1인칭). 이동·엎드리기, M1 개런드(8발 클립, 다 쏘면 '핑'), 탄약 상자 보충, 바주카.
/// 조작: WASD 이동, 마우스 시점, Shift 달리기, C 엎드리기, 왼쪽 클릭 사격, R 장전, F 탄약 보충, 1 M1, 2 바주카.
/// 실패는 없다. 맞으면 화면 가장자리 연출만 나온다.
/// </summary>
public class DemoPlayer : MonoBehaviour
{
    public enum Mode { Free, LookOnly, None }

    public Mode mode = Mode.LookOnly;
    public bool Prone { get; private set; }
    /// <summary>1 = M1 개런드, 2 = 바주카</summary>
    public int Weapon { get; private set; } = 1;
    public int Clip { get; private set; }
    public int SpareClips { get; private set; }
    public int Rockets { get; private set; }
    public bool RocketLoaded { get; private set; }
    public bool Reloading => reloadUntil > 0f;
    /// <summary>화면 가장자리 연출 세기(0~1)</summary>
    public float Hurt { get; private set; }
    public float Shake { get; private set; }

    // A안: 체력. 적 총알에 맞으면 깎이고, 4초 동안 안 맞으면 천천히 찬다. 0 이 되면 전사(실패).
    public float Health { get; private set; } = 100f;
    public const float MaxHealth = 100f;
    public bool Dead => Health <= 0f;
    float lastHitAt = -10f;
    /// <summary>마지막으로 쏜 시각. 적이 총소리로 플레이어를 알아챈다.</summary>
    public float LastShotAt { get; private set; } = -10f;
    /// <summary>적을 맞힌 시각과 머리 명중 여부(히트마커).</summary>
    public float HitMarkAt { get; private set; } = -10f;
    public bool HitMarkKill { get; private set; }
    /// <summary>맞은 방향(화면 테두리에 표시). 플레이어 기준 수평 각도.</summary>
    public float HurtFromAngle { get; private set; }
    public int Hits { get; private set; }

    // A안 조작(기획서 4장·6장): 우클릭 조준, Z 엎드리기, 달리기 5초, 탄은 R 을 누를 때만 잠깐 보인다
    public bool Aiming { get; private set; }
    /// <summary>검사 도구가 자동 조작 중에 조준 화면을 확인할 때 켠다.</summary>
    public bool ForceAim;
    public float Stamina { get; private set; } = M01PlanA.SprintSeconds;
    /// <summary>남은 탄을 화면에 잠깐 보여 준 시각(R, 장전, 보충).</summary>
    public float AmmoShownAt { get; private set; } = -10f;
    float baseFov;

    // 기록(검사용)
    public int ShotsFired { get; private set; }
    public int Pings { get; private set; }
    public int Resupplies { get; private set; }
    public int RocketsFired { get; private set; }
    public int Grazes { get; private set; }

    // 자동 조작(DemoAutoPilot 이 쓴다)
    [HideInInspector] public bool auto;
    [HideInInspector] public bool autoHasMove, autoSprint, autoHasLook;
    [HideInInspector] public Vector3 autoMove, autoLook;
    [HideInInspector] public float autoLookSpeed = 120f;

    public Vector3 EyePosition => cam != null ? cam.transform.position : transform.position + Vector3.up * eye;
    public Vector3 Forward => cam != null ? cam.transform.forward : transform.forward;

    M01Demo demo;
    M01DemoData.PlayerData pd;
    M01DemoData.WeaponData wd;
    CharacterController cc;
    Camera cam;
    float yaw, pitch, eye, eyeTarget, velY;
    float nextFire, reloadUntil;

    // 손에 든 총(1인칭). 바르코3D → 블렌더로 만든 모델을 Resources/Weapons 에서 읽는다. 없으면 총 없이 진행한다.
    Transform viewM1, viewBazooka;
    float aimBlend; // 0 = 허리, 1 = 조준
    float kick, lower;
    public bool HasWeaponModels => viewM1 != null && viewBazooka != null;
    Vector3 ammoBox;

    public void Init(M01Demo d)
    {
        demo = d;
        pd = d.data.player;
        wd = d.data.weapon;
        cam = d.cam;
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0f, 0.92f, 0f);
        cc.stepOffset = 0.4f; cc.slopeLimit = 55f;
        Clip = wd.m1ClipSize;
        SpareClips = wd.m1SpareClips;
        Rockets = wd.bazookaRockets;
        RocketLoaded = Rockets > 0;
        eye = eyeTarget = pd.eyeStand;
        baseFov = cam != null ? cam.fieldOfView : 60f;
        ammoBox = d.Marker(wd.ammoBoxMarker);
        Place(M01Demo.OnGround(d.Marker("SPAWN_PLAYER")));
        // 중심점은 M1 이 방아쇠, 바주카가 손잡이다. 총구까지 거리가 M1 은 긴 쪽, 바주카는 짧은 쪽이다.
        viewM1 = LoadViewModel(DemoWeapons.M1View, 1.10f, true); // 1차 과제의 M1 개런드를 1인칭용으로 다시 다듬은 것(조준선 맞춤)
        if (viewM1 == null) viewM1 = LoadViewModel(DemoWeapons.M1Prop, 1.10f, true);
        if (cam != null) cam.nearClipPlane = 0.02f; // 눈앞 11cm 의 가늠자가 잘리지 않게
        viewBazooka = LoadViewModel(DemoWeapons.Bazooka, 1.524f, false);
        if (!Application.isBatchMode) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
    }

    /// <summary>발 위치를 바로 옮긴다(도입·새벽처럼 시연이 자리를 정해 줄 때).</summary>
    public void Place(Vector3 feet)
    {
        cc.enabled = false;
        transform.position = feet;
        cc.enabled = mode == Mode.Free;
    }

    public void SetMode(Mode m, float eyeHeight)
    {
        mode = m;
        cc.enabled = m == Mode.Free;
        if (m != Mode.Free) Prone = false;
        eyeTarget = eyeHeight;
    }

    public void Face(Vector3 worldPoint)
    {
        var dir = worldPoint - EyePosition;
        if (dir.sqrMagnitude < 0.01f) return;
        var e = Quaternion.LookRotation(dir).eulerAngles;
        yaw = e.y;
        pitch = Mathf.DeltaAngle(0f, e.x);
    }

    public float AngleTo(Vector3 worldPoint) => Vector3.Angle(Forward, worldPoint - EyePosition);

    public void Tick(float dt)
    {
        if (demo == null) return;
        var kb = auto ? null : Keyboard.current;
        var mouse = auto ? null : Mouse.current;

        // ---- 시점 ----
        if (mode != Mode.None)
        {
            if (auto)
            {
                if (autoHasLook)
                {
                    var dir = autoLook - EyePosition;
                    if (dir.sqrMagnitude > 0.01f)
                    {
                        var e = Quaternion.LookRotation(dir).eulerAngles;
                        float step = autoLookSpeed * dt;
                        yaw = Mathf.MoveTowardsAngle(yaw, e.y, step);
                        pitch = Mathf.MoveTowardsAngle(pitch, Mathf.DeltaAngle(0f, e.x), step);
                    }
                }
            }
            else if (mouse != null)
            {
                var delta = mouse.delta.ReadValue() * pd.mouseSensitivity;
                yaw += delta.x;
                pitch -= delta.y;
            }
            pitch = Mathf.Clamp(pitch, -80f, 80f);
        }

        // ---- 이동 ----
        if (mode == Mode.Free)
        {
            Vector3 wish = Vector3.zero;
            bool sprint = false;
            if (auto)
            {
                Aiming = ForceAim;
                if (autoHasMove)
                {
                    wish = autoMove - transform.position;
                    wish.y = 0f;
                    wish = wish.magnitude > 0.25f ? wish.normalized : Vector3.zero;
                    sprint = autoSprint;
                }
            }
            else if (kb != null)
            {
                float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                wish = Quaternion.Euler(0f, yaw, 0f) * new Vector3(x, 0f, z);
                if (wish.sqrMagnitude > 1f) wish.Normalize();
                sprint = kb.leftShiftKey.isPressed && (!demo.IsA || Stamina > 0f) && wish.sqrMagnitude > 0.01f;
                Aiming = demo.IsA && mouse != null && mouse.rightButton.isPressed && !sprint;
                if (kb.cKey.wasPressedThisFrame || (demo.IsA && kb.zKey.wasPressedThisFrame)) SetProne(!Prone);
                if (kb.digit1Key.wasPressedThisFrame) Switch(1);
                if (kb.digit2Key.wasPressedThisFrame) Switch(2);
                if (kb.rKey.wasPressedThisFrame) { AmmoShownAt = Time.time; Reload(); }
                if (kb.fKey.wasPressedThisFrame) Interact();
                if (mouse != null && (Weapon == 1 ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame)) TryFire();
            }
            if (Prone && sprint) SetProne(false); // 달리면 일어난다
            float speed = Prone ? pd.proneSpeed : sprint ? pd.sprintSpeed : pd.walkSpeed;
            velY = cc.isGrounded ? -2f : velY - 20f * dt;
            cc.Move((wish * speed + Vector3.up * velY) * dt);
            // 달리기 지속 5초: 달리면 줄고, 걷거나 서 있으면 천천히 찬다.
            Stamina = sprint ? Mathf.Max(0f, Stamina - dt) : Mathf.Min(M01PlanA.SprintSeconds, Stamina + dt * 0.8f);
        }

        // ---- 장전 ----
        if (reloadUntil > 0f && Time.time >= reloadUntil)
        {
            reloadUntil = 0f;
            if (Weapon == 1) { SpareClips--; Clip = wd.m1ClipSize; }
            else RocketLoaded = true;
        }

        // A안 회복: 5초 동안 안 맞으면 체력 50%까지만 찬다(완전 회복은 없다).
        if (demo.IsA && !Dead && Time.time - lastHitAt > M01PlanA.RegenDelay && Health < M01PlanA.RegenCap) Health = Mathf.Min(M01PlanA.RegenCap, Health + M01PlanA.RegenPerSecond * dt);
        if (cam != null && demo.IsA) cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, Aiming ? baseFov * 0.7f : baseFov, dt * 90f); // 조준하면 시야가 좁아진다
        Hurt = Mathf.MoveTowards(Hurt, 0f, dt * 0.9f);
        Shake = Mathf.MoveTowards(Shake, 0f, dt * 2.5f);
        eye = Mathf.MoveTowards(eye, eyeTarget, dt * 3f);

        if (cam != null)
        {
            var jitter = Shake > 0f ? Random.insideUnitSphere * (Shake * 0.08f) : Vector3.zero;
            cam.transform.SetPositionAndRotation(transform.position + Vector3.up * eye + jitter, Quaternion.Euler(pitch, yaw, 0f));
            UpdateViewModel(dt);
        }
    }

    // =====================================================================
    //  손에 든 총
    // =====================================================================
    Transform LoadViewModel(string resource, float realLength, bool muzzleIsFarEnd)
    {
        var holder = DemoWeapons.Load(resource, realLength, muzzleIsFarEnd);
        if (holder != null) holder.gameObject.SetActive(false);
        return holder;
    }

    void UpdateViewModel(float dt)
    {
        kick = Mathf.MoveTowards(kick, 0f, dt * 9f);
        aimBlend = Mathf.MoveTowards(aimBlend, Aiming ? 1f : 0f, dt * 6f); // 약 0.17초에 올라온다
        lower = Mathf.MoveTowards(lower, Reloading || Time.time < nextFire - 0.3f ? 1f : 0f, dt * 4f); // 장전·교체 중에는 내린다
        bool show = mode == Mode.Free && !demo.UiHiddenWeapons;
        Place(viewM1, show && Weapon == 1, Vector3.Lerp(new Vector3(0.15f, -0.145f, 0.27f), new Vector3(0f, 0f, 0.08f), aimBlend), 1f, (1f - aimBlend) * 4f); // 원점 = 가늠자 구멍. 허리: 오른쪽 아래, 총구가 살짝 안쪽. 조준: 눈앞 8cm 정가운데(판이 시야 가장자리를 가리고 구멍이 크게 보인다)
        Place(viewBazooka, show && Weapon == 2, new Vector3(0.24f, -0.27f, 0.34f), 0.4f); // 어깨에 멘 채 장전하므로 조금만 내린다
    }

    void Place(Transform view, bool show, Vector3 offset, float lowerScale, float yawIn = 0f)
    {
        if (view == null) return;
        if (view.gameObject.activeSelf != show) view.gameObject.SetActive(show);
        if (!show) return;
        var c = cam.transform;
        float down = lower * lowerScale;
        offset += new Vector3(0f, -0.22f * down, -0.035f * kick);
        view.SetPositionAndRotation(c.TransformPoint(offset), c.rotation * Quaternion.Euler(-2.5f * kick + 25f * down, -yawIn, 0f)); // yawIn: 총구를 화면 가운데 쪽으로 살짝 돌린다
    }

    public void SetProne(bool on)
    {
        if (mode != Mode.Free) return;
        Prone = on;
        eyeTarget = on ? pd.eyeProne : pd.eyeStand;
    }

    public void Switch(int weapon)
    {
        if (weapon == Weapon || (weapon != 1 && weapon != 2)) return;
        Weapon = weapon;
        reloadUntil = 0f;
        nextFire = Time.time + 0.6f; // 무기를 바꾸는 시간
        if (Weapon == 2 && !RocketLoaded && Rockets > 0) reloadUntil = Time.time + wd.bazookaReloadSeconds;
        if (Weapon == 1 && Clip == 0) Reload();
    }

    public void Reload()
    {
        if (Reloading) return;
        // 다 쏘고 갈아 끼우면 2.2초, 탄이 남았을 때 빼고 끼우면 3.5초(A안). 다 쏘고 갈아 끼우는 쪽이 빠르다.
        if (Weapon == 1 && SpareClips > 0 && Clip < wd.m1ClipSize) { reloadUntil = Time.time + (demo.IsA && Clip > 0 ? M01PlanA.ReloadPartial : wd.m1ReloadSeconds); demo.Play(DemoAudio.Reload, 0.7f); AmmoShownAt = Time.time; }
        if (Weapon == 2 && !RocketLoaded && Rockets > 0) reloadUntil = Time.time + wd.bazookaReloadSeconds;
    }

    public bool NearAmmo
    {
        get { var d = transform.position - ammoBox; d.y = 0f; return mode == Mode.Free && d.magnitude <= wd.interactRange; }
    }

    /// <summary>탄약 상자 가까이에서 F: 예비 클립을 채운다.</summary>
    public bool Interact()
    {
        if (mode != Mode.Free) return false;
        var d = transform.position - ammoBox;
        d.y = 0f;
        if (d.magnitude > wd.interactRange) return false;
        SpareClips = Mathf.Max(SpareClips, wd.ammoBoxClips);
        AmmoShownAt = Time.time;
        Resupplies++;
        demo.Play(DemoAudio.Click, 0.8f);
        if (Weapon == 1 && Clip == 0) Reload();
        return true;
    }

    public bool TryFire()
    {
        if (mode != Mode.Free || Reloading || Time.time < nextFire) return false;
        var origin = EyePosition;
        var dir = Forward;
        if (demo.IsA && Weapon == 1)
        {
            // 조준 흔들림: 허리 사격 1.1도, 조준 0.25도. 엎드리면 40% 줄어든다.
            float spread = (Aiming ? 0.25f : 1.1f) * (Prone ? M01PlanA.ProneSteadyFactor : 1f);
            dir = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * (Quaternion.AngleAxis(Random.Range(0f, spread), cam.transform.right) * dir);
        }
        var muzzle = Aiming ? origin + dir * 0.8f - cam.transform.up * 0.03f : origin + cam.transform.right * 0.12f - cam.transform.up * 0.12f + dir * 0.7f;
        bool hitSomething = Physics.Raycast(origin, dir, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore);
        var end = hitSomething ? hit.point : origin + dir * 300f;

        if (Weapon == 1)
        {
            if (Clip <= 0)
            {
                if (SpareClips > 0) Reload(); else demo.Play(DemoAudio.Click, 0.5f);
                nextFire = Time.time + 0.3f;
                return false;
            }
            Clip--;
            ShotsFired++;
            LastShotAt = Time.time;
            nextFire = Time.time + wd.m1FireInterval;
            demo.Play(DemoAudio.Shot, 0.9f, Random.Range(0.96f, 1.04f));
            kick = 1f;
            if (demo.IsA) pitch -= 1.4f * (Prone ? M01PlanA.ProneSteadyFactor : 1f); // 반동: 총구가 들린다
            demo.Stage.Flash(muzzle, 7f, 11f, 0.06f);
            demo.Stage.Tracer(muzzle, end, new Color(1f, 0.85f, 0.5f), 0.05f, 0.07f);
            if (hitSomething)
            {
                var enemy = hit.collider.GetComponentInParent<DemoEnemy>();
                if (enemy != null && !enemy.Down)
                {
                    Hits++;
                    HitMarkKill = enemy.Hit(hit.point, wd.m1BodyDamage, wd.m1HeadDamage);
                    HitMarkAt = Time.time;
                }
            }
            if (Clip == 0)
            {
                Pings++;
                demo.Play(DemoAudio.Ping, 0.9f); // 빈 클립이 튀어나오는 소리
                Reload();
            }
            return true;
        }

        if (!RocketLoaded) return false;
        RocketLoaded = false;
        Rockets--;
        RocketsFired++;
        nextFire = Time.time + 0.5f;
        demo.Play(DemoAudio.BazookaFire, 0.9f);
        kick = 2.5f;
        demo.Stage.Flash(muzzle, 30f, 20f, 0.12f);
        demo.Stage.Tracer(muzzle, end, new Color(1f, 0.6f, 0.3f), 0.18f, 0.25f);
        if (hitSomething && demo.Tank.Owns(hit.collider)) demo.Tank.RocketHit(hit.point, transform.position);
        else if (hitSomething) { demo.Stage.Flash(hit.point, 120f, 30f, 0.25f); demo.Play(DemoAudio.Boom, 0.6f); }
        if (Rockets > 0) reloadUntil = Time.time + wd.bazookaReloadSeconds;
        return true;
    }

    /// <summary>적 총알이 스쳤다. 무적이든 아니든 죽지는 않고, 화면 가장자리 연출만 나온다.</summary>
    public void Grazed()
    {
        Grazes++;
        demo.Play(DemoAudio.BulletPass, 0.8f, Random.Range(0.9f, 1.15f)); // 머리 옆을 스치는 총알
        Hurt = demo.Invincible ? 0.6f : 1f;
    }

    public void AddShake(float amount) => Shake = Mathf.Max(Shake, amount);

    /// <summary>A안: 적 총알에 맞았다. from 은 쏜 자리(맞은 방향 표시용).</summary>
    public void TakeHit(float damage, Vector3 from)
    {
        if (!demo.IsA || Dead || demo.Failed) return;
        lastHitAt = Time.time;
        var d = from - transform.position;
        HurtFromAngle = Vector3.SignedAngle(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, new Vector3(d.x, 0f, d.z), Vector3.up);
        Hurt = 1f;
        AddShake(0.35f);
        if (demo.Invincible) return;
        Health -= damage;
        if (Health > 0f) return;
        Health = 0f;
        demo.Fail("전사했습니다");
    }
}
