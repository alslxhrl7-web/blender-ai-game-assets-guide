#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// 열려 있는 M01_Dabudong 씬을 위에서 내려다본 그림(탑뷰)으로 저장한다. 레벨 논의 자료용.
/// 메뉴: Five Rivers > M1 탑뷰 캡처
/// 씬을 저장하거나 바꾸지 않는다. 캡처하는 동안만 안개를 끄고 마커·경로 표시를 켰다가 원래대로 돌린다.
/// 결과: Shots/TopView/M01_top_clean.png, M01_top_markers.png, M01_markers.csv (북쪽 +Z 가 위)
/// </summary>
public static class M01TopViewCapture
{
    const string SceneName = "M01_Dabudong";
    const string OutDir = "Shots/TopView";
    const string RequestFile = "Temp/m01_topview.request";

    // 지형 범위 x -60..75, z -60..120 의 가운데 위에서, 세로 190m 를 담는다.
    static readonly Vector3 CamPos = new Vector3(7.5f, 150f, 30f);
    const float HalfHeight = 95f;
    const int PxW = 1500, PxH = 2000;

    [MenuItem("Five Rivers/M1 탑뷰 캡처")]
    public static void Capture()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Debug.LogWarning("[Five Rivers] 탑뷰 캡처는 " + SceneName + " 씬을 연 상태에서 실행하세요. 지금 씬: " + scene.name);
            return;
        }
        Directory.CreateDirectory(OutDir);

        var go = new GameObject("cap_top") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        var ad = go.AddComponent<UniversalAdditionalCameraData>();
        ad.renderPostProcessing = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        cam.orthographicSize = HalfHeight;
        cam.nearClipPlane = 1f; cam.farClipPlane = 400f;
        cam.transform.position = CamPos;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.aspect = PxW / (float)PxH;

        var markers = Object.FindObjectsByType<GreyboxMarker>();
        var shown = new List<Renderer>();
        bool fog = RenderSettings.fog;
        try
        {
            RenderSettings.fog = false;
            Shoot(cam, "M01_top_clean.png");

            foreach (var m in markers)
                foreach (var r in m.GetComponentsInChildren<MeshRenderer>())
                    if (!r.enabled) { r.enabled = true; shown.Add(r); }
            foreach (var l in Object.FindObjectsByType<LineRenderer>())
                if (!l.enabled) { l.enabled = true; shown.Add(l); }
            Shoot(cam, "M01_top_markers.png");

            var sb = new StringBuilder("id,x,y,z,px,py\n");
            foreach (var m in markers)
            {
                Vector3 p = m.transform.position, v = cam.WorldToViewportPoint(p);
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:F2},{2:F2},{3:F2},{4:F0},{5:F0}\n",
                    m.id, p.x, p.y, p.z, v.x * PxW, (1f - v.y) * PxH);
            }
            File.WriteAllText(Path.Combine(OutDir, "M01_markers.csv"), sb.ToString(), new UTF8Encoding(false));
        }
        finally
        {
            foreach (var r in shown) r.enabled = false;
            RenderSettings.fog = fog;
            Object.DestroyImmediate(go);
        }
        Debug.Log("[Five Rivers] 탑뷰 저장: " + OutDir + " (마커 " + markers.Length + "개)");
    }

    static void Shoot(Camera cam, string file)
    {
        var rt = new RenderTexture(PxW, PxH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        for (int i = 0; i < 2; i++) cam.Render();
        RenderTexture.active = rt;
        var t2 = new Texture2D(PxW, PxH, TextureFormat.RGB24, false);
        t2.ReadPixels(new Rect(0, 0, PxW, PxH), 0, 0); t2.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, file), t2.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(t2); Object.DestroyImmediate(rt);
    }

    // Temp 폴더에 요청 파일이 있으면 스크립트를 다시 읽은 직후 한 번 캡처한다(메뉴를 누르지 않고 돌리기 위한 것).
    [InitializeOnLoadMethod]
    static void RunIfRequested()
    {
        if (!File.Exists(RequestFile)) return;
        File.Delete(RequestFile);
        EditorApplication.delayCall += Capture;
    }
}
#endif
