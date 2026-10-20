using UnityEngine;

/// <summary>
/// 시연자 동선 자동 재생(기획서 3장 "시연자 동선"을 그대로 따라 한다). F8 로 켜고 끈다.
/// 시연자가 연습할 때 참고로 보거나, 사람 없이 3분이 어떻게 흘러가는지 확인할 때 쓴다. 시연 당일에는 쓰지 않는다.
/// 사람 대신 시점·이동·사격·보충·무기 교체를 눌러 줄 뿐이고, 판정은 사람이 할 때와 같다.
/// </summary>
public class DemoAutoPilot : MonoBehaviour
{
    public bool active;

    M01Demo demo;
    DemoPlayer p;
    Vector3 spawn, firePost, leftPost, ammoSpot, entry, mound, mansu, seat;
    float nextShot;
    float blockedFor;
    DemoEnemy target, skip;
    bool frontFired;

    public void Init(M01Demo d)
    {
        demo = d;
        p = d.Player;
        spawn = d.Marker("SPAWN_PLAYER");
        firePost = spawn + new Vector3(0f, 0f, 2.6f);          // 북쪽 흙벽 바로 뒤, 가운데
        leftPost = spawn + new Vector3(-9f, 0f, 2.6f);          // 왼쪽 모래주머니
        ammoSpot = d.Marker(d.data.weapon.ammoBoxMarker) + new Vector3(0f, 0f, 1.2f);
        entry = d.Marker("A_EAST_ENTRY");
        mound = d.TryPath(d.data.t34.moundPath, out var path) ? path[path.Length - 1] : entry;
        mansu = d.Marker("PROP_MANSU");
        seat = d.Marker("SEAT_TAESEOK");
    }

    public void Tick(float dt)
    {
        p.auto = active;
        if (!active) return;
        p.autoHasMove = false;
        p.autoHasLook = false;
        p.autoSprint = false;
        p.autoLookSpeed = 120f;
        float t = demo.Clock;
        string beat = demo.Beat != null ? demo.Beat.id : "";
        float s = demo.Beat != null ? t - demo.Beat.start : 0f;

        switch (beat)
        {
            case "INTRO": // 주변을 천천히 둘러본다. 이동은 자동.
                Look(p.transform.position + Quaternion.Euler(0f, -30f + Mathf.Sin(t * 0.5f) * 55f, 0f) * new Vector3(0f, 2f, 30f), 30f);
                break;

            case "FIRST_FLARE":
                // 0:25 엎드려 흙벽 밑에 숨는다(엎드리면 흙벽 너머는 보이지 않는다). 조명탄이 뜨면 일어나 정찰병 2명을 각각 두 발로 쏜다.
                Move(firePost);
                p.SetProne(s < 3f);
                if (!p.Prone) Engage(); else Look(spawn + new Vector3(0f, 6f, 60f), 60f);
                break;

            case "ASSAULT":
                p.SetProne(false);
                if (s < 15f) { Move(firePost); Engage(); }                                          // 0:40~0:55 진입로 2의 3명
                else if (s < 20f) { Move(leftPost); Look(spawn + new Vector3(-20f, 6f, 50f), 60f); } // 0:55~ 어두울 때 왼쪽으로 옮긴다
                else if (s < 25f) { Move(firePost); if (s > 22f) Suppress(); }                      // 가운데로 돌아와 남은 탄을 다 쓴다
                else if (s < 35f)                                                                   // 1:05~1:15 탄약 상자에서 F 로 보충
                {
                    if (p.Resupplies == 0) { Move(ammoSpot); Look(ammoSpot + new Vector3(0f, 0.3f, -1.5f), 90f); p.Interact(); }
                    else { Move(firePost); Look(spawn + new Vector3(0f, 6f, 50f), 90f); }
                }
                else { Move(firePost); Engage(); }                                                  // 1:15~1:25 남은 4명
                break;

            case "SILENCE": // 시점을 천천히 왼쪽 참호 구석으로 돌려 담요를 덮은 형체를 본다
                Look(mansu + Vector3.up * 0.2f, 14f);
                break;

            case "T34":
                var tank = demo.Tank;
                var aim = tank.Appeared ? tank.AimPoint : demo.Marker("T34_START");
                bool sees = tank.Appeared && SeesTank(aim);
                bool atMound = Flat(p.transform.position - mound).magnitude <= 1.5f;
                if (s < 10f) { Look(aim, 40f); break; }                                             // 1:45~1:55 도로 쪽을 바라본다
                p.Switch(2);
                if (!frontFired && s < 22f)
                {
                    // 바주카로 바꿔 정면에 1발. 진지 가운데에서는 전차가 비탈에 가려 보이지 않아서,
                    // 2:00 부터는 보이는 자리가 나올 때까지 참호 동쪽 입구 쪽으로 나가며 쏜다.
                    if (s >= 15f && !sees) Move(entry, true);
                    Look(aim, 120f);
                    if (sees && p.AngleTo(aim) < 0.7f && p.TryFire()) frontFired = true;
                }
                else if (!atMound)                                                                  // 둔덕으로 달린다
                {
                    bool pastEntry = p.transform.position.x > entry.x - 1f;
                    var to = pastEntry ? mound : entry;
                    Move(to, true);
                    Look(to + Vector3.up * 1.6f, 120f);
                }
                else                                                                                // 2:10~2:25 측면에 2발
                {
                    Look(aim, 90f);
                    if (s >= 26f && !tank.Destroyed && sees && p.AngleTo(aim) < 0.7f) p.TryFire();
                }
                break;

            case "DAWN": // 시점만 움직여 옆에 앉는 태석을 본다
                Look(seat + Vector3.up * 0.7f, 20f);
                break;
        }
    }

    void Move(Vector3 to, bool sprint = false) { p.autoHasMove = true; p.autoMove = to; p.autoSprint = sprint; }
    void Look(Vector3 at, float speed) { p.autoHasLook = true; p.autoLook = at; p.autoLookSpeed = speed; }
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    bool SeesTank(Vector3 at)
    {
        var eye = p.EyePosition;
        return Physics.Raycast(eye, at - eye, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore) && demo.Tank.Owns(hit.collider);
    }

    bool Sees(DemoEnemy e, Vector3 at)
    {
        var eye = p.EyePosition;
        return Physics.Raycast(eye, at - eye, out var hit, 500f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<DemoEnemy>() == e;
    }

    // 보이는 적을 가까운 순서로 한 명씩, 몸통에 두 발. 나무나 엄폐물에 가려 있으면 머리를 노리고, 그래도 안 보이면 다른 적부터 쏜다.
    void Engage()
    {
        // 엄폐 지점에 멈춰 실루엣이 또렷한 적부터 쏜다. 멈춘 적이 없으면 뛰어오는 적을 쏜다.
        if (target == null || !target.Visible)
        {
            target = demo.Stage.NearestVisibleEnemy(p.transform.position, skip, true);
            if (target == null && demo.Beat != null && demo.Beat.end - demo.Clock < 6f) target = demo.Stage.NearestVisibleEnemy(p.transform.position, skip);
            blockedFor = 0f;
        }
        if (target == null) { skip = null; Look(spawn + new Vector3(0f, 6f, 55f), 60f); return; }
        var at = target.Chest;
        bool clear = Sees(target, at);
        if (!clear) { at = target.transform.position + Vector3.up * 0.7f; clear = Sees(target, at); }
        Look(at, 140f);
        if (!clear)
        {
            blockedFor += Time.deltaTime;
            if (blockedFor > 1f) { skip = target; target = null; }
            return;
        }
        blockedFor = 0f;
        if (p.AngleTo(at) > 0.4f || Time.time < nextShot) return;
        if (p.TryFire()) nextShot = Time.time + 0.55f;
    }

    // 비탈 위로 쏴서 남은 탄을 다 쓴다(1:05쯤 탄이 떨어지는 장면).
    void Suppress()
    {
        var at = demo.Marker("CP05") + new Vector3(0f, 5f, 0f);
        Look(at, 140f);
        if (p.Clip == 0 && p.SpareClips == 0) return;
        if (p.AngleTo(at) > 2f || Time.time < nextShot) return;
        if (p.TryFire()) nextShot = Time.time + 0.4f;
    }
}
