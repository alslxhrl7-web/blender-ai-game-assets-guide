using UnityEngine;

/// <summary>
/// 북한군 소총수(기획서 4장 EnemyData).
/// B안(관람형): 스크립트 적. 능선에서 나타남 → 정해진 엄폐 지점까지 이동 → 그 자리에서 빗나가게 사격 반복 → 맞으면 쓰러짐.
/// A안(직접 플레이): 같은 길로 내려오지만, 엄폐 지점에서는 상태 머신으로 스스로 판단한다.
///   이동 → 엄폐(몸을 숙여 숨는다) ⇄ 조준·사격(일어나 쏜다) → 비트가 끝나면 후퇴.
///   플레이어를 알아채는 조건: 시야가 트여 있고, 조명탄이 떠 있거나 · 가깝거나(25m) · 방금 총을 쐈을 때.
///   엎드린 플레이어는 멀리서 잘 보이지 않는다. 명중률은 거리·조명탄·엎드림에 따라 달라진다.
/// 비트가 끝나면 남은 적은 무대(DemoStage)가 쓰러뜨리거나(B안) 후퇴시킨다(A안).
/// </summary>
public class DemoEnemy : MonoBehaviour
{
    public enum State { Hidden, Advance, Cover, Aim, Charge, Retreat, Gone }

    public string beatId;
    public State state { get; private set; } = State.Hidden;
    public bool Down { get; private set; }
    /// <summary>물러나는 중이거나 사라진 적. 공세가 끝났는지 셀 때 쓰러진 적과 같이 본다.</summary>
    public bool Leaving => state == State.Retreat || state == State.Gone;
    public bool KilledByPlayer { get; private set; }
    public bool AtCover { get; private set; }
    /// <summary>A안에서 엄폐물 뒤에 숙인 채다. 이때는 몸이 가려진다.</summary>
    public bool Crouched { get; private set; }
    public bool Visible => gameObject.activeSelf && !Down && !Crouched;
    public Vector3 Chest => transform.position + Vector3.up * 0.15f;
    public int ShotsAtPlayer { get; private set; }
    public int HitsOnPlayer { get; private set; }

    // A안 수치는 M01PlanA(기획서 4장 EnemyData)에 있다. 총소리를 들으면 이 거리 안에서는 어두워도 알아챈다.
    const float HearSeconds = 1.5f, HearRange = 45f;

    M01Demo demo;
    M01DemoData.EnemyData ed;
    Vector3[] route;
    int next = 1, burst;
    float health, delay, nextShot, yaw, stateUntil;

    public void Init(M01Demo d, Vector3[] waypoints, float startDelay, string beat)
    {
        demo = d;
        ed = d.data.enemy;
        route = waypoints;
        delay = startDelay;
        beatId = beat;
        health = ed.health;
        yaw = Random.Range(0f, 360f);
        Stand(route[0]);
        gameObject.SetActive(false);
    }

    public void Tick(float dt)
    {
        if (Down) return;
        if (delay > 0f)
        {
            if (demo.IsA && demo.Stage.ActiveEnemies >= M01PlanA.MaxActive) return; // A안: 동시에 활동하는 적은 최대 6명
            delay -= dt;
            if (delay > 0f) return;
            gameObject.SetActive(true);
            state = State.Advance;
        }
        if (state == State.Retreat) { Retreat(dt); return; }
        if (!AtCover)
        {
            var p = transform.position;
            var to = route[next] - p;
            to.y = 0f;
            float step = ed.moveSpeed * dt;
            if (to.magnitude <= step)
            {
                Stand(route[next]);
                if (++next >= route.Length)
                {
                    AtCover = true;
                    nextShot = Time.time + Random.Range(0.5f, 1.5f);
                    if (demo.IsA) Enter(State.Cover, Random.Range(0.8f, 2f));
                }
            }
            else Stand(p + to.normalized * step);
            return;
        }
        if (demo.IsA) Think();
        else ScriptedFire();
    }

    // =====================================================================
    //  B안: 정해진 자리에서 빗나가게 쏜다(위협감만 준다)
    // =====================================================================
    void ScriptedFire()
    {
        if (Time.time < nextShot || demo.Player == null) return;
        nextShot = Time.time + Random.Range(ed.fireIntervalMin, ed.fireIntervalMax);
        var miss = Random.onUnitSphere * ed.missRadius;
        miss.y = Mathf.Abs(miss.y) * 0.5f;
        MuzzleFx(demo.Player.EyePosition + miss);
        if (Random.value < ed.grazeChance) demo.Player.Grazed();
    }

    // =====================================================================
    //  A안: 상태 머신 (기획서 4장 EnemyData)
    //  엄폐(3~5초) → 일어나 살핌 → 알아챘으면 0.8초 뒤 한 발 → 다시 엄폐. "쏘고 숨고를 반복"
    //  플레이어와 15m 이내거나 남은 적이 2명 이하면 돌격(엄폐 없이 다가오며 2~3초마다 쏜다).
    //  알아채는 거리: 어두울 때 20m, 조명탄 아래 60m(+ 방금 총소리가 났으면 45m). 시야가 트여 있어야 한다.
    //  명중률: 어두울 때 15%, 밝을 때 35%. 한 발 피해 30(네 발이면 사망).
    // =====================================================================
    void Enter(State s, float seconds)
    {
        state = s;
        stateUntil = Time.time + seconds;
        Crouch(s == State.Cover);
        if (s == State.Aim) nextShot = Time.time + M01PlanA.Reaction;           // 처음 보고 쏘기까지 0.8초
        if (s == State.Charge) nextShot = Time.time + Random.Range(M01PlanA.FireMin, M01PlanA.FireMax);
    }

    void Think()
    {
        var player = demo.Player;
        if (player == null || demo.Failed) return;
        float dist = Vector3.Distance(transform.position, player.transform.position);
        if (state != State.Charge && (dist < M01PlanA.ChargeRange || demo.Stage.AliveEnemies <= M01PlanA.ChargeWhenLeft))
            Enter(State.Charge, 0f); // 끝에 몰리는 압박
        switch (state)
        {
            case State.Cover:
                if (Time.time < stateUntil) return;
                if (Aware(player)) Enter(State.Aim, 3f);
                else Enter(State.Cover, Random.Range(1f, 2f)); // 살짝 고개만 들었다가 다시 숨는다
                break;

            case State.Aim:
                Face(player.transform.position);
                if (!Aware(player)) { Enter(State.Cover, Random.Range(1f, 2f)); return; }
                if (Time.time < nextShot) return;
                FireAt(player);
                Enter(State.Cover, Random.Range(M01PlanA.CoverMin, M01PlanA.CoverMax)); // 쏘고 숨는다
                break;

            case State.Charge:
                // 돌격: 참호 앞 흉벽 바깥(참호 북쪽 선)까지만 다가와 거기서 쏜다. 참호 안으로 뛰어들지 않는다.
                Face(player.transform.position);
                {
                    var p = transform.position;
                    var to = ChargeGoal(player.transform.position) - p; to.y = 0f;
                    float step = ed.moveSpeed * 0.7f * Time.deltaTime;
                    if (to.magnitude > 0.3f) Stand(p + to.normalized * Mathf.Min(step, to.magnitude));
                }
                if (Time.time >= nextShot)
                {
                    // 보이면 조준 사격, 흉벽에 가려 안 보이면 머리 위로 제압 사격(총소리·예광선으로 압박만 준다)
                    if (Aware(player)) FireAt(player);
                    else if (dist < 30f) SuppressFire(player);
                    nextShot = Time.time + Random.Range(M01PlanA.FireMin, M01PlanA.FireMax);
                }
                break;
        }
    }

    /// <summary>플레이어를 알아챘는지: 거리(어두울 때 20m, 조명탄 아래 60m, 총소리 45m) 안이고 시야가 트였다.</summary>
    bool Aware(DemoPlayer player)
    {
        var head = transform.position + Vector3.up * (Crouched ? 0.6f : 0.75f); // 서 있으면 눈높이 약 1.6 m(흉벽 너머를 본다)
        var eye = player.EyePosition;
        float dist = Vector3.Distance(head, eye);
        bool lit = demo.Stage.FlareLevel > 0.3f;
        bool heard = Time.time - player.LastShotAt < HearSeconds && dist < HearRange;
        if (dist > (lit ? M01PlanA.DetectLit : M01PlanA.DetectDark) && !heard) return false;
        return LineOfSight(head, eye, player);
    }

    bool LineOfSight(Vector3 from, Vector3 to, DemoPlayer player)
    {
        var dir = to - from;
        float len = dir.magnitude;
        from += dir / len * 0.4f; // 자기 몸을 피해서 쏜다
        if (!Physics.Raycast(from, dir, out var hit, len, ~0, QueryTriggerInteraction.Ignore)) return true;
        return hit.collider.transform.IsChildOf(player.transform);
    }

    void FireAt(DemoPlayer player)
    {
        ShotsAtPlayer++;
        bool lit = demo.Stage.FlareLevel > 0.3f;
        bool hit = Random.value < (lit ? M01PlanA.HitLit : M01PlanA.HitDark);
        var aim = player.EyePosition + (hit ? Vector3.down * 0.4f : Random.onUnitSphere * ed.missRadius);
        MuzzleFx(aim);
        if (hit) { HitsOnPlayer++; player.TakeHit(M01PlanA.EnemyRifleDamage, transform.position); }
        else if (Random.value < ed.grazeChance) player.Grazed();
    }

    /// <summary>
    /// 돌격 목표: 플레이어에게서 8 m 떨어진 자리. 참호(x -21~21) 앞이면 흉벽 바깥(z 6.5 m 이상)에서 멈춘다.
    /// 예전에는 6 m 까지 곧장 다가와, 흉벽에 가려 쏘지도 못한 채 참호 안으로 내려왔다.
    /// </summary>
    Vector3 ChargeGoal(Vector3 playerPos)
    {
        var away = transform.position - playerPos; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
        var goal = playerPos + away.normalized * 8f;
        if (Mathf.Abs(goal.x) <= 21.5f && goal.z < 6.5f) goal.z = 6.5f;
        // 이미 그보다 멀리 있으면 그 자리를 넘어가지 않는다(뒤로 물러나지 않음)
        var cur = transform.position; cur.y = 0f; var pp = playerPos; pp.y = 0f;
        if ((cur - pp).magnitude < (goal - pp).magnitude) return cur;
        return goal;
    }

    /// <summary>흉벽에 가려 플레이어가 안 보일 때: 머리 위로 빗나가는 제압 사격(피해 없음, 가끔 스치는 소리).</summary>
    void SuppressFire(DemoPlayer player)
    {
        ShotsAtPlayer++;
        MuzzleFx(player.EyePosition + Vector3.up * Random.Range(0.4f, 1.2f) + Random.insideUnitSphere * 0.8f);
        if (Random.value < ed.grazeChance) player.Grazed();
    }

    void MuzzleFx(Vector3 target)
    {
        var from = Chest + Vector3.up * 0.3f;
        demo.Stage.Flash(from, 6f, 9f, 0.06f);
        demo.Stage.Tracer(from, target, new Color(0.8f, 1f, 0.7f), 0.05f, 0.07f);
        demo.Play(DemoAudio.EnemyShot, 0.5f, Random.Range(0.92f, 1.08f));
    }

    /// <summary>A안: 비트가 끝나면 능선 너머로 물러난다(쓰러뜨린 수에 넣지 않는다).</summary>
    public void BeginRetreat()
    {
        if (Down) return;
        if (!gameObject.activeSelf) { Down = true; state = State.Gone; return; }
        Crouch(false);
        state = State.Retreat;
    }

    void Retreat(float dt)
    {
        var p = transform.position;
        var to = route[0] - p;
        to.y = 0f;
        float step = ed.moveSpeed * dt;
        if (to.magnitude <= step) { Down = true; state = State.Gone; gameObject.SetActive(false); return; }
        Stand(p + to.normalized * step);
        Face(route[0]);
    }

    void Crouch(bool on)
    {
        if (Crouched == on) return;
        Crouched = on;
        var p = transform.position;
        transform.position = new Vector3(p.x, M01Demo.Ground(p.x, p.z) + (on ? 0.35f : 0.85f), p.z);
    }

    void Face(Vector3 target)
    {
        var d = target - transform.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
    }

    void Stand(Vector3 xz)
    {
        transform.SetPositionAndRotation(new Vector3(xz.x, M01Demo.Ground(xz.x, xz.z) + 0.85f, xz.z), transform.rotation);
    }

    /// <summary>M1 에 맞았다. 캡슐 윗부분(머리)이면 머리 피해. 쓰러뜨렸으면 true.</summary>
    public bool Hit(Vector3 point, float bodyDamage, float headDamage)
    {
        if (Down) return false;
        bool head = point.y > transform.position.y + 0.55f;
        health -= head ? headDamage : bodyDamage;
        if (health > 0f)
        {
            if (demo.IsA && AtCover) Enter(State.Cover, Random.Range(1.5f, 2.5f)); // 맞으면 일단 숨는다
            return false;
        }
        KilledByPlayer = true;
        demo.AddKill(head);
        Fall();
        return true;
    }

    /// <summary>쓰러진다.</summary>
    public void Fall()
    {
        if (Down) return;
        Down = true;
        state = State.Gone;
        if (!gameObject.activeSelf) return; // 아직 능선 뒤에서 나오지 않았으면 그대로 숨겨 둔다
        var p = transform.position;
        transform.SetPositionAndRotation(new Vector3(p.x, M01Demo.Ground(p.x, p.z) + 0.25f, p.z), Quaternion.Euler(90f, yaw, 0f));
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;
    }
}
