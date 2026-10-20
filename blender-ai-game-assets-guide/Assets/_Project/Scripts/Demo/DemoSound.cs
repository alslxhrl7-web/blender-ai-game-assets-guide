using UnityEngine;

/// <summary>
/// 시각에 맞춰 나는 소리(기획서 7장 사운드 목록). 효과음 한 번씩은 각 스크립트가 demo.Play 로 내고, 여기서는 이어지는 소리를 맡는다.
/// - 환경음: 밤 바람·풀벌레(새벽 전까지) → 새벽 바람(미션 종료 화면까지), 먼 포성(전차 등장 전까지 몇 초마다)
/// - 트럭 엔진(도입, 트럭 자리에서 들림), T-34 엔진·궤도(전차 자리에서 들림, 정적 구간 끝에 멀리서 커진다)
/// - 정적 구간의 부상병 신음(최만수 자리, 박용팔이 부르기 전까지)
/// - 플레이어 발소리(흙길). 대사는 자막으로만 보인다(녹음 전).
/// </summary>
public class DemoSound : MonoBehaviour
{
    M01Demo demo;
    M01DemoData data;
    AudioSource night, dawn, tankEngine, distantEngine, truckEngine, moan, voice, breath;
    float nextArtillery = 3f, stepLeft = 0.7f;
    Vector3 lastFeet;
    int nextLine;

    public void Init(M01Demo d)
    {
        demo = d;
        data = d.data;
        night = Loop(gameObject, DemoAudio.Night, 0f, false);
        dawn = Loop(gameObject, DemoAudio.DawnWind, 0f, false);
        distantEngine = Loop(gameObject, DemoAudio.TankEngine, 0f, false);
        if (d.Tank.Root != null) tankEngine = Loop(d.Tank.Root.gameObject, DemoAudio.TankEngine, 0f, true);
        if (d.Stage.Truck != null) truckEngine = Loop(d.Stage.Truck.gameObject, DemoAudio.TruckEngine, 0f, true);
        var m = new GameObject("Mansu_Voice");
        m.transform.SetParent(transform, false);
        m.transform.position = d.Marker("PROP_MANSU") + Vector3.up * 0.3f;
        moan = Source(m, true);
        moan.clip = DemoAudio.Wounded;
        moan.minDistance = 3f;
        voice = Source(gameObject, false);
        breath = Loop(gameObject, DemoAudio.Breath, 0f, false); // 체력이 낮을 때 거친 숨소리(A안 HUD: 체력은 숫자 대신 소리와 어두움)
        lastFeet = d.Player.transform.position;
        // 체크포인트에서 시작하면 지나간 대사는 건너뛴다.
        while (nextLine < data.lines.Count && data.lines[nextLine].time < d.Clock - 0.5f) nextLine++;
    }

    static AudioSource Source(GameObject g, bool spatial)
    {
        var s = g.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = spatial ? 1f : 0f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 10f;
        s.maxDistance = 400f;
        s.dopplerLevel = 0f;
        return s;
    }

    static AudioSource Loop(GameObject g, AudioClip clip, float volume, bool spatial)
    {
        var s = Source(g, spatial);
        s.clip = clip;
        s.loop = true;
        s.volume = volume;
        if (clip != null) s.Play();
        return s;
    }

    public void Tick(float dt)
    {
        float t = demo.Clock;
        float dawnAt = data.dawn.fadeOutAt;
        bool ended = demo.Failed;

        // 환경음: 새벽 어두워지는 1.5초 동안 밤에서 새벽 바람으로 바꾼다.
        float k = Mathf.InverseLerp(dawnAt, dawnAt + 1.5f, t);
        night.volume = Mathf.Lerp(0.45f, 0f, k) * (ended ? 0.3f : 1f);
        dawn.volume = Mathf.Lerp(0f, 0.55f, k);

        // 먼 포성: 전차가 나오기 전까지
        if (!ended && t < data.t34.appearAt && t >= nextArtillery)
        {
            nextArtillery = t + Random.Range(7f, 14f);
            demo.Play(DemoAudio.Artillery, Random.Range(0.25f, 0.45f), Random.Range(0.85f, 1.1f));
        }

        // 트럭: 도입에서 멈추고 사람들이 내린 뒤 꺼진다.
        if (truckEngine != null)
            truckEngine.volume = demo.IsA ? 0f : t < data.intro.truckStopAt ? 1f : Mathf.MoveTowards(truckEngine.volume, t < data.intro.walkFrom + 2f ? 0.5f : 0f, dt * 0.5f); // A안: 트럭은 서 있기만 한다

        // 전차: 정적 끝 무렵 멀리서 엔진 소리가 커지고(1:30~1:45), 나오면 전차 자리에서 난다. 멈추면 꺼진다.
        float far = t >= 90f && t < data.t34.appearAt ? Mathf.InverseLerp(90f, data.t34.appearAt, t) * 0.5f : 0f;
        distantEngine.volume = Mathf.MoveTowards(distantEngine.volume, far, dt);
        if (tankEngine != null)
        {
            bool running = demo.Tank.Appeared && !demo.Tank.Destroyed && !ended;
            tankEngine.volume = Mathf.MoveTowards(tankEngine.volume, running ? 1f : 0f, dt * (running ? 1f : 0.7f));
        }

        // 부상병 신음: 정적 구간이 시작되면 담요 아래에서 두어 번. 박용팔이 부르는 순간부터는 대답이 없다.
        float silence = demo.BeatStart("SILENCE"), call = CallTime();
        if (moan.clip != null && !moan.isPlaying && t >= silence && t < call - 2.6f && !ended)
        {
            moan.pitch = Random.Range(0.92f, 1.05f);
            moan.volume = 0.9f;
            moan.Play();
        }

        // 체력이 50 아래로 떨어지면 숨이 거칠어진다.
        var pl = demo.Player;
        float want = demo.IsA && pl != null && !ended ? Mathf.Clamp01((60f - pl.Health) / 50f) * 0.8f : 0f;
        breath.volume = Mathf.MoveTowards(breath.volume, want, dt * 0.8f);

        Footsteps(dt);
        // 대사는 자막으로만 보인다(기획 담당 결정 2026-10-20). 녹음이 생기면 Voices(t) 를 다시 켠다.
    }

    float CallTime()
    {
        foreach (var l in data.lines) if (l.id == "VO_Park_003") return l.time;
        return float.MaxValue;
    }

    void Footsteps(float dt)
    {
        var p = demo.Player;
        var feet = p.transform.position;
        var move = feet - lastFeet; move.y = 0f;
        lastFeet = feet;
        if (p.mode != DemoPlayer.Mode.Free || move.magnitude > 2f) return; // 자리 옮김(도입·새벽)은 발소리가 아니다
        stepLeft -= move.magnitude;
        if (stepLeft > 0f) return;
        stepLeft = p.Prone ? 0.5f : 1.5f;
        demo.Play(DemoAudio.Footstep, p.Prone ? 0.25f : 0.45f, Random.Range(0.85f, 1.15f));
    }

    /// <summary>
    /// 대사: 자막 시각이 되면 줄을 세우고, 앞 대사가 끝난 뒤에 다음 대사를 튼다(겹치지 않게).
    /// 6초 넘게 밀린 대사는 건너뛴다(장면이 이미 넘어갔으므로). 높낮이는 녹음 파일에 이미 들어 있어 재생 속도는 바꾸지 않는다.
    /// </summary>
    readonly System.Collections.Generic.Queue<(AudioClip clip, float at)> pending = new System.Collections.Generic.Queue<(AudioClip, float)>();

    void Voices(float t)
    {
        while (nextLine < data.lines.Count && data.lines[nextLine].time <= t)
        {
            var line = data.lines[nextLine++];
            if (t - line.time > 1f) continue; // 건너뛴 대사
            var clip = DemoAudio.Voice(line.id);
            if (clip != null) pending.Enqueue((clip, line.time));
        }
        if (voice.isPlaying || pending.Count == 0) return;
        var next = pending.Dequeue();
        if (t - next.at > 6f) return;
        voice.pitch = 1f;
        voice.clip = next.clip;
        voice.Play();
    }
}
