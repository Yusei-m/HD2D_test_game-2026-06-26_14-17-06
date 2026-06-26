using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 初回ロード時に HD-2D シーンが未生成であれば自動構築する。
/// - シーンファイルが既に存在する場合は何もしない
/// - 編集中（未保存の変更がある）シーンは破棄しないようスキップ
/// - 一度実行したら EditorPrefs に記録し、再実行しない
/// 手動で作り直したいときは Tools > HD2D > Build Walking Game Scene を使う。
/// </summary>
[InitializeOnLoad]
public static class HD2DAutoBuild
{
    private const string ScenePath = "Assets/Scenes/HD2D_Town.unity";
    private const string PrefKey = "HD2D_AutoBuilt_v1";

    static HD2DAutoBuild()
    {
        EditorApplication.delayCall += TryBuild;
    }

    private static void TryBuild()
    {
        EditorApplication.delayCall -= TryBuild;

        if (EditorPrefs.GetBool(PrefKey, false)) return;
        if (System.IO.File.Exists(ScenePath)) { EditorPrefs.SetBool(PrefKey, true); return; }

        // 未保存の変更を持つシーンを開いている場合は安全のためスキップ
        if (EditorSceneManager.GetActiveScene().isDirty) return;

        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        EditorPrefs.SetBool(PrefKey, true);
        HD2DSceneBuilder.Build();
        EditorSceneManager.OpenScene(ScenePath);
    }
}
