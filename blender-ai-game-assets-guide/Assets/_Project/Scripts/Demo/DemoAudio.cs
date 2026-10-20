using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 소리 파일 모음. Resources/Audio/ 의 WAV(기획서 7장 사운드 목록과 같은 이름)를 읽는다.
/// 지금 파일은 M01SoundMaker 로 합성한 소리이고, 녹음·라이브러리 소리가 들어오면 같은 이름으로 바꿔 넣으면 된다.
/// 파일이 없으면 코드로 만든 임시음으로 대신한다(타이밍 확인용).
/// </summary>
public static class DemoAudio
{
    const int Rate = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

    public static AudioClip Shot => Get("SFX_M1_Fire", TempShot);
    public static AudioClip EnemyShot => Get("SFX_Mosin_Fire", TempShot);
    public static AudioClip Ping => Get("SFX_M1_Ping", () => Make("Temp_Ping", 0.45f, (t, n) => (Mathf.Sin(t * 2f * Mathf.PI * 2900f) + 0.6f * Mathf.Sin(t * 2f * Mathf.PI * 4350f)) * Mathf.Exp(-t * 9f) * 0.4f));
    public static AudioClip Reload => Get("SFX_M1_Reload", TempClick);
    public static AudioClip Click => Get("SFX_Click", TempClick);
    public static AudioClip BulletPass => Get("SFX_Bullet_Pass", TempClick);
    public static AudioClip Boom => Get("SFX_Bazooka_Hit", TempBoom);
    public static AudioClip Cannon => Get("SFX_T34_Cannon", TempBoom);
    public static AudioClip BazookaFire => Get("SFX_Bazooka_Fire", TempBoom);
    public static AudioClip FlareLaunch => Get("SFX_Flare_Launch", TempBoom);
    public static AudioClip Ricochet => Get("SFX_T34_Ricochet", () => Make("Temp_Ricochet", 0.5f, (t, n) => Mathf.Sin(2f * Mathf.PI * (2400f * t - 1700f * t * t)) * Mathf.Exp(-t * 7f) * 0.5f + n * Mathf.Exp(-t * 60f) * 0.5f));
    public static AudioClip Footstep => Get("FOLEY_Footsteps_Dirt", TempClick);
    public static AudioClip Wounded => Get("SFX_Wounded", null);
    public static AudioClip Artillery => Get("AMB_Distant_Artillery", TempBoom);
    // 고리 소리
    public static AudioClip Night => Get("AMB_Night", null);
    public static AudioClip DawnWind => Get("AMB_Dawn_Wind", null);
    public static AudioClip TankEngine => Get("SFX_T34_Engine", null);
    public static AudioClip TruckEngine => Get("SFX_Truck_Engine", null);
    public static AudioClip FlareBurn => Get("SFX_Flare_Burn", null);
    public static AudioClip Breath => Get("SFX_Player_Breath", null);

    /// <summary>대사 녹음(지금은 TTS). Resources/Audio/VO/{id}</summary>
    public static AudioClip Voice(string id) => Get("VO/" + id, null);

    static AudioClip Get(string name, System.Func<AudioClip> fallback)
    {
        if (cache.TryGetValue(name, out var c) && c != null) return c;
        c = Resources.Load<AudioClip>("Audio/" + name);
        if (c == null && fallback != null) c = fallback();
        cache[name] = c;
        return c;
    }

    static AudioClip TempShot() => Make("Temp_Shot", 0.22f, (t, n) => n * Mathf.Exp(-t * 28f) + Mathf.Sin(t * 2f * Mathf.PI * 90f) * Mathf.Exp(-t * 18f) * 0.6f);
    static AudioClip TempBoom() => Make("Temp_Boom", 1.1f, (t, n) => (n * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 55f)) * Mathf.Exp(-t * 4.5f));
    static AudioClip TempClick() => Make("Temp_Click", 0.05f, (t, n) => n * Mathf.Exp(-t * 160f) * 0.6f);

    static AudioClip Make(string name, float seconds, System.Func<float, float, float> wave)
    {
        int count = Mathf.CeilToInt(seconds * Rate);
        var samples = new float[count];
        var rng = new System.Random(name.GetHashCode());
        for (int i = 0; i < count; i++)
        {
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            samples[i] = Mathf.Clamp(wave(i / (float)Rate, noise), -1f, 1f);
        }
        var clip = AudioClip.Create(name, count, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
