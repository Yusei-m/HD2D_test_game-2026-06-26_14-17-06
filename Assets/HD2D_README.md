# HD-2D ウォーキングゲーム プロトタイプ

参照画像の HD-2D / 2.5D ピクセルアートの街並みを再現した、歩き回れるプロトタイプ一式です。

## 使い方

1. Unity でこのプロジェクトを開く。
2. 初回ロード時、`Assets/Scenes/HD2D_Town.unity` が自動生成されます
   （`HD2DAutoBuild` による。未保存シーンがある場合は安全のためスキップ）。
3. 生成されない場合・作り直したい場合はメニュー
   **Tools > HD2D > Build Walking Game Scene** を実行。
4. シーンを開いて **Play**。WASD / 矢印キーで歩けます。

## 構成

| ファイル | 役割 |
| --- | --- |
| `Scripts/PlayerController.cs` | プレイヤー移動（新 Input System、X-Z 平面を滑らかに移動） |
| `Scripts/CameraController.cs` | `Vector3.Lerp` による俯瞰カメラの滑らか追従 |
| `Scripts/Billboard.cs` | Y軸ビルボード（スプライトが常にカメラ正面・直立を維持） |
| `Editor/HD2DSceneBuilder.cs` | 地面・建物・プレイヤー・カメラ・Volume を全自動構築 |
| `Editor/HD2DAutoBuild.cs` | 初回ロード時に自動構築（一度だけ） |
| `Editor/PixelSpriteImportSettings.cs` | `Assets/Sprites/` のテクスチャを Point / 無圧縮で取り込み |

## 含まれるセットアップ

- **シーン**: 3D 地面、建物（家・店・教会風・風車）、フェンス付き花壇、木、土の道・石畳。
- **プレイヤー**: 空 GameObject + 子の SpriteRenderer（プログラム生成のドット絵キャラ）。
  Rigidbody（回転全軸固定 + Y位置固定 + 重力オフ）、BoxCollider 付き。
- **カメラ**: 俯瞰アイソメトリック（pitch 45°、FOV 34）、追従付き。
- **URP ポストプロセス**: Global Volume に Bloom（光の溢れ）と
  Depth of Field（Bokeh：背景ボケ）。霧で空気感を演出。
- **テクスチャ**: スプライトは Filter=Point / Compression=None。

> 自動生成キャラはプレースホルダのドット絵です。`Assets/Sprites/` に
> 正式なスプライトを入れれば、同フォルダ設定で自動的に Point / 無圧縮になります。
