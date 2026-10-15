using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 1차 평가 9단계: 바르코3D 원본 FBX와 블렌더 정리본 FBX를 같은 씬에 나란히 놓고 비교한다.
// 배치 모드: Unity.exe -batchmode -quit -projectPath . -executeMethod BlenderCompareBuilder.Build
public static class BlenderCompareBuilder
{
    const string ArtDir = "Assets/_Project/Art/Portfolio";
    const string ScenePath = "Assets/_Project/Scenes/Portfolio/BlenderCompare.unity";
    static readonly string[] Raw = { "Seokgu_varco.fbx", "M1Garand_varco.fbx", "Sandbag_varco.fbx" };
    static readonly string[] Clean = { "Hero_Seokgu.fbx", "Prop_M1Garand.fbx", "Env_SandbagPosition.fbx" };

    [MenuItem("FiveRivers/Portfolio/Build BlenderCompare")]
    public static void Build()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "portfolio", "01_blender"));
        Directory.CreateDirectory(ArtDir + "/Raw");
        Directory.CreateDirectory(ArtDir + "/Clean");
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        for (int i = 0; i < 3; i++)
        {
            File.Copy(Path.Combine(root, "varco_raw", Raw[i]), $"{ArtDir}/Raw/{Raw[i]}", true);
            File.Copy(Path.Combine(root, "export", Clean[i]), $"{ArtDir}/Clean/{Clean[i]}", true);
        }
        AssetDatabase.Refresh();
        // FBX 안에 묻힌 텍스처를 꺼내야 URP 머티리얼에 연결된다
        foreach (var sub in new[] { "Raw", "Clean" })
            foreach (var file in Directory.GetFiles($"{ArtDir}/{sub}", "*.fbx"))
            {
                string path = file.Replace('\\', '/');
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                string texDir = $"{ArtDir}/{sub}/Textures_{Path.GetFileNameWithoutExtension(path)}";
                Directory.CreateDirectory(texDir);
                imp.ExtractTextures(texDir);
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Ref_1m_Cube";
        cube.transform.position = new Vector3(0f, 0.5f, -1.5f);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(4f, 1f, 3f);

        var report = new StringBuilder();
        report.AppendLine("| 리소스 | 구분 | 삼각형 | 크기 X·Y·Z (m) | 루트 Rot | 루트 Scale | 메시 수 |");
        report.AppendLine("|---|---|---|---|---|---|---|");
        // 정리 전은 왼쪽(−X), 정리 후는 오른쪽(+X). 진지는 뒤쪽
        Vector3[] slots = { new Vector3(1.4f, 0, -1.5f), new Vector3(0.75f, 0, -1.5f), new Vector3(5f, 0, -5f) };
        string[] names = { "오석구", "M1 개런드", "모래주머니 진지" };
        for (int i = 0; i < 3; i++)
        {
            var rawGo = Place($"{ArtDir}/Raw/{Raw[i]}", "RAW_" + Raw[i], slots[i]);
            var cleanGo = Place($"{ArtDir}/Clean/{Clean[i]}", "CLEAN_" + Clean[i], new Vector3(-slots[i].x, 0, slots[i].z));
            report.AppendLine(Row(names[i], "정리 전", rawGo));
            report.AppendLine(Row(names[i], "정리 후", cleanGo));
        }

        var cam = Camera.main;
        cam.transform.position = new Vector3(0f, 3.2f, 6.5f);
        cam.transform.LookAt(new Vector3(0f, 0.8f, -0.5f));
        cam.fieldOfView = 55f;
        Directory.CreateDirectory(Path.Combine(root, "captures", "unity"));
        Shot(cam, Path.Combine(root, "captures", "unity", "unity_compare_game.png"));
        // 가까이: 석구·소총·1 m 큐브
        cam.transform.position = new Vector3(0f, 1.5f, 2.4f);
        cam.transform.LookAt(new Vector3(0f, 0.75f, -1.5f));
        cam.fieldOfView = 40f;
        Shot(cam, Path.Combine(root, "captures", "unity", "unity_compare_close.png"));

        EditorSceneManager.SaveScene(scene, ScenePath);
        File.WriteAllText(Path.Combine(root, "unity_compare.md"), report.ToString(), new UTF8Encoding(false));
        Debug.Log("BLENDER_COMPARE_DONE\n" + report);
    }

    static GameObject Place(string assetPath, string name, Vector3 pos)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.position = pos;
        // 꺼낸 base_color로 URP Lit 머티리얼을 만들어 붙인다
        string texDir = $"{Path.GetDirectoryName(assetPath).Replace('\\', '/')}/Textures_{Path.GetFileNameWithoutExtension(assetPath)}";
        string matPath = $"{texDir}/M_{Path.GetFileNameWithoutExtension(assetPath)}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/base_color.png"));
            mat.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        return go;
    }

    static string Row(string res, string kind, GameObject go)
    {
        var mfs = go.GetComponentsInChildren<MeshFilter>();
        int tris = mfs.Sum(m => m.sharedMesh ? m.sharedMesh.triangles.Length / 3 : 0);
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        var t = go.transform;
        // 루트와 첫 메시 자식의 Transform을 함께 적는다(정리 전 FBX는 자식에 회전·스케일이 들어 있을 수 있다)
        var child = mfs[0].transform;
        string rot = $"{t.localEulerAngles} / child {child.localEulerAngles}";
        string scl = $"{t.localScale} / child {child.localScale}";
        return $"| {res} | {kind} | {tris:N0} | {b.size.x:F2} · {b.size.y:F2} · {b.size.z:F2} | {rot} | {scl} | {mfs.Length} |";
    }

    static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
    }
}
