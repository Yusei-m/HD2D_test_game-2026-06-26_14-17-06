using UnityEditor;
using UnityEngine;

/// <summary>
/// "Assets/Textures/" 配下のテクスチャを、地面タイルとして繰り返し使えるよう
/// Wrap=Repeat / Filter=Point / 無圧縮 で取り込む。
/// （手続き生成タイルもユーザー提供の *_tile.png も同じ設定に揃える。）
/// </summary>
public class GroundTextureImportSettings : AssetPostprocessor
{
    private const string TargetFolder = "/Textures/";

    private void OnPreprocessTexture()
    {
        if (!assetPath.Contains(TargetFolder)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Point;          // ドット絵をくっきり
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = true;                   // 遠景のチラつき防止
        importer.sRGBTexture = true;
    }
}
