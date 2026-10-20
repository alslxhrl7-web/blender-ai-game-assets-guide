#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1차 과제(블렌더 기초)에서 정리한 리소스로 게임용 프리팹을 만든다. 메뉴: Five Rivers > 1차 리소스 프리팹 만들기
/// - Hero_Seokgu.fbx (오석구, 20,143 tris, Idle·Walk) → Resources/Characters/Ally_Soldier.prefab (아군: 김덕만·박용팔·보충병·태석)
///                                                     → Resources/Characters/Enemy_Soldier.prefab (적: 같은 모델, 군복 색만 바꿈)
/// - Env_SandbagPosition.fbx (모래주머니 진지) → Resources/Props/Env_SandbagPosition.prefab (벽마다 MeshCollider)
/// 1차 문서의 남은 작업이던 법선(normal) 지도 연결도 여기서 한다.
/// </summary>
public static class M01CharacterSetup
{
    const string Art = "Assets/_Project/Art/Portfolio01/";
    const string Res = "Assets/_Project/Resources/";

    [MenuItem("Five Rivers/1차 리소스 프리팹 만들기")]
    public static void Build()
    {
        Directory.CreateDirectory(Res + "Characters");
        Directory.CreateDirectory(Res + "Props");
        SetNormal(Art + "Textures_Hero_Seokgu/normal.jpg");
        SetNormal(Art + "Textures_Env_SandbagPosition/normal.jpg");
        SetNormal(Art + "Textures_Prop_M1Garand/normal.jpg");

        var heroMat = AssetDatabase.LoadAssetAtPath<Material>(Art + "Textures_Hero_Seokgu/M_Hero_Seokgu.mat");
        LinkNormal(heroMat, Art + "Textures_Hero_Seokgu/normal.jpg");
        var sandMat = AssetDatabase.LoadAssetAtPath<Material>(Art + "Textures_Env_SandbagPosition/M_Env_SandbagPosition.mat");
        LinkNormal(sandMat, Art + "Textures_Env_SandbagPosition/normal.jpg");
        if (sandMat != null) { sandMat.SetColor("_BaseColor", new Color(0.72f, 0.66f, 0.55f)); sandMat.SetFloat("_Smoothness", 0.05f); } // 조명탄 빛에 하얗게 뜨지 않게 흙먼지 톤으로

        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Art + "Seokgu.controller");
        var hero = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Hero_Seokgu.fbx");
        Character(hero, controller, heroMat, Res + "Characters/Ally_Soldier.prefab");
        EnemyPrefab();
        foreach (var n in new[] { "T34_85", "GMC_Truck", "Mansu_Blanket" }) PropPrefab(n);

        // 1차 과제의 M1 개런드: 플레이어가 손에 든 총과 김덕만·보충병의 총으로 쓴다(Resources/Weapons/Prop_M1Garand).
        var m1Mat = AssetDatabase.LoadAssetAtPath<Material>(Art + "Textures_Prop_M1Garand/M_Prop_M1Garand.mat");
        LinkNormal(m1Mat, Art + "Textures_Prop_M1Garand/normal.jpg");
        var m1 = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Prop_M1Garand.fbx"));
        foreach (var r in m1.GetComponentsInChildren<Renderer>()) r.sharedMaterial = m1Mat;
        PrefabUtility.SaveAsPrefabAsset(m1, Res + "Weapons/Prop_M1Garand.prefab");
        Object.DestroyImmediate(m1);

        var env = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Env_SandbagPosition.fbx");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(env);
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            if (!mf.name.Contains("Ground")) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; // 흙 바닥은 지형과 겹치므로 뺀다
        PrefabUtility.SaveAsPrefabAsset(g, Res + "Props/Env_SandbagPosition.prefab");
        Object.DestroyImmediate(g);

        AssetDatabase.SaveAssets();
        Debug.Log("[Five Rivers] 1차 리소스 프리팹을 만들었습니다: Resources/Characters, Resources/Props");
    }

    /// <summary>
    /// 북한군 소총수: 바르코3D 고증 이미지 → 3D → Rig → Animate 4개(돌격·엄폐·사격·쓰러짐) → 블렌더에서 하나로 묶은 Enemy_NK.fbx.
    /// 상태 이름이 클립 이름과 같은 애니메이터를 만들고 프리팹으로 저장한다. 코드(DemoBody)가 상태 이름으로 바꿔 튼다.
    /// </summary>
    static void EnemyPrefab()
    {
        const string dir = Res + "Characters/Enemy/";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + "Enemy_NK.fbx");
        if (model == null) { Debug.LogWarning("[Five Rivers] 적 모델 없음: " + dir + "Enemy_NK.fbx"); return; }
        string ctlPath = "Assets/_Project/Art/Enemy_NK.controller";
        AssetDatabase.DeleteAsset(ctlPath);
        var ctl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctlPath);
        var sm = ctl.layers[0].stateMachine;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(dir + "Enemy_NK.fbx"))
            if (o is AnimationClip clip && !clip.name.StartsWith("__preview"))
            {
                var s = sm.AddState(clip.name);
                s.motion = clip;
                if (clip.name == "Run") sm.defaultState = s;
            }

        string matPath = "Assets/_Project/Art/M_Enemy_NK.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "T_Enemy_NK_BaseColor.jpg"));
        mat.SetFloat("_Smoothness", 0.2f);
        LinkNormal(mat, dir + "T_Enemy_NK_Normal.jpg");
        Character(model, ctl, mat, Res + "Characters/Enemy_Soldier.prefab");
    }

    /// <summary>바르코3D → 블렌더(clean_prop.py)로 정리한 전차·트럭·최만수(담요) → Resources/Props/{이름}.prefab</summary>
    static void PropPrefab(string name)
    {
        const string dir = Res + "Vehicles/";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(dir + name + ".fbx");
        if (model == null) { Debug.LogWarning("[Five Rivers] 모델 없음: " + dir + name + ".fbx"); return; }
        string matPath = "Assets/_Project/Art/M_" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "T_" + name + "_BaseColor.jpg"));
        mat.SetFloat("_Metallic", name == "T34_85" ? 0.4f : 0.1f);
        mat.SetFloat("_Smoothness", 0.25f);
        LinkNormal(mat, dir + "T_" + name + "_Normal.jpg");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(model);
        foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
        PrefabUtility.SaveAsPrefabAsset(g, Res + "Props/" + name + ".prefab");
        Object.DestroyImmediate(g);
    }

    static void Character(GameObject model, RuntimeAnimatorController controller, Material mat, string path)
    {
        var g = (GameObject)PrefabUtility.InstantiatePrefab(model);
        var anim = g.GetComponent<Animator>();
        if (anim == null) anim = g.AddComponent<Animator>(); // ?? 는 Unity 의 빈 오브젝트를 못 걸러서 쓰지 않는다
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        foreach (var r in g.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }
        PrefabUtility.SaveAsPrefabAsset(g, path);
        Object.DestroyImmediate(g);
    }

    static void SetNormal(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
        {
            ti.textureType = TextureImporterType.NormalMap;
            ti.SaveAndReimport();
        }
    }

    static void LinkNormal(Material m, string normalPath)
    {
        if (m == null) return;
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (n == null) return;
        m.SetTexture("_BumpMap", n);
        m.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(m);
    }
}
#endif
