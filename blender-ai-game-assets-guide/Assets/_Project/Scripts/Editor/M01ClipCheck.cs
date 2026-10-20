#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 인물 동작 점검: 프리팹 하나를 꺼내 동작 클립을 시각별로 직접 맞춰 보고(SampleAnimation) 옆에서 찍는다. 다리 뼈 곡선이 클립에 들어 있는지도 적는다.
/// 배치: -executeMethod M01ClipCheck.Batch  → Shots/Clip/, Logs/M01Demo/clipcheck.txt
/// </summary>
public static class M01ClipCheck
{
    public static void Batch()
    {
        Directory.CreateDirectory("Shots/Clip");
        var log = new System.Text.StringBuilder();
        foreach (var (prefabPath, fbxPath) in new[] {
            ("Assets/_Project/Resources/Characters/Ally_Kim.prefab", "Assets/_Project/Resources/Characters/Allies/Ally_Kim.fbx"),
            ("Assets/_Project/Resources/Characters/Enemy_Soldier.prefab", "Assets/_Project/Resources/Characters/Enemy/Enemy_NK.fbx") })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
            foreach (var clip in clips)
            {
                var binds = AnimationUtility.GetCurveBindings(clip);
                int legs = binds.Count(b => b.path.Contains("Leg") || b.path.Contains("Foot"));
                log.AppendLine($"{prefab.name} {clip.name} len={clip.length:F2}s curves={binds.Length} legCurves={legs}");
                if (clip.name != "Fire" && clip.name != "Cover") continue;
                for (int i = 0; i < 3; i++)
                {
                    var g = Object.Instantiate(prefab);
                    float t = clip.length * i / 2f;
                    clip.SampleAnimation(g, t);
                    float low = g.GetComponentsInChildren<SkinnedMeshRenderer>().Min(r => { var m = new Mesh(); r.BakeMesh(m, true); var lo = m.vertices.Min(v => r.transform.TransformPoint(v).y); Object.DestroyImmediate(m); return lo; });
                    log.AppendLine($"   t={t:F2} lowest={low:F3}");
                    Shot(g, $"Shots/Clip/{prefab.name}_{clip.name}_{i}.png");
                    Object.DestroyImmediate(g);
                }
            }
        }
        Directory.CreateDirectory("Logs/M01Demo");
        File.WriteAllText("Logs/M01Demo/clipcheck.txt", log.ToString());
        EditorApplication.Exit(0);
    }

    static void Shot(GameObject g, string path)
    {
        var go = new GameObject("cam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 1.1f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.white;
        cam.transform.position = g.transform.position + new Vector3(4f, 0.9f, 0f);
        cam.transform.LookAt(g.transform.position + Vector3.up * 0.9f);
        var light = new GameObject("l").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(40f, -60f, 0f);
        var rt = new RenderTexture(400, 500, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(400, 500, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 400, 500), 0, 0); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null;
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go); Object.DestroyImmediate(light.gameObject);
    }
}
#endif
