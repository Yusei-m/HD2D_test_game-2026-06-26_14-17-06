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
  地面・道・壁・屋根・石畳には**手続き生成のピクセルテクスチャ**を貼り、
  単色ではなく質感のある見た目にしています（`Assets/Textures/`）。
- **プレイヤー**: 空 GameObject + 子の SpriteRenderer（アウトライン・陰影付きのドット絵キャラ）。
  足元に柔らかい**接地影**スプライト。
  Rigidbody（回転全軸固定 + Y位置固定 + 重力オフ）、BoxCollider 付き。
- **カメラ**: 俯瞰アイソメトリック（pitch 45°、FOV 34）、追従付き。
- **ライティング**: 低く差し込む暖色の西日（柔らかい影）＋ 三色アンビエント。
  霧と空の色を合わせ、黄昏の空気感を演出。
- **URP カラーグレード**（HD-2D の肝）: Global Volume に
  Bloom（光の溢れ）/ Depth of Field（Bokeh：背景ボケ）/
  Tonemapping（ACES）/ White Balance（暖色）/ Color Adjustments（彩度・コントラスト）/
  Vignette（四隅落とし）。
- **テクスチャ**: スプライトは Filter=Point / Compression=None。
  繰り返しテクスチャは Repeat / Point / 無圧縮。

> 自動生成のキャラ・テクスチャは「それらしく見せる」プレースホルダです。
> 参照画像のような作り込みには、実際のドット絵スプライトシートや 3D 建物モデルを
> `Assets/Sprites/` `Assets/Models/` に入れて差し替えてください。

## ビジュアルの調整ポイント

見た目を追い込むときは `Assets/Editor/HD2DSceneBuilder.cs` の以下を編集して、
メニュー **Tools > HD2D > Build Walking Game Scene** で作り直すのが速いです。

- `ConfigureEnvironment()` … 太陽の色・角度・強さ、霧、アンビエント。
- `BuildGlobalVolume()` … Bloom / DoF / 彩度 / 暖色味 / Vignette。
- 各 `*Texture()` … 地面・壁・屋根などのピクセル模様。
