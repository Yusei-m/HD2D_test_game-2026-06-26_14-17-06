using UnityEditor;
using UnityEngine;

/// <summary>
/// HD-2D 用テクスチャインポート自動設定。
/// "Assets/Sprites/" 配下のテクスチャを、3D空間でボケ・歪みが出ないよう
/// Filter Mode = Point / Compression = None / Sprite 設定で強制インポートする。
/// </summary>
public class PixelSpriteImportSettings : AssetPostprocessor
{
    private const string TargetFolder = "/Sprites/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.Contains(TargetFolder)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;            // ドット絵をくっきり保つ
        importer.textureCompression = TextureImporterCompression.Uncompressed; // None
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 32f;
    }
}
