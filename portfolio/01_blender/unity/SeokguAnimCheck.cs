using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 1차 평가 9단계: Hero_Seokgu.fbx의 Idle·Walk 클립에 Loop Time을 켜고 Animator로 재생되는지 확인한다.
// 바르코3D 리그는 Head 본이 없어 Humanoid 아바타 조건을 채우지 못하므로 Generic으로 가져온다.
public static class SeokguAnimCheck
{
    const string Fbx = "Assets/_Project/Art/Portfolio/Clean/Hero_Seokgu.fbx";
    const string Ctrl = "Assets/_Project/Art/Portfolio/Clean/Seokgu.controller";

    [MenuItem("FiveRivers/Portfolio/Seokgu Anim Check")]
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "portfolio", "01_blender"));
        File.Copy(Path.Combine(root, "export", "Hero_Seokgu.fbx"), Fbx, true);
        AssetDatabase.ImportAsset(Fbx, ImportAssetOptions.ForceUpdate);

        var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
        imp.animationType = ModelImporterAnimationType.Generic;
        var takes = imp.importedTakeInfos;
        var log = new StringBuilder();
        log.AppendLine("takes: " + string.Join(", ", takes.Select(t => $"{t.name} [{t.startTime * t.sampleRate:F0}-{t.stopTime * t.sampleRate:F0}] @{t.sampleRate}fps")));
        var clips = takes.Select(t =>
        {
            bool walk = t.name.ToLower().Contains("walk");
            float rate = t.sampleRate;
            var c = new ModelImporterClipAnimation
            {
                takeName = t.name,
                name = walk ? "Walk" : "Idle",
                firstFrame = walk ? 7f - 1f : t.startTime * rate,   // Walk 반복 구간: 블렌더 프레임 7~74 (FBX는 1프레임부터 구움)
                lastFrame = walk ? 74f - 1f : t.stopTime * rate,
                loopTime = true,
                loopPose = walk,
            };
            if (!walk) { c.firstFrame = t.startTime * rate; c.lastFrame = t.stopTime * rate; }
            return c;
        }).ToArray();
        imp.clipAnimations = clips;
        imp.SaveAndReimport();

        // 머티리얼: BlenderCompareBuilder가 만든 것을 다시 쓴다
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Portfolio/Clean/Textures_Hero_Seokgu/M_Hero_Seokgu.mat");
        var all = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToArray();
        foreach (var c in all)
            log.AppendLine($"clip {c.name}: length {c.length:F2}s, frames {c.length * c.frameRate:F0}, loop {c.isLooping}, fps {c.frameRate}");

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(Ctrl);
        var sm = ctrl.layers[0].stateMachine;
        foreach (var c in all) sm.AddState(c.name).motion = c;
        sm.defaultState = sm.states.First(s => s.state.name == "Idle").state;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        Directory.CreateDirectory(Path.Combine(root, "captures", "unity"));
        foreach (var c in all)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (mat) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            log.AppendLine($"root rot {go.transform.localEulerAngles} scale {go.transform.localScale}");
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.position = new Vector3(2.2f, 1.1f, 2.6f);
            cam.transform.LookAt(new Vector3(0, 0.85f, 0));
            cam.fieldOfView = 35f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.22f, 0.23f, 0.22f);
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(40, -30, 0); light.intensity = 1.3f;
            // 세 시점을 한 장에
            var tex = new Texture2D(1500, 700, TextureFormat.RGB24, false);
            go.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                // 배치 모드에서는 같은 오브젝트를 다시 샘플링하면 스킨이 갱신되지 않아 시점마다 새로 만든다
                var shot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (mat) foreach (var r in shot.GetComponentsInChildren<Renderer>()) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                c.SampleAnimation(shot, c.length * i / 3f);
                var rt = new RenderTexture(500, 700, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 500, 700), i * 500, 0); cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(rt); Object.DestroyImmediate(shot);
            }
            tex.Apply();
            File.WriteAllBytes(Path.Combine(root, "captures", "unity", $"unity_anim_{c.name}.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(go); Object.DestroyImmediate(cam.gameObject); Object.DestroyImmediate(light.gameObject);
        }
        File.WriteAllText(Path.Combine(root, "unity_anim.md"), log.ToString(), new UTF8Encoding(false));
        Debug.Log("SEOKGU_ANIM_DONE\n" + log);
    }
}
