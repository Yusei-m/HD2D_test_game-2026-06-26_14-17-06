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
    // 明るさ（露出/環境光/Bloom）と被写界深度を調整したのでバージョンを上げ、
    // 既存の生成済みシーン・ボリュームプロファイルを作り直す。
    private const string PrefKey = "HD2D_AutoBuilt_v18";

    static HD2DAutoBuild()
    {
        EditorApplication.delayCall += TryBuild;
    }

    private static void TryBuild()
    {
        EditorApplication.delayCall -= TryBuild;

        if (EditorPrefs.GetBool(PrefKey, false)) return;

        // 未保存の変更を持つシーンを開いている場合は安全のためスキップ。
        // （生成済みの HD2D_Town を開いているだけなら dirty にはならず、作り直す。）
        if (EditorSceneManager.GetActiveScene().isDirty) return;

        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        EditorPrefs.SetBool(PrefKey, true);
        HD2DSceneBuilder.Build();   // 新規シーンを生成し、ScenePath へ上書き保存する
        EditorSceneManager.OpenScene(ScenePath);
    }
}
