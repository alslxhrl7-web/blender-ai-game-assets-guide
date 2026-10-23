#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor.Media;
using UnityEngine;

/// <summary>
/// 플레이 영상 녹화(에디터 전용). 화면(UI 포함)과 소리를 30fps MP4 로 저장한다.
/// 정해진 구간만 담고, 구간 사이(정적·새벽)는 시계를 건너뛴다. M01PlayTools 가 붙인다.
/// 녹화 중에는 Time.captureFramerate 로 게임 시간을 1/30초씩 정확히 나아가게 한다(PC 가 느려도 영상은 제 속도).
/// </summary>
public class DemoRecorder : MonoBehaviour
{
    public string path;
    public int width = 1280, height = 640;
    /// <summary>담을 구간(시작·끝 초). 구간이 끝나면 다음 구간 시작으로 시계를 옮긴다.</summary>
    public List<Vector2> segments = new List<Vector2>();
    public bool Done { get; private set; }
    public int Frames { get; private set; }
    public bool HasAudio { get; private set; }

    M01Demo demo;
    MediaEncoder encoder;
    RenderTexture rt;
    Texture2D frame;
    NativeArray<float> audio;
    int seg;

    IEnumerator Start()
    {
        demo = FindAnyObjectByType<M01Demo>();
        const int fps = 30, rate = 48000;
        var video = new VideoTrackAttributes
        {
            frameRate = new MediaRational(fps), width = (uint)width, height = (uint)height,
            includeAlpha = false, bitRateMode = UnityEditor.VideoBitrateMode.High
        };
        var sound = new AudioTrackAttributes { sampleRate = new MediaRational(rate), channelCount = 2, language = "ko" };
        encoder = new MediaEncoder(path, video, sound);
        rt = new RenderTexture(width, height, 0);
        frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
        audio = new NativeArray<float>(rate / fps * 2, Allocator.Persistent);
        Time.captureFramerate = fps;
        HasAudio = AudioRenderer.Start();
        demo.JumpTo(segments[0].x);

        while (seg < segments.Count)
        {
            yield return new WaitForEndOfFrame();
            if (demo.Clock >= segments[seg].y || demo.Finished)
            {
                if (++seg >= segments.Count) break;
                demo.JumpTo(segments[seg].x);
                continue;
            }
            var shot = ScreenCapture.CaptureScreenshotAsTexture();
            Graphics.Blit(shot, rt);
            Destroy(shot);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            frame.Apply();
            RenderTexture.active = prev;
            encoder.AddFrame(frame);
            // 소리: 이 프레임 동안 나온 만큼(1/30초)
            if (HasAudio) AudioRenderer.Render(audio);
            encoder.AddSamples(audio);
            Frames++;
        }
        Finish();
    }

    void Finish()
    {
        if (Done) return;
        Done = true;
        if (HasAudio) AudioRenderer.Stop();
        Time.captureFramerate = 0;
        encoder?.Dispose();
        encoder = null;
        if (audio.IsCreated) audio.Dispose();
        Debug.Log($"[FiveRiver] 영상 저장: {path} ({Frames} 프레임, {Frames / 30f:F0}초, 소리 {(HasAudio ? "있음" : "없음")})");
    }

    void OnDestroy() => Finish();
}
#endif
