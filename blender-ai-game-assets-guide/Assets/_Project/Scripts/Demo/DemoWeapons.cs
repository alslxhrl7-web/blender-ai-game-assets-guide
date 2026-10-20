using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 모델 읽기. 바르코3D → 블렌더로 만든 모델을 Resources/Weapons 에서 읽어, 총구가 앞(+Z)·위가 +Y 인 상태로 돌려준다.
/// 중심점은 손잡이(방아쇠)다. 저장소를 가볍게 하려고 FBX 에는 형태만 있고 색·법선 텍스처는 옆에 따로 있어서, 재질은 여기서 만든다.
/// </summary>
public static class DemoWeapons
{
    public const string M1 = "Weapons/M1_Garand";
    public const string Bazooka = "Weapons/M20_Bazooka";
    public const string Bar = "Weapons/BAR_M1918A2";
    public const string Mosin = "Weapons/Mosin_Nagant";
    /// <summary>1차 과제(블렌더 기초)에서 정리한 M1 개런드. 중심점이 개머리판 바닥이고 재질이 프리팹에 들어 있다.</summary>
    public const string M1Prop = "Weapons/Prop_M1Garand";

    /// <summary>실제 길이(m)</summary>
    public static float LengthOf(string resource) =>
        resource == M1Prop ? 1.10f : resource == M1 ? 1.103f : resource == Bazooka ? 1.524f : resource == Bar ? 1.215f : resource == Mosin ? 1.232f : 1f;

    /// <summary>중심점에서 총구까지가 개머리판까지보다 먼가. 바주카만 손잡이가 앞쪽에 있어 반대다.</summary>
    public static bool MuzzleIsFarEnd(string resource) => resource != Bazooka;

    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    public static Transform Load(string resource) => Load(resource, LengthOf(resource), MuzzleIsFarEnd(resource));

    public static Transform Load(string resource, float realLength, bool muzzleIsFarEnd)
    {
        var prefab = Resources.Load<GameObject>(resource);
        if (prefab == null) { Debug.LogWarning("[Five Rivers] 무기 모델 없음: Resources/" + resource); return null; }
        var holder = new GameObject("Weapon_" + prefab.name).transform;
        var model = Object.Instantiate(prefab, holder).transform;
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;

        if (!materials.TryGetValue(resource, out var mat) || mat == null)
        {
            // 금속·거칠기는 값 하나로 대신한다.
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var color = Resources.Load<Texture2D>("Weapons/T_" + prefab.name + "_BaseColor");
            var normal = Resources.Load<Texture2D>("Weapons/T_" + prefab.name + "_Normal");
            if (lit != null && color != null)
            {
                mat = new Material(lit) { name = "MAT_" + prefab.name };
                mat.SetTexture("_BaseMap", color);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", 0.15f);
                mat.SetFloat("_Smoothness", 0.3f);
                if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.EnableKeyword("_NORMALMAP"); }
                materials[resource] = mat;
            }
            else if (resource != M1Prop) Debug.LogWarning("[Five Rivers] 무기 텍스처 없음: " + prefab.name); // 1차 소품은 프리팹 재질을 그대로 쓴다
        }

        // 모델이 어느 축으로 누워 있든 맞춰 세운다: 가장 긴 축이 총열, 그다음 긴 축이 위아래.
        var b = new Bounds(Vector3.zero, Vector3.zero);
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var rb = r.bounds; // holder 가 원점에 있어 그대로 holder 기준 좌표다
            b.Encapsulate(rb.min); b.Encapsulate(rb.max);
        }
        foreach (var c in model.GetComponentsInChildren<Collider>()) Object.Destroy(c); // 사격 판정에 걸리지 않게
        int longAxis = 0, upAxis = 0;
        for (int i = 1; i < 3; i++) if (b.size[i] > b.size[longAxis]) longAxis = i;
        float best = -1f;
        for (int i = 0; i < 3; i++) if (i != longAxis && b.size[i] > best) { best = b.size[i]; upAxis = i; }
        Vector3 forward = Vector3.zero, up = Vector3.zero;
        bool plusIsFar = b.max[longAxis] > -b.min[longAxis];
        forward[longAxis] = plusIsFar == muzzleIsFarEnd ? 1f : -1f;
        up[upAxis] = b.max[upAxis] > -b.min[upAxis] ? 1f : -1f; // 중심점(손잡이)에서 위쪽이 더 길다
        model.localRotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
        float scale = b.size[longAxis] > 0.001f ? realLength / b.size[longAxis] : 1f;
        model.localScale *= scale;
        // 1차 소품은 중심점이 개머리판 끝이라, 방아쇠(개머리판에서 약 38%)가 손 자리에 오도록 뒤로·아래로 옮긴다.
        if (resource == M1Prop) model.localPosition = new Vector3(0f, -0.07f, -realLength * 0.38f);
        return holder;
    }
}
