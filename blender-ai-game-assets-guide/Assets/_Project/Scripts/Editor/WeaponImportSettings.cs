#if UNITY_EDITOR
using System.Linq;
using UnityEditor;

/// <summary>
/// Resources/Weapons · Vehicles · Characters/Enemy 에 넣는 바르코3D → 블렌더 파일의 가져오기 설정을 자동으로 맞춘다.
/// 텍스처 이름이 _Normal 로 끝나면 법선 지도로, FBX 는 재질을 따로 만들지 않게 한다(재질은 텍스처로 따로 만든다).
/// 적(Characters/Enemy)만 애니메이션을 가져오고, 클립 이름을 Run·Cover·Fire·Death 로 줄이고 쓰러짐 말고는 반복시킨다.
/// </summary>
public class WeaponImportSettings : AssetPostprocessor
{
    static readonly string[] Folders = { "/Resources/Weapons/", "/Resources/Vehicles/", "/Resources/Characters/Enemy/", "/Resources/Characters/Allies/", "/Resources/Background/" };
    bool Mine => Folders.Any(assetPath.Contains);
    bool Animated => assetPath.Contains("/Resources/Characters/Enemy/") || assetPath.Contains("/Resources/Characters/Allies/");
    /// <summary>규칙을 바꾸면 이 숫자를 올린다. 해당 파일들이 다시 가져와진다.</summary>
    public override uint GetVersion() => 2;

    void OnPreprocessTexture()
    {
        if (!Mine) return;
        var importer = (TextureImporter)assetImporter;
        if (System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith("_Normal")) importer.textureType = TextureImporterType.NormalMap;
        importer.mipmapEnabled = true;
    }

    void OnPreprocessModel()
    {
        if (!Mine) return;
        var importer = (ModelImporter)assetImporter;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.importAnimation = Animated;
        importer.animationType = Animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
        // 압축을 끈다: 사격 동작은 하체를 한 자세로 고정해 두었는데(fix_fire.py), 압축이 "변하지 않는 곡선"으로 보고 지우면
        // 다리가 모델의 기본 자세(웅크림)로 돌아가 공중에 뜬 것처럼 보인다.
        if (Animated) importer.animationCompression = ModelImporterAnimationCompression.Off;
        importer.importCameras = false;
        importer.importLights = false;
    }

    void OnPreprocessAnimation()
    {
        if (!Animated) return;
        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = c.name.Substring(c.name.LastIndexOf('|') + 1); // "Enemy_NK_Rig|Run" → "Run"
            c.loopTime = c.name != "Death";
        }
        importer.clipAnimations = clips;
    }
}
#endif
