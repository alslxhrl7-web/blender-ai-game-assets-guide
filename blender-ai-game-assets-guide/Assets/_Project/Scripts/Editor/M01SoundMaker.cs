#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 효과음·환경음을 합성해 WAV 로 만든다(Resources/Audio/). 메뉴: Five Rivers > 효과음 만들기
/// 파일 이름은 기획서 7장 사운드 목록과 같다. 녹음·라이브러리 소리가 들어오면 같은 이름으로 덮어쓰면 된다.
/// 고리(loop)로 쓰는 소리(엔진·바람·조명탄 타는 소리)는 길이 안에서 주기가 딱 떨어지게 만들어 이음매가 들리지 않게 한다.
/// 대사(VO)는 Windows 음성 합성(TTS)으로 따로 만든다(Resources/Audio/VO/).
/// </summary>
public static class M01SoundMaker
{
    const int Rate = 44100;
    /// <summary>true 면 이미 있는 파일도 합성음으로 덮어쓴다. 기본은 녹음 파일을 지킨다.</summary>
    public static bool overwrite;
    const string Dir = "Assets/_Project/Resources/Audio/";
    static System.Random rng;

    [MenuItem("Five Rivers/효과음 만들기")]
    public static void Build()
    {
        Directory.CreateDirectory(Dir);
        rng = new System.Random(1950);

        // ---- 총 ----
        // 총성: 아주 짧은 충격음(수 ms) → 높은 "탕"(파열음) → 짧고 굵은 저음 → 바깥 공기에 퍼지는 꼬리 → 능선에서 돌아오는 메아리 두 번.
        Save("SFX_M1_Fire", Gunshot(1.9f, crack: 1.0f, crackK: 95f, body: 1.4f, bodyK: 16f, thumpHz: 105f, tail: 0.38f, release: 1.3f, echo1: 0.30f, echo2: 0.14f), 0.92f);
        // 적 소총은 멀리서 들린다: 높은 소리가 깎이고 꼬리와 메아리가 상대적으로 크다.
        Save("SFX_Mosin_Fire", Gunshot(2.0f, crack: 0.45f, crackK: 70f, body: 1.2f, bodyK: 11f, thumpHz: 85f, tail: 0.5f, release: 1.5f, echo1: 0.38f, echo2: 0.18f, far: true), 0.8f);
        Save("SFX_M1_Ping", Norm(Mix(0.9f,
            (t, i) => (Mathf.Sin(t * 6.2832f * 2860f) * Env(t, 7f) + 0.6f * Mathf.Sin(t * 6.2832f * 4190f) * Env(t, 10f) + 0.35f * Mathf.Sin(t * 6.2832f * 6350f) * Env(t, 16f)),
            (t, i) => Hp(i, 0) * Env(t, 400f) * 0.6f,
            (t, i) => Gate(t, 0.21f) * (Mathf.Sin(t * 6.2832f * 2875f) * Env(t - 0.21f, 12f) * 0.35f)), 0.8f));     // 땅에 한 번 튄다
        Save("SFX_M1_Reload", Norm(Mix(1.0f,
            (t, i) => Clack(t, 0.0f) + Clack(t, 0.32f) * 0.8f + Clack(t, 0.62f) * 1.2f), 0.8f));
        Save("SFX_Click", Norm(Mix(0.08f, (t, i) => Clack(t, 0f)), 0.7f));
        Save("SFX_Bullet_Pass", Norm(Mix(0.4f,
            (t, i) => Hp(i, 0) * Env(t, 120f) * 0.8f,
            (t, i) => Bp(i, 1, 0.25f, 0.05f) * Mathf.Exp(-Mathf.Pow((t - 0.08f) / 0.06f, 2f)) * 1.5f,
            (t, i) => Mathf.Sin(6.2832f * (1700f * t - 1500f * t * t)) * Env(t, 14f) * 0.15f), 0.7f));

        // ---- 조명탄 ----
        Save("SFX_Flare_Launch", Reverb(Mix(1.8f,
            (t, i) => Lp(i, 0, 0.1f) * Env(t, 25f) * 1.2f + Mathf.Sin(t * 6.2832f * 90f) * Env(t, 30f),
            (t, i) => Hp(i, 1) * Mathf.Clamp01(t * 3f) * Env(t - 0.2f, 1.6f) * 0.25f), 0.35f, 0.7f));
        Save("SFX_Flare_Burn", Loop(4f, (t, i) =>
            Lp(i, 0, 0.3f) * 0.18f + (rng.NextDouble() < 0.0009 ? 1f : 0f) * 0.9f + Hp(i, 1) * 0.05f), 0.5f);

        // ---- 전차·트럭 ----
        Save("SFX_T34_Engine", Loop(4f, (t, i) =>
        {
            float fire = 0.6f + 0.4f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 6.2832f * 25f), 4f);      // 디젤 폭발 박자
            float body = Mathf.Sin(t * 6.2832f * 32f) + 0.6f * Mathf.Sin(t * 6.2832f * 64f) + 0.3f * Mathf.Sin(t * 6.2832f * 96f + 1f);
            float clank = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 6.2832f * 9f), 30f) * Bp(i, 0, 0.3f, 0.05f) * 2f; // 궤도 덜컹
            return body * fire * 0.35f + Lp(i, 1, 0.02f) * 0.6f + clank;
        }), 0.8f);
        Save("SFX_Truck_Engine", Loop(4f, (t, i) =>
        {
            float body = Mathf.Sin(t * 6.2832f * 45f) + 0.5f * Mathf.Sin(t * 6.2832f * 90f) + 0.25f * Mathf.Sin(t * 6.2832f * 135f);
            float wob = 0.75f + 0.25f * Mathf.Sin(t * 6.2832f * 12f);
            return body * wob * 0.3f + Lp(i, 0, 0.03f) * 0.4f + Mathf.Sin(t * 6.2832f * 180f) * 0.04f;
        }), 0.7f);
        // 전차 주포·명중·바주카: 시작 소리(충격) → 묵직한 저음(몸통) → 흙·파편 → 계곡 울림. 기계적인 반복 울림(빗살 잔향)은 쓰지 않는다.
        Save("SFX_T34_Cannon", Blast(4.5f, crack: 1.0f, boomHz: 48f, boomLen: 0.5f, body: 1.6f, debris: 0.25f, tail: 0.55f, release: 2.4f), 1f);
        Save("SFX_Bazooka_Hit", Blast(3.2f, crack: 1.2f, boomHz: 62f, boomLen: 0.32f, body: 1.2f, debris: 0.45f, tail: 0.45f, release: 1.8f), 1f);
        Save("SFX_T34_Ricochet", Outdoor(Mix(1.0f,
            (t, i) => Hp(i, 0) * Env(t, 160f) * 0.9f,                                                         // 쇠에 맞는 순간
            (t, i) => Mathf.Sin(6.2832f * (2900f * t - 1300f * t * t)) * Env(t, 4.5f) * Mathf.Clamp01(t * 40f) * 0.35f, // 튕겨 나가는 탄
            (t, i) => (Mathf.Sin(t * 6.2832f * 520f) + 0.5f * Mathf.Sin(t * 6.2832f * 1370f) + 0.3f * Mathf.Sin(t * 6.2832f * 2210f)) * Env(t, 9f) * 0.3f), // 장갑판 울림
            tail: 0.25f, release: 1.2f, echo1: 0.12f, echo2: 0.05f), 0.85f);
        Save("SFX_Bazooka_Fire", Outdoor(Mix(1.8f,
            (t, i) => Hp(i, 0) * Env(t, 120f) * 0.8f,                                                         // 점화
            (t, i) => Lp(i, 1, 0.05f) * Env(t, 7f) * 2.6f,                                                     // 뒤로 터지는 후폭풍(낮고 굵다)
            (t, i) => Mathf.Sin(6.2832f * (95f * t - 25f * t * t)) * Env(t, 10f) * 0.9f,                       // 쿵
            (t, i) => Bp(i, 2, 0.5f, 0.12f) * Mathf.Clamp01(t * 60f) * Env(t, 2.4f) * Mathf.Clamp01((0.9f - t) * 3f) * 0.55f), // 로켓이 멀어지는 쉭
            tail: 0.4f, release: 1.6f, echo1: 0.22f, echo2: 0.1f), 0.95f);

        // ---- 사람·땅 ----
        Save("FOLEY_Footsteps_Dirt", Norm(Mix(0.28f,
            (t, i) => Bp(i, 0, 0.25f, 0.02f) * Env(t, 30f) * 1.2f,
            (t, i) => Hp(i, 1) * (rng.NextDouble() < 0.04 ? 1f : 0.1f) * Env(t, 18f) * 0.5f), 0.5f));
        Save("SFX_Wounded", Norm(Mix(2.6f, (t, i) =>
        {
            // 낮은 신음: 떨리는 목소리 바탕(톱니파)에 '으' 모음 쪽 울림을 걸고 숨소리를 더한다.
            float f0 = 118f - 22f * t / 2.6f + Mathf.Sin(t * 6.2832f * 5f) * 2f;
            phase += f0 / Rate; float saw = 2f * (phase - Mathf.Floor(phase)) - 1f;
            float voice = Bp1(saw, 0, 0.06f, 0.012f) * 2.5f + Bp1(saw, 1, 0.11f, 0.03f);
            float amp = Mathf.Clamp01(t * 3f) * Mathf.Clamp01((2.6f - t) * 1.5f) * (0.7f + 0.3f * Mathf.Sin(t * 6.2832f * 1.3f));
            return voice * amp + Lp(i, 4, 0.15f) * amp * 0.15f; // 숨소리(필터 칸이 목소리와 겹치지 않게 4번)
        }), 0.55f));

        // ---- 환경 ----
        Save("AMB_Night", Loop(8f, (t, i) =>
        {
            float gust = 0.5f + 0.3f * Mathf.Sin(t * 6.2832f * 0.125f) + 0.2f * Mathf.Sin(t * 6.2832f * 0.375f + 1f);
            float wind = Lp(i, 0, 0.012f) * 3f * gust;
            // 풀벌레: 4.6 kHz 짧은 울음을 초당 몇 번씩 무리 지어
            float chirpGate = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 6.2832f * 18f), 8f) * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 6.2832f * 0.75f), 3f);
            float cricket = Mathf.Sin(t * 6.2832f * 4600f) * chirpGate * 0.06f;
            float cricket2 = Mathf.Sin(t * 6.2832f * 5200f) * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 6.2832f * 22f + 2f), 8f) * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 6.2832f * 0.5f + 1f), 3f) * 0.04f;
            return wind + cricket + cricket2;
        }), 0.5f);
        Save("AMB_Dawn_Wind", Loop(8f, (t, i) =>
        {
            float gust = 0.55f + 0.45f * Mathf.Sin(t * 6.2832f * 0.125f);
            float bird = Mathf.Sin(6.2832f * (2600f * t + 900f * Mathf.Sin(t * 6.2832f * 6f) / 6.2832f)) * Gate(t, 5.1f) * Env(t - 5.1f, 9f) * 0.05f;
            return Lp(i, 0, 0.01f) * 3.2f * gust + bird;
        }), 0.5f);
        Save("AMB_Distant_Artillery", Norm(Mix(3.5f,
            (t, i) => Lp(i, 0, 0.015f) * Mathf.Clamp01(t * 30f) * Env(t, 1.3f) * 4f,
            (t, i) => Lp(i, 1, 0.01f) * Env(t - 0.9f, 1.5f) * Gate(t, 0.9f) * 2f), 0.6f));

        AssetDatabase.Refresh();
        Debug.Log("[Five Rivers] 효과음을 만들었습니다: " + Dir);
    }

    // =====================================================================
    //  합성 도구
    // =====================================================================
    static float phase;
    static float[] lp = new float[8], lp2 = new float[8];
    static float noise;
    static int lastI = -1;

    static float N(int i)
    {
        if (i != lastI) { lastI = i; noise = (float)(rng.NextDouble() * 2 - 1); }
        return noise;
    }
    static float Lp(int i, int k, float a) { lp[k] += a * (N(i) - lp[k]); return lp[k]; }                  // 낮은 소리만
    static float Hp(int i, int k) { lp[k + 4] += 0.2f * (N(i) - lp[k + 4]); return N(i) - lp[k + 4]; }        // 높은 소리만
    static float Bp(int i, int k, float hi, float lo) { lp[k] += hi * (N(i) - lp[k]); lp2[k] += lo * (lp[k] - lp2[k]); return lp[k] - lp2[k]; }
    static float Bp1(float x, int k, float hi, float lo) { lp[k + 2] += hi * (x - lp[k + 2]); lp2[k + 2] += lo * (lp[k + 2] - lp2[k + 2]); return lp[k + 2] - lp2[k + 2]; }
    static float Env(float t, float k) => t < 0f ? 0f : Mathf.Exp(-t * k);
    static float Gate(float t, float at) => t >= at ? 1f : 0f;
    static float Clack(float t, float at)
    {
        float s = t - at;
        if (s < 0f || s > 0.08f) return 0f;
        return ((float)(rng.NextDouble() * 2 - 1) * Mathf.Exp(-s * 120f) + Mathf.Sin(s * 6.2832f * 1900f) * Mathf.Exp(-s * 60f) * 0.6f + Mathf.Sin(s * 6.2832f * 3300f) * Mathf.Exp(-s * 90f) * 0.3f);
    }

    static float[] Mix(float seconds, params Func<float, int, float>[] parts)
    {
        Array.Clear(lp, 0, lp.Length); Array.Clear(lp2, 0, lp2.Length); phase = 0f;
        int n = Mathf.CeilToInt(seconds * Rate);
        var o = new float[n];
        for (int i = 0; i < n; i++) { float t = i / (float)Rate; foreach (var p in parts) o[i] += p(t, i); }
        return o;
    }

    /// <summary>고리 소리: 두 번 만들어 뒤쪽을 쓰고, 끝을 처음과 겹쳐 이음매를 없앤다.</summary>
    static float[] Loop(float seconds, Func<float, int, float> f, float peak = 0.8f)
    {
        var a = Mix(seconds * 2f, f);
        int n = Mathf.CeilToInt(seconds * Rate), fade = Rate / 4;
        var o = new float[n];
        for (int i = 0; i < n; i++) o[i] = a[n + i];
        for (int i = 0; i < fade; i++) { float k = i / (float)fade; o[i] = o[i] * k + a[n - fade + i] * (1f - k); }
        return Norm(o, peak);
    }

    /// <summary>
    /// 총성 한 발. 충격음(1~2 ms, 모든 높이) → 파열음(높은 "탕", crackK 로 빨리 사라짐) → 몸통(낮은 잡음) → 쿵(낮은 사인이 아래로 미끄러짐)
    /// → 바깥 공기 꼬리 → 능선 메아리 두 번(Outdoor). far 이면 멀리서 쏜 소리처럼 높은 소리를 깎는다.
    /// </summary>
    static float[] Gunshot(float seconds, float crack, float crackK, float body, float bodyK, float thumpHz, float tail, float release, float echo1, float echo2, bool far = false)
    {
        var dry = Mix(seconds,
            (t, i) => N(i) * Env(t, 900f) * 1.2f,                                                     // 충격
            (t, i) => Hp(i, 0) * Env(t, crackK) * crack,                                              // 파열음
            (t, i) => Lp(i, 1, far ? 0.06f : 0.14f) * Env(t, bodyK) * body,                           // 몸통
            (t, i) => Mathf.Sin(6.2832f * (thumpHz * t - thumpHz * 0.9f * t * t)) * Env(t, 26f) * 0.9f);  // 쿵
        for (int i = 0; i < dry.Length; i++) dry[i] = (float)Math.Tanh(dry[i] * 1.6f);            // 살짝 눌러 단단하게
        if (far) { float s = 0f; for (int i = 0; i < dry.Length; i++) { s += 0.35f * (dry[i] - s); dry[i] = s * 1.6f; } }
        return Outdoor(dry, tail, release, echo1, echo2);
    }

    /// <summary>폭발(주포·바주카 명중): 파열음 → 굵은 저음(주파수가 떨어진다) → 흙·파편 후두둑 → 긴 바깥 꼬리.</summary>
    static float[] Blast(float seconds, float crack, float boomHz, float boomLen, float body, float debris, float tail, float release)
    {
        var dry = Mix(seconds,
            (t, i) => N(i) * Env(t, 500f) * 1.3f + Hp(i, 0) * Env(t, 45f) * crack,
            (t, i) => Lp(i, 1, 0.04f) * Env(t, 2.8f) * body * 2.2f,
            (t, i) => Mathf.Sin(6.2832f * (boomHz * 1.6f * t - boomHz * 0.6f * t * t / boomLen)) * Mathf.Exp(-t / boomLen) * 1.1f,
            (t, i) => (rng.NextDouble() < 0.004 * Mathf.Exp(-t * 2f) ? (float)(rng.NextDouble() * 2 - 1) : 0f) * debris * Gate(t, 0.08f) * 3f);
        for (int i = 0; i < dry.Length; i++) dry[i] = (float)Math.Tanh(dry[i] * 1.4f);
        return Outdoor(dry, tail, release, 0.3f, 0.15f);
    }

    /// <summary>
    /// 바깥 소리 공간: 소리 크기를 따라가는 낮은 잡음 꼬리(release 초 동안 사라짐) + 능선에서 돌아오는 메아리 두 번(0.33 s, 0.78 s, 높은 소리가 깎임).
    /// 빗살 잔향처럼 "웅웅" 울리는 반복이 생기지 않는다.
    /// </summary>
    static float[] Outdoor(float[] dry, float tail, float release, float echo1, float echo2)
    {
        int extra = (int)(Rate * (release + 0.9f));
        var o = new float[dry.Length + extra];
        float env = 0f, rel = Mathf.Exp(-1f / (release * Rate / 3f)), lpN = 0f, lpN2 = 0f;
        for (int i = 0; i < o.Length; i++)
        {
            float x = i < dry.Length ? dry[i] : 0f;
            float a = Mathf.Abs(x);
            env = a > env ? env + 0.02f * (a - env) : env * rel;                                   // 크기 따라가기
            float n = (float)(rng.NextDouble() * 2 - 1);
            lpN += 0.08f * (n - lpN); lpN2 += 0.08f * (lpN - lpN2);                                // 낮은 공기 소리
            o[i] = x + lpN2 * env * tail * 6f;
        }
        Echo(o, dry, (int)(0.33f * Rate), echo1, 0.12f);
        Echo(o, dry, (int)(0.78f * Rate), echo2, 0.06f);
        int end = o.Length; while (end > 1 && Mathf.Abs(o[end - 1]) < 0.0004f) end--;
        Array.Resize(ref o, end);
        for (int i = 0; i < Rate / 6 && i < end; i++) o[end - 1 - i] *= i / (float)(Rate / 6);
        return o;
    }

    static void Echo(float[] o, float[] dry, int delay, float gain, float lpA)
    {
        float s1 = 0f, s2 = 0f;
        for (int i = 0; i < dry.Length && i + delay < o.Length; i++)
        {
            s1 += lpA * (dry[i] - s1); s2 += lpA * (s1 - s2);                                     // 먼 길을 돌아와 둔해진다
            o[i + delay] += s2 * gain * 2.2f;
        }
    }

    /// <summary>계곡 울림: 빗살 지연 4개 + 전대역 통과 2개(슈뢰더 잔향).</summary>
    static float[] Reverb(float[] dry, float wet, float peak)
    {
        int tail = Rate * 2;
        var o = new float[dry.Length + tail];
        int[] combs = { 1557, 1617, 1491, 1422 }; float[] fb = { 0.82f, 0.81f, 0.8f, 0.79f };
        var w = new float[o.Length];
        foreach (var (d, g) in new[] { (combs[0], fb[0]), (combs[1], fb[1]), (combs[2], fb[2]), (combs[3], fb[3]) })
        {
            var buf = new float[o.Length];
            for (int i = 0; i < o.Length; i++)
            {
                float x = i < dry.Length ? dry[i] : 0f;
                buf[i] = x + (i >= d ? buf[i - d] * g : 0f);
                w[i] += buf[i] * 0.25f;
            }
        }
        foreach (int d in new[] { 225, 556 })
        {
            var y = new float[o.Length];
            for (int i = 0; i < o.Length; i++) y[i] = -0.5f * w[i] + (i >= d ? w[i - d] + 0.5f * y[i - d] : 0f);
            w = y;
        }
        for (int i = 0; i < o.Length; i++) o[i] = (i < dry.Length ? dry[i] : 0f) + w[i] * wet;
        int end = o.Length; while (end > 1 && Mathf.Abs(o[end - 1]) < 0.0005f) end--;
        Array.Resize(ref o, end);
        for (int i = 0; i < Rate / 5 && i < end; i++) o[end - 1 - i] *= i / (float)(Rate / 5); // 끝을 부드럽게
        return Norm(o, peak);
    }

    static float[] Norm(float[] s, float peak)
    {
        float m = 0f; foreach (var v in s) m = Mathf.Max(m, Mathf.Abs(v));
        if (m > 0f) for (int i = 0; i < s.Length; i++) s[i] *= peak / m;
        return s;
    }

    static void Save(string name, float[] s, float peak = 0f)
    {
        if (File.Exists(Dir + name + ".wav") && !overwrite) return; // 녹음 소리(Freesound)가 있으면 그대로 둔다
        if (peak > 0f) Norm(s, peak); // 고리 소리는 여기서 크기를 맞춘다
        using (var f = new FileStream(Dir + name + ".wav", FileMode.Create))
        using (var w = new BinaryWriter(f))
        {
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + s.Length * 2); w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(s.Length * 2);
            foreach (var v in s) w.Write((short)(Mathf.Clamp(v, -1f, 1f) * 32767f));
        }
    }
}
#endif
