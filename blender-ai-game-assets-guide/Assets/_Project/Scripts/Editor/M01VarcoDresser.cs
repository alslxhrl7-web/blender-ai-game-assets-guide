#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 배경 소품을 바르코3D 모델로 바꾼다. 메뉴: Five Rivers > 배경 소품 바르코 모델로 바꾸기
/// 순서: 바르코3D(고증 이미지 → 3D) → 블렌더 clean_prop.py(게임 크기·원점·삼각형 줄이기) → Resources/Background/BG_*.fbx
///       → 여기서 프리팹(Resources/Props/BG_*) → M01_Dabudong 씬에 배치(기획서 7장 소품 배치 계획).
/// 코드로 만든 나무·바위·관목(M01RealisticBuilder)은 바르코 모델로 바꾸고, 1950년 경북 산지의 민둥산에 맞게 나무 수를 줄인다.
/// 다시 실행해도 같은 결과가 나온다(이전에 놓은 Varco_Dressing 을 지우고 다시 놓는다).
/// </summary>
public static class M01VarcoDresser
{
    const string Dir = "Assets/_Project/Resources/Background/";
    const string PrefabDir = "Assets/_Project/Resources/Props/";
    const string MatDir = "Assets/_Project/Art/Background/";
    const string Scene = "Assets/_Project/Scenes/Missions/" + M01Demo.SceneName + ".unity";

    // 충돌체: 0 없음, 1 모델 크기 상자, 2 줄기만(나무)
    static readonly (string name, int collider)[] Models =
    {
        ("BG_Pine", 2), ("BG_DeadTree", 2), ("BG_Bush", 0), ("BG_RockA", 0), ("BG_RockB", 0), ("BG_RockC", 0),
        ("BG_SandbagWall", 1), ("BG_Parapet", 0), ("BG_Revetment", 1), ("BG_AmmoCrates", 1),
        ("BG_FieldPhone", 0), ("BG_CanteenShovel", 0), ("BG_BarbedWire", 0), ("BG_TelegraphPole", 2),
        ("BG_OxCart", 1), ("BG_RocketCrate", 1), ("BG_Drums", 1), ("BG_BurnedBarn", 1), ("BG_Straw", 1), ("BG_Onggi", 1), ("BG_Jige", 0),
    };

    static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    static Terrain terrain;
    static Transform root;
    static int placed;

    [MenuItem("Five Rivers/배경 소품 바르코 모델로 바꾸기")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        MakePrefabs();
        Dress();
    }

    /// <summary>배치 모드: -executeMethod M01VarcoDresser.Batch</summary>
    public static void Batch()
    {
        try { MakePrefabs(); Dress(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    // =====================================================================
    //  프리팹
    // =====================================================================
    static void MakePrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Background")) AssetDatabase.CreateFolder("Assets/_Project/Art", "Background");
        prefabs.Clear();
        foreach (var (name, collider) in Models)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + name + ".fbx");
            if (model == null) { Debug.LogWarning("[Five Rivers] 배경 모델 없음: " + Dir + name + ".fbx"); continue; }
            string matPath = MatDir + "M_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "T_" + name + "_BaseColor.jpg"));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "T_" + name + "_Normal.jpg");
            if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
            mat.SetFloat("_Metallic", name == "BG_Drums" ? 0.3f : 0f);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);

            var g = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in g.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = mat;
                bounds.Encapsulate(r.bounds);
            }
            if (collider == 1) { var b = g.AddComponent<BoxCollider>(); b.center = bounds.center; b.size = bounds.size; }
            if (collider == 2) { var b = g.AddComponent<BoxCollider>(); b.center = new Vector3(0f, bounds.size.y * .3f, 0f); b.size = new Vector3(.35f, bounds.size.y * .6f, .35f); }
            prefabs[name] = PrefabUtility.SaveAsPrefabAsset(g, PrefabDir + name + ".prefab");
            Object.DestroyImmediate(g);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Five Rivers] 배경 프리팹 {prefabs.Count}개를 만들었습니다: {PrefabDir}BG_*");
    }

    // =====================================================================
    //  배치
    // =====================================================================
    static void Dress()
    {
        var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        terrain = Object.FindAnyObjectByType<Terrain>();
        var old = GameObject.Find("Varco_Dressing");
        root = new GameObject("Varco_Dressing").transform;
        // 코드 식생은 처음 실행 때 지워지므로, 다시 실행할 때는 이미 바꾼 식생을 그대로 옮겨 둔다
        var keptVeg = old != null ? old.transform.Find("Vegetation_Varco") : null;
        if (keptVeg != null) keptVeg.SetParent(root, false);
        if (old != null) Object.DestroyImmediate(old);
        placed = 0;
        var rng = new System.Random(1950);

        ReplaceVegetation(rng);
        DressTrench(rng);
        DressCover(rng);
        DressSlope();
        DressRoadAndMound();
        DressDistance();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Five Rivers] 바르코 배경 소품 {placed}개를 놓았습니다: {Scene}");
    }

    /// <summary>
    /// 코드로 만든 소나무 520·관목 420·바위 300 자리를 그대로 쓰되, 민둥산 고증에 맞게 나무는 다섯에 하나만 남긴다
    /// (그중 절반 가까이는 포격에 말라 죽은 나무). 관목은 진지에서 보이는 곳 위주로 줄이고, 바위는 모두 바르코 바위로 바꾼다.
    /// </summary>
    static void ReplaceVegetation(System.Random rng)
    {
        var veg = GameObject.Find("Vegetation");
        if (veg == null) { Debug.LogWarning("[Five Rivers] Vegetation 없음"); return; }
        var parent = root.Find("Vegetation_Varco") ?? Group("Vegetation_Varco");
        var kids = new List<Transform>();
        foreach (Transform t in veg.transform) kids.Add(t);
        int trees = 0, bushes = 0, rocks = 0;
        foreach (var t in kids)
        {
            string n = t.name;
            Vector3 p = t.position, s = t.localScale;
            var rot = t.rotation;
            if (n.StartsWith("RL_Pine"))
            {
                if (rng.NextDouble() < .2)
                {
                    bool dead = rng.NextDouble() < .45;
                    Place(dead ? "BG_DeadTree" : "BG_Pine", parent, p, rot, Vector3.one * Mathf.Clamp(s.y, .75f, 1.3f), false);
                    trees++;
                }
            }
            else if (n.StartsWith("RL_Bush"))
            {
                float distToSlope = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(0f, 40f));
                if (distToSlope < 70f && rng.NextDouble() < .1)
                {
                    Place("BG_Bush", parent, p, rot, Vector3.one * Mathf.Clamp(s.x, .7f, 1.5f), false);
                    bushes++;
                }
            }
            else if (n.StartsWith("RL_Rock"))
            {
                string pick = "BG_Rock" + (char)('A' + rng.Next(3));
                Place(pick, parent, p, rot, new Vector3(s.x, s.y, s.z) * .7f, false);
                rocks++;
            }
            else continue;
            Object.DestroyImmediate(t.gameObject);
        }
        Debug.Log($"[Five Rivers] 식생 교체: 나무 {trees}, 관목 {bushes}, 바위 {rocks}");
    }

    /// <summary>A 진지: 흉벽 모래주머니, 모래주머니 더미 8, 탄약상자 3, 야전 전화기 1, 수통·야전삽 2, 참호 판자 보강 1</summary>
    static void DressTrench(System.Random rng)
    {
        var parent = Group("Trench_Varco");
        // 코드로 만든 흉벽 모래주머니와 탄약상자는 숨긴다(충돌체는 그레이박스 Zones 에 따로 있다)
        Hide("Sandbag_Parapets"); Hide("Ammo_Crates");

        const float top = 0.94f; // 흙둑(1.0m) 위
        for (float x = -18.5f; x <= 19f; x += 2.9f)
        {
            Place("BG_Parapet", parent, new Vector3(x, top, 4.5f), Quaternion.Euler(0f, R(rng, -3f, 3f), 0f), Vector3.one, false);
            Place("BG_Parapet", parent, new Vector3(x + .7f, top, -4.5f), Quaternion.Euler(0f, 180f + R(rng, -3f, 3f), 0f), Vector3.one, false);
        }
        for (float z = -3f; z <= 3.1f; z += 2.9f)
            Place("BG_Parapet", parent, new Vector3(-20.5f, top, z), Quaternion.Euler(0f, 90f + R(rng, -3f, 3f), 0f), Vector3.one, false);

        // 모래주머니 더미 8: 참호 안 사격 자리(북쪽 벽 앞)와 뒤쪽 구석. 플레이어 이동선(z 0 ~ -2)은 비운다.
        var piles = new[] { new Vector3(-11f, 0f, 3.2f), new Vector3(-5f, 0f, 3.3f), new Vector3(3.5f, 0f, 3.3f), new Vector3(9f, 0f, 3.2f),
                            new Vector3(15f, 0f, 3.3f), new Vector3(-19f, 0f, -3.3f), new Vector3(-8f, 0f, -3.4f), new Vector3(17.5f, 0f, -3.4f) };
        foreach (var p in piles) Place("BG_SandbagWall", parent, p, Quaternion.Euler(0f, R(rng, -6f, 6f), 0f), new Vector3(.8f, .9f, .7f));

        // 미군 탄약상자 3: 보충 지점(AMMO_01)과 양 끝
        Place("BG_AmmoCrates", parent, new Vector3(0f, 0f, -2.1f), Quaternion.Euler(0f, 4f, 0f), Vector3.one, false);
        Place("BG_AmmoCrates", parent, new Vector3(-13f, 0f, -3.3f), Quaternion.Euler(0f, -10f, 0f), Vector3.one);
        Place("BG_AmmoCrates", parent, new Vector3(12.5f, 0f, -3.3f), Quaternion.Euler(0f, 12f, 0f), Vector3.one);

        Place("BG_FieldPhone", parent, new Vector3(6.8f, 0f, -3.4f), Quaternion.Euler(0f, 165f, 0f), Vector3.one);
        Place("BG_CanteenShovel", parent, new Vector3(-6.5f, 0f, -3.4f), Quaternion.Euler(0f, 170f, 0f), Vector3.one);
        Place("BG_CanteenShovel", parent, new Vector3(10.5f, 0f, 3.4f), Quaternion.Euler(0f, 10f, 0f), Vector3.one);

        // 진지 뒤 판자로 두른 엄폐호
        Place("BG_Revetment", parent, new Vector3(-9f, 0f, -9.5f), Quaternion.Euler(0f, 4f, 0f), Vector3.one);
    }

    /// <summary>B 경사면 엄폐 12곳: 코드로 만든 바위·모래주머니 모양을 바르코 바위·모래주머니로 바꾼다(충돌은 그레이박스 엄폐물이 맡는다)</summary>
    static void DressCover(System.Random rng)
    {
        var cv = GameObject.Find("Cover_Visuals");
        if (cv == null) return;
        var parent = Group("Cover_Varco");
        var kids = new List<Transform>();
        foreach (Transform t in cv.transform) kids.Add(t);
        int i = 0;
        foreach (var t in kids)
        {
            string n = t.name;
            if (n.EndsWith("_Rock"))
                Place("BG_Rock" + (char)('A' + i++ % 3), parent, t.position - Vector3.up * .45f, t.rotation, new Vector3(1.15f, 1.15f, .85f), false);
            else if (n.Contains("_RockSmall"))
                Place("BG_Rock" + (char)('A' + i++ % 3), parent, t.position - Vector3.up * .1f, t.rotation, Vector3.one * .35f, false);
            else if (n.EndsWith("_Sandbags"))
                Place("BG_SandbagWall", parent, t.position, t.rotation, new Vector3(.8f, 1.15f, .9f), false);
            else continue;
            t.gameObject.SetActive(false);
        }
        // 엄폐물 옆 관목 몇 개
        foreach (Transform cp in GameObject.Find("Cover").transform)
            if (rng.NextDouble() < .4)
                Place("BG_Bush", parent, Vector3.Scale(cp.position, new Vector3(1f, 0f, 1f)) + new Vector3(R(rng, 1.2f, 1.8f) * (rng.Next(2) * 2 - 1), 0f, R(rng, -.5f, .8f)), Quaternion.Euler(0f, R(rng, 0f, 360f), 0f), Vector3.one * R(rng, .8f, 1.1f));
    }

    /// <summary>B 경사면 철조망 말뚝 6: 진입로(x -20, 0, 22)를 피해 진지 앞에 늘어놓는다. 맵 양쪽 끝은 철조망으로 막는다.</summary>
    static void DressSlope()
    {
        var parent = Group("Wire_Varco");
        foreach (var (x, z, yaw) in new[] { (-30f, 13f, 4f), (-11f, 12f, -3f), (10f, 12.5f, 5f), (31f, 13f, -6f), (-50f, 20f, 80f), (55f, 10f, 95f) })
            Place("BG_BarbedWire", parent, new Vector3(x, 0f, z), Quaternion.Euler(0f, yaw, 0f), Vector3.one);
    }

    /// <summary>C 도로: 나무 전봇대 3, 부서진 소달구지 1. D 둔덕: 바주카 로켓 상자 1, 드럼통 2</summary>
    static void DressRoadAndMound()
    {
        var parent = Group("Road_Varco");
        foreach (var z in new[] { -12f, 32f, 74f }) Place("BG_TelegraphPole", parent, new Vector3(45.5f, 0f, z), Quaternion.Euler(0f, 90f, R1(z)), Vector3.one);
        Place("BG_OxCart", parent, new Vector3(36.8f, 0f, 48f), Quaternion.Euler(0f, 160f, 0f), Vector3.one);

        var mound = Group("Mound_Varco");
        Place("BG_RocketCrate", mound, new Vector3(26.2f, 0f, -7.6f), Quaternion.Euler(0f, 75f, 0f), Vector3.one);
        Place("BG_Drums", mound, new Vector3(31f, 0f, -4.2f), Quaternion.Euler(0f, 20f, 0f), Vector3.one);
    }

    /// <summary>원경: 불탄 초가 헛간 2, 볏짚 더미, 옹기, 지게. 새벽에 동쪽(A_EAST_ENTRY 쪽)을 볼 때 보이는 마을 터</summary>
    static void DressDistance()
    {
        var parent = Group("Village_Varco");
        Place("BG_BurnedBarn", parent, new Vector3(60f, 0f, -18f), Quaternion.Euler(0f, -100f, 0f), Vector3.one);
        Place("BG_BurnedBarn", parent, new Vector3(57f, 0f, -44f), Quaternion.Euler(0f, -70f, 0f), Vector3.one * .9f);
        Place("BG_Straw", parent, new Vector3(54f, 0f, -26f), Quaternion.identity, Vector3.one);
        Place("BG_Straw", parent, new Vector3(64f, 0f, -30f), Quaternion.Euler(0f, 40f, 0f), Vector3.one * .8f);
        Place("BG_Onggi", parent, new Vector3(55.5f, 0f, -15.5f), Quaternion.Euler(0f, -120f, 0f), Vector3.one);
        Place("BG_Jige", parent, new Vector3(56f, 0f, -21f), Quaternion.Euler(-8f, -95f, 0f), Vector3.one);
        Place("BG_Jige", parent, new Vector3(53f, 0f, -41f), Quaternion.Euler(0f, -60f, 0f), Vector3.one);
    }

    // =====================================================================
    //  도움
    // =====================================================================
    static Transform Group(string name)
    {
        var g = new GameObject(name).transform;
        g.SetParent(root, false);
        return g;
    }

    /// <summary>prefab 을 땅 높이에 놓는다. y 는 땅에서 띄울 높이(참호 안은 바닥 0, 흉벽 위는 그 높이 그대로)</summary>
    static GameObject Place(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, bool snap = true)
    {
        if (!prefabs.TryGetValue(name, out var prefab)) return null;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        g.transform.SetParent(parent, false);
        if (snap) pos.y += Ground(pos.x, pos.z);
        g.transform.SetPositionAndRotation(pos, rot);
        g.transform.localScale = scale;
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        placed++;
        return g;
    }

    static float Ground(float x, float z)
    {
        if (Mathf.Abs(x) <= 20.5f && Mathf.Abs(z) < 4.2f) return 0.02f; // 참호 바닥
        if (terrain == null) return 0f;
        return terrain.transform.position.y + terrain.SampleHeight(new Vector3(x, 0f, z));
    }

    static void Hide(string name)
    {
        var g = GameObject.Find(name);
        if (g != null) foreach (var r in g.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    static float R(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static float R1(float seed) => Mathf.Sin(seed * 12.9f) * 3f;
}
#endif
