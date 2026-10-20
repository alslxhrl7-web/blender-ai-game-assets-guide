using UnityEngine;

/// <summary>
/// 관람형 화면(기획서 5장). 크로스헤어 점 하나만 남긴다. 남은 탄·상호작용 문구·목표 문구·히트마커·점수는 없다.
/// 맞았을 때 화면 가장자리 연출, 대사 자막(녹음 전 임시), 도입·새벽의 어두워짐, 미션 종료 화면을 그린다.
/// F11 로 화면 UI 를 모두 숨긴다(어두워짐과 미션 종료 화면은 연출이라 남는다). F1 은 연습용 표시.
/// </summary>
public class DemoHud : MonoBehaviour
{
    public int LinesShown { get; private set; }

    M01Demo demo;
    M01DemoData data;
    bool[] lineSeen;
    Texture2D edge;
    GUIStyle subtitle, caption, big, small;

    public void Init(M01Demo d)
    {
        demo = d;
        data = d.data;
        lineSeen = new bool[data.lines.Count];

        // 가장자리로 갈수록 짙어지는 테두리 그림(맞았을 때)
        const int n = 64;
        edge = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(n - 1, n - 1) * 0.5f) / (n * 0.5f);
                float a = Mathf.Clamp01((dist - 0.55f) / 0.6f);
                px[y * n + x] = new Color(0.55f, 0.02f, 0.02f, a * a);
            }
        edge.SetPixels32(px); edge.Apply();
    }

    /// <summary>화면을 덮는 검정의 짙기(0~1): 시작 자막, 새벽으로 넘어갈 때, 미션 종료 화면.</summary>
    public float Blackness()
    {
        float t = demo.Clock, a = 0f;
        var intro = data.intro;
        if (t < intro.captionSeconds) a = 1f;
        else if (t < intro.captionSeconds + 1.5f) a = 1f - (t - intro.captionSeconds) / 1.5f;

        var dawn = data.dawn;
        if (t >= dawn.fadeOutAt)
            a = t < dawn.blackUntil ? Mathf.Clamp01((t - dawn.fadeOutAt) / 1.2f) : 1f - Mathf.Clamp01((t - dawn.blackUntil) / Mathf.Max(0.01f, dawn.fadeInSeconds));

        float end = demo.BeatStart("END");
        if (t >= end) a = Mathf.Max(a, Mathf.Clamp01((t - end) / EndFade)); // A안: 새벽 장면이 2초에 걸쳐 검게 바뀐다
        if (demo.Waiting) a = 1f; // 시작 화면은 검은 배경
        return a;
    }

    void OnGUI()
    {
        if (demo == null || data == null) return;
        if (subtitle == null)
        {
            subtitle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            caption = new GUIStyle(subtitle) { fontSize = 30 };
            big = new GUIStyle(subtitle) { fontSize = 44 };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        }
        float t = demo.Clock, w = Screen.width, h = Screen.height;
        bool ui = !demo.UiHidden;

        // 맞았을 때 화면 가장자리 연출
        if (ui && !demo.IsA && demo.Player != null && demo.Player.Hurt > 0.01f) // A안은 맞은 방향 쪽만 붉어진다(DrawPlayHud)
        {
            GUI.color = new Color(1f, 1f, 1f, demo.Player.Hurt);
            GUI.DrawTexture(new Rect(0, 0, w, h), edge, ScaleMode.StretchToFill);
            GUI.color = Color.white;
        }

        // 크로스헤어: 점 하나
        if (ui && demo.Player != null && demo.Player.mode == DemoPlayer.Mode.Free)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(w * 0.5f - 2f, h * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // 어두워짐
        float black = Blackness();
        if (black > 0.001f)
        {
            GUI.color = new Color(0f, 0f, 0f, black);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // 대사·자막
        float y = h - 120f;
        for (int i = 0; i < data.lines.Count; i++)
        {
            var line = data.lines[i];
            if (t < line.time || t >= line.time + line.seconds) continue;
            if (!lineSeen[i]) { lineSeen[i] = true; LinesShown++; }
            if (line.caption)
            {
                GUI.Label(new Rect(0, h * 0.5f - 30f, w, 60f), line.text, caption);
                continue;
            }
            if (!ui || !data.showLinesAsSubtitles) continue;
            string text = string.IsNullOrEmpty(line.speaker) ? line.text : line.speaker + ": " + line.text;
            var box = new Rect(w * 0.15f, y, w * 0.7f, 44f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(box, text, subtitle);
            y -= 50f;
        }

        // 미션 종료 화면: 날짜·장소 → 전사자 이름 → 편지 한 줄 → 제목과 부제. 점수와 '미션 완료'는 없다.
        float end = demo.BeatStart("END");
        if (t >= end)
        {
            var es = data.endScreen;
            float s = t - end - (demo.IsA ? EndFade : 0f);
            if (s >= 0f)
            {
            if (s < es.datePlaceSeconds) GUI.Label(new Rect(0, h * 0.5f - 30f, w, 60f), es.datePlace, caption);
            else if (s < es.datePlaceSeconds + es.killedSeconds) GUI.Label(new Rect(0, h * 0.5f - 30f, w, 60f), es.killedInAction, caption);
            else if (s < es.datePlaceSeconds + es.killedSeconds + es.letterSeconds) GUI.Label(new Rect(0, h * 0.5f - 30f, w, 60f), es.letter, caption);
            else
            {
                GUI.Label(new Rect(0, h * 0.5f - 60f, w, 70f), es.title, big);
                GUI.Label(new Rect(0, h * 0.5f + 15f, w, 40f), es.subtitle, subtitle);
            }
            }
        }

        if (demo.IsA) DrawPlayHud(w, h, ui, t);

        // 연습용 표시(F1): 시각, 비트, 탄, 시연자 도구 상태
        if (ui && demo.ShowDebug && demo.Player != null)
        {
            var p = demo.Player;
            string weapon = p.Weapon == 1 ? $"M1 {p.Clip}/{data.weapon.m1ClipSize} 예비 {p.SpareClips}" : $"바주카 {(p.RocketLoaded ? "장전됨" : "빈 통")} 남은 로켓 {p.Rockets}";
            string text = $"{M01Demo.Stamp(t)} / {M01Demo.Stamp(data.totalSeconds)}   {(demo.Beat != null ? demo.Beat.title : "")}   {weapon}{(p.Reloading ? " (장전 중)" : "")}{(p.Prone ? "   엎드림" : "")}\n" +
                          $"무적 {(demo.Invincible ? "켜짐" : "꺼짐")}{(demo.Pilot.active ? "   자동 조작 켜짐" : "")}   F9 다음 비트  F10 무적  F11 UI 숨김  F12 처음부터  F1 이 표시  F8 자동 조작   (파랑: 아군, 빨강: 적·T-34, 임시 도형)";
            var content = new GUIContent(text);
            var size = small.CalcSize(content);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(6, 6, size.x + 12, size.y + 8), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(12, 10, size.x, size.y), content, small);
        }
    }

    // =====================================================================
    //  A안 화면 (기획서 5장 HUD)
    //  크로스헤어 점 하나. 남은 탄은 R 을 누를 때만 잠깐. 체력은 숫자 없이 화면 가장자리가 어두워진다(+ 거친 숨소리, DemoSound).
    //  맞은 방향은 그쪽 가장자리가 붉어진다. 탄약 상자 근처에서만 "F 탄약 보충". 목표 문구는 T-34 때 "측면 둔덕으로" 한 번뿐.
    //  넣지 않는 것: 히트마커, 킬 카운트, 점수, 미니맵. 미션 종료 화면에도 점수·통계·'미션 완료'가 없다.
    // =====================================================================
    float EndFade => demo.IsA ? 2f : 0.4f;
    GUIStyle hudC, hudSmall, title, button;
    Texture2D sideRed, dark;
    string eventSpeaker, eventText;
    float eventUntil;

    /// <summary>비트 시각이 아니라 일이 생겼을 때 나오는 대사(예: 탄약이 떨어졌을 때 박용팔).</summary>
    public void ShowEventLine(string speaker, string text, float seconds)
    {
        eventSpeaker = speaker; eventText = text; eventUntil = Time.time + seconds;
    }

    void DrawPlayHud(float w, float h, bool ui, float t)
    {
        if (hudC == null)
        {
            hudC = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, richText = true };
            hudSmall = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.LowerRight };
            title = new GUIStyle(hudC) { fontSize = 64 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 26 };
            sideRed = Gradient(new Color(0.75f, 0.03f, 0.02f));
            dark = Gradient(new Color(0f, 0f, 0f));
        }
        var p = demo.Player;

        // 시작 화면: 검은 배경, 타이틀, '시작' 버튼 하나
        if (demo.Waiting)
        {
            GUI.Label(new Rect(0, h * 0.5f - 130f, w, 80f), "다섯 개의 강", title);
            GUI.Label(new Rect(0, h * 0.5f - 50f, w, 40f), "The Forgotten War", hudC);
            if (GUI.Button(new Rect(w * 0.5f - 90f, h * 0.5f + 40f, 180f, 56f), "시작", button)) demo.BeginPlay();
            return;
        }
        if (p == null) return;
        bool play = ui && p.mode == DemoPlayer.Mode.Free && !demo.Failed;

        if (play)
        {
            // 체력: 숫자 없이 가장자리가 어두워진다
            float hurt = 1f - p.Health / DemoPlayer.MaxHealth;
            if (hurt > 0.02f) { GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(hurt * 1.3f)); GUI.DrawTexture(new Rect(0, 0, w, h), dark, ScaleMode.StretchToFill); GUI.color = Color.white; }

            // 맞은 방향: 그쪽 가장자리에 붉은 기운
            if (p.Hurt > 0.05f)
            {
                float a = p.HurtFromAngle;
                GUI.color = new Color(1f, 1f, 1f, p.Hurt);
                var c = new Vector2(w * 0.5f, h * 0.5f);
                GUIUtility.RotateAroundPivot(a, c);
                GUI.DrawTexture(new Rect(c.x - w * 0.35f, c.y - h * 0.5f - h * 0.05f, w * 0.7f, h * 0.28f), sideRed, ScaleMode.StretchToFill);
                GUI.matrix = Matrix4x4.identity;
                GUI.color = Color.white;
            }

            // 남은 탄: R 을 누르거나 장전·보충할 때만 잠깐
            if (Time.time - p.AmmoShownAt < 2f)
            {
                string ammo = p.Weapon == 1 ? $"M1 {p.Clip} / {data.weapon.m1ClipSize}    예비 클립 {p.SpareClips}" : $"로켓 {(p.RocketLoaded ? 1 : 0)} / 남은 {p.Rockets}";
                GUI.Label(new Rect(w - 420f, h - 70f, 400f, 50f), ammo, hudSmall);
            }

            // 상호작용
            if (p.NearAmmo) GUI.Label(new Rect(0, h * 0.5f + 70f, w, 30f), "F 탄약 보충", hudC);

            // 유일한 목표 문구: T-34 때 "측면 둔덕으로"
            float t34 = demo.BeatStart("T34");
            if (t >= t34 + 2f && t < t34 + 8f && !demo.Tank.Destroyed) GUI.Label(new Rect(0, h * 0.22f, w, 40f), "측면 둔덕으로", hudC);
        }

        // 조작 안내(시작 비트). 두 번째 시도부터는 생략한다.
        if (ui && !demo.GuideSkipped && demo.InBeat("START") && t > 5f && t < 18f)
        {
            GUI.Label(new Rect(0, h * 0.70f, w, 30f), "WASD 이동 · Z 엎드리기 · 우클릭 조준", hudC);
            GUI.Label(new Rect(0, h * 0.70f + 34f, w, 30f), "좌클릭 사격 · R 재장전", hudC);
        }

        // 일이 생겼을 때 나오는 대사
        if (ui && Time.time < eventUntil)
        {
            var box = new Rect(w * 0.15f, h - 170f, w * 0.7f, 44f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(box, Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(box, eventSpeaker + ": " + eventText, subtitle);
        }

        // 실패: 화면이 어두워진 뒤 저절로 다시 시작한다(점수·통계 없음)
        if (demo.Failed)
        {
            float k = Mathf.Clamp01((Time.unscaledTime - FailedSince()) / 1.5f);
            GUI.color = new Color(0f, 0f, 0f, k); GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.color = new Color(1f, 1f, 1f, k);
            GUI.Label(new Rect(0, h * 0.5f - 20f, w, 40f), demo.FailReason, hudC);
            GUI.color = Color.white;
        }
    }

    float failSeen = -1f;
    float FailedSince()
    {
        if (failSeen < 0f) failSeen = Time.unscaledTime;
        return failSeen;
    }

    /// <summary>가장자리로 갈수록 짙어지는 테두리 그림.</summary>
    static Texture2D Gradient(Color col)
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n - 1, n - 1) * 0.5f) / (n * 0.5f);
                float a = Mathf.Clamp01((d - 0.45f) / 0.6f);
                col.a = a * a; px[y * n + x] = col;
            }
        tex.SetPixels32(px); tex.Apply();
        return tex;
    }
}
