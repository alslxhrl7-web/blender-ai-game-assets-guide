using UnityEngine;

/// <summary>
/// 캡슐 대신 보이는 사람 모델. 캡슐은 그대로 두고(판정·이동은 캡슐이 한다) 그림만 감춘 뒤, 모델이 매 프레임 캡슐을 따라간다.
/// 아군: 1차 과제의 오석구 모델(Idle·Walk). 움직이면 Walk, 서 있으면 Idle. 쓰러진 캡슐이면 눕는다.
/// 적: 바르코3D 로 만든 북한군 소총수(Run·Cover·Fire·Death). DemoEnemy 의 상태를 보고 동작을 고른다.
///   이동·후퇴 = Run(숙여 뛰기), 엄폐 = Cover(무릎), 조준·사격 = Fire, 쓰러짐 = Death.
/// 프리팹이 없으면 캡슐이 그대로 보인다.
/// </summary>
public class DemoBody : MonoBehaviour
{
    public const string Ally = "Characters/Ally_Soldier", Enemy = "Characters/Enemy_Soldier";

    /// <summary>오른손 뼈(총을 붙이는 자리). 없으면 null.</summary>
    public Transform Hand { get; private set; }
    public Vector3 Facing => model != null ? model.forward : transform.forward;

    Transform model;
    Animator anim;
    Vector3 last, face = Vector3.forward;
    string playing = "";
    bool enemy, lying;
    M01Demo demo;
    DemoEnemy brain;

    /// <summary>캡슐(owner)에 사람 모델을 붙인다. 붙였으면 true.</summary>
    public static bool Attach(Transform owner, bool isEnemy, M01Demo d)
    {
        var prefab = Resources.Load<GameObject>(isEnemy ? Enemy : Ally);
        if (prefab == null) return false;
        var b = owner.gameObject.AddComponent<DemoBody>();
        b.demo = d;
        b.enemy = isEnemy;
        b.brain = owner.GetComponent<DemoEnemy>();
        b.model = Instantiate(prefab).transform;
        b.model.name = owner.name + "_Body";
        b.anim = b.model.GetComponent<Animator>();
        foreach (var t in b.model.GetComponentsInChildren<Transform>()) if (t.name.EndsWith("RightHand")) { b.Hand = t; break; }
        b.last = owner.position;
        b.face = isEnemy ? Vector3.back : Vector3.forward;
        var r = owner.GetComponent<Renderer>();
        if (r != null) r.enabled = false;
        return true;
    }

    void LateUpdate()
    {
        if (model == null) return;
        bool show = gameObject.activeInHierarchy;
        if (model.gameObject.activeSelf != show) model.gameObject.SetActive(show);
        if (!show) return;
        if (enemy) { TickEnemy(); return; }

        var p = transform.position;
        if (transform.up.y < 0.5f)
        {
            // 쓰러졌다: 캡슐처럼 눕고 멈춘다.
            if (!lying) { lying = true; if (anim != null) anim.enabled = false; }
            model.SetPositionAndRotation(p - transform.up * 0.85f + Vector3.down * 0.15f, transform.rotation);
            return;
        }
        float speed = Follow(p, p - Vector3.up * transform.localScale.y);
        Play(speed > 0.4f ? "Walk" : "Idle");
        if (anim != null) anim.speed = playing == "Walk" ? Mathf.Clamp(speed / 1.4f, 1f, 2.6f) : 1f;
    }

    void TickEnemy()
    {
        // 발은 늘 땅 위. 숙이거나 쓰러지는 것은 동작이 보여 준다.
        var p = transform.position;
        var feet = new Vector3(p.x, M01Demo.Ground(p.x, p.z), p.z);
        if (brain != null && brain.Down)
        {
            if (playing != "Death") { model.position = feet; Play("Death"); }
            return; // 쓰러진 자리와 방향 그대로
        }
        Follow(p, feet);
        var s = brain != null ? brain.state : DemoEnemy.State.Advance;
        Play(s == DemoEnemy.State.Cover ? "Cover" : s == DemoEnemy.State.Aim || (brain != null && brain.AtCover && !demo.IsA) ? "Fire" : "Run");
    }

    /// <summary>캡슐을 따라 자리와 방향을 맞춘다. 움직인 속도(m/s)를 돌려준다.</summary>
    float Follow(Vector3 p, Vector3 feet)
    {
        var move = p - last; move.y = 0f;
        float speed = Time.deltaTime > 0f ? move.magnitude / Time.deltaTime : 0f;
        last = p;
        if (move.sqrMagnitude > 0.000004f) face = move.normalized;
        else if (enemy && demo != null && demo.Player != null) { var to = demo.Player.transform.position - p; to.y = 0f; if (to.sqrMagnitude > 1f) face = to.normalized; }
        else if (!enemy) face = Vector3.forward;
        model.SetPositionAndRotation(feet, Quaternion.Slerp(model.rotation, Quaternion.LookRotation(face), 1f - Mathf.Exp(-10f * Time.deltaTime)));
        return speed;
    }

    void Play(string state)
    {
        if (anim == null || playing == state) return;
        playing = state;
        anim.CrossFade(state, 0.2f);
    }

    void OnDestroy()
    {
        if (model != null) Destroy(model.gameObject);
    }
}
