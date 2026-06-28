using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// HD-2D / 2.5D ピクセルアートのウォーキングゲーム・プロトタイプを
/// シーンごと全自動で構築するエディタ拡張。
///
/// メニュー  Tools > HD2D > Build Walking Game Scene  を一度実行するだけで、
///   - 手続き生成のピクセルテクスチャを貼った地面・建物・フェンス・木
///   - SpriteRenderer + Rigidbody + BoxCollider を備えたプレイヤー（接地影付き）
///   - 俯瞰アイソメトリック視点のカメラ（追従付き）
///   - HD-2D らしい黄昏のライティングとカラーグレード
///     （Bloom / Depth of Field / Tonemapping / White Balance / Vignette）
/// をすべて生成し、即プレイ可能な状態で保存する。
/// </summary>
public static class HD2DSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/HD2D_Town.unity";
    private const string MatFolder = "Assets/Materials";
    private const string TexFolder = "Assets/Textures";   // 床・壁の繰り返しテクスチャ（Sprites とは分ける）
    private const string SpriteFolder = "Assets/Sprites";
    private const string ProfilePath = "Assets/Settings/HD2D_VolumeProfile.asset";

    [MenuItem("Tools/HD2D/Build Walking Game Scene")]
    public static void Build()
    {
        EnsureFolder(MatFolder);
        EnsureFolder(TexFolder);
        EnsureFolder(SpriteFolder);
        EnsureFolder("Assets/Scenes");

        // ---- 1. 新規シーン（Main Camera + Directional Light 付き）----
        var scene = EditorSceneManager.NewScene(
            NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        ConfigureEnvironment();

        // ---- 2. 地面 ----
        BuildGround();

        // ---- 3. 街並み（建物・道・フェンス・花壇・木）----
        BuildTown();

        // ---- 4. プレイヤー ----
        GameObject player = BuildPlayer();

        // ---- 5. カメラ ----
        ConfigureCamera(player.transform);

        // ---- 6. ポストプロセス（URP Global Volume）----
        BuildGlobalVolume();

        // ---- 6.5 空気感（舞う光粒・降り注ぐ光）----
        BuildAtmosphere(Camera.main);

        // ---- 7. 保存 ----
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        Selection.activeGameObject = player;

        Debug.Log("[HD2D] シーンを構築しました: " + ScenePath +
                  "  ▶ そのまま Play で歩き回れます。");
    }

    // ===================================================================
    //  環境（霧・ライト・アンビエント）— 黄昏のHD-2Dらしい空気感
    // ===================================================================
    private static void ConfigureEnvironment()
    {
        // オクトパストラベラー風：暖かい西日＋深い寒色の影、控えめな空気遠近。
        Color skyColor = new Color(0.93f, 0.82f, 0.66f); // 暖色の空
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = skyColor;
        RenderSettings.fogStartDistance = 26f;   // 近景はくっきり、遠景だけ霞ませる
        RenderSettings.fogEndDistance = 95f;

        // アンビエントは低め＆やや寒色。影を深く見せてコントラストを稼ぐ。
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.95f, 0.94f, 0.98f);
        RenderSettings.ambientEquatorColor = new Color(0.80f, 0.78f, 0.76f);
        RenderSettings.ambientGroundColor = new Color(0.48f, 0.46f, 0.44f);
        RenderSettings.ambientIntensity = 1.5f;

        var light = Object.FindFirstObjectByType<Light>();
        if (light != null)
        {
            light.type = LightType.Directional;
            // 明るく暖かい陽光。柔らかい影。
            light.color = new Color(1.0f, 0.93f, 0.78f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.5f;
            light.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
        }
    }

    // ===================================================================
    //  地面
    // ===================================================================
    private static void BuildGround()
    {
        // ユーザー提供の地面素材（Assets/Textures/*_tile.png）を優先。無ければ手続き生成。
        Texture2D grassTex = LoadTex("Assets/Textures/grass_tile.png") ?? GrassTexture();
        Texture2D dirtTex = LoadTex("Assets/Textures/dirt_tile.png") ?? DirtTexture();
        Texture2D stoneTex = LoadTex("Assets/Textures/stone_tile.png") ?? CobbleTexture();

        var grass = MakeTexturedMat("GroundGrass", grassTex, new Vector2(18f, 18f), 0.03f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(6f, 1f, 6f); // 60 x 60
        ground.GetComponent<Renderer>().sharedMaterial = grass;

        // 土の道（薄い箱を地面に重ねる）。少し幅広にして道らしく見せる。
        var roadMat = MakeTexturedMat("GroundDirt", dirtTex, new Vector2(2.5f, 20f), 0.04f);
        CreateBox("Road_Main", null, new Vector3(0f, 0.02f, 0f),
            new Vector3(4.5f, 0.04f, 40f), roadMat, collider: false);
        var roadMatCross = MakeTexturedMat("GroundDirtCross", dirtTex, new Vector2(13f, 2.5f), 0.04f);
        CreateBox("Road_Cross", null, new Vector3(0f, 0.02f, 4f),
            new Vector3(26f, 0.04f, 4.5f), roadMatCross, collider: false);
        // 店の前へ続く脇道
        var roadMatSide = MakeTexturedMat("GroundDirtSide", dirtTex, new Vector2(2f, 6f), 0.04f);
        CreateBox("Road_Side", null, new Vector3(-8.5f, 0.02f, -1.5f),
            new Vector3(3f, 0.04f, 10f), roadMatSide, collider: false);

        // 石畳（右の建物前）
        var stoneMat = MakeTexturedMat("GroundStone", stoneTex, new Vector2(3.5f, 4f), 0.12f);
        CreateBox("StonePath", null, new Vector3(8f, 0.03f, -2f),
            new Vector3(7f, 0.04f, 8f), stoneMat, collider: false);
    }

    private static Texture2D LoadTex(string path) =>
        AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    // ===================================================================
    //  街並み
    // ===================================================================
    private static void BuildTown()
    {
        var root = new GameObject("Town").transform;

        // 共有テクスチャ
        Texture2D plasterCream = PlasterTexture("PlasterCream", new Color(0.90f, 0.86f, 0.74f));
        Texture2D plasterStone = PlasterTexture("PlasterStone", new Color(0.74f, 0.76f, 0.76f));
        Texture2D brick = BrickTexture();
        Texture2D roofSlate = RoofTexture("RoofSlate", new Color(0.42f, 0.49f, 0.60f));
        Texture2D roofBrown = RoofTexture("RoofBrown", new Color(0.44f, 0.30f, 0.24f));

        var matCream = MakeTexturedMat("WallCream", plasterCream, new Vector2(2f, 2f), 0.04f);
        var matStone = MakeTexturedMat("WallStone", plasterStone, new Vector2(2f, 2f), 0.04f);
        var matBrick = MakeTexturedMat("WallBrick", brick, new Vector2(2f, 2f), 0.05f);
        var matRoofSlate = MakeTexturedMat("MatRoofSlate", roofSlate, new Vector2(3f, 1.5f), 0.05f);
        var matRoofBrown = MakeTexturedMat("MatRoofBrown", roofBrown, new Vector2(3f, 1.5f), 0.05f);

        // --- 街並みは渡された GLB モデルの建物のみで構成（正面がカメラ側を向くよう yaw 180）。
        //     モデルが無いときだけプリミティブにフォールバック。 ---
        // 左側（道の西）
        PlaceTownBuilding(root, "House_L1", "Assets/Models/shop.glb",
            new Vector3(-8.5f, 0f, 0f), 4.8f, matBrick, matRoofBrown);
        PlaceTownBuilding(root, "House_L2", "Assets/Models/house1.glb",
            new Vector3(-9f, 0f, 6.5f), 5.0f, matCream, matRoofSlate);
        PlaceTownBuilding(root, "House_L3", "Assets/Models/house2.glb",
            new Vector3(-9f, 0f, 13f), 5.0f, matStone, matRoofSlate);
        // 右側（道の東）
        PlaceTownBuilding(root, "House_R1", "Assets/Models/house2.glb",
            new Vector3(8.5f, 0f, -1f), 5.0f, matStone, matRoofSlate);
        PlaceTownBuilding(root, "House_R2", "Assets/Models/house1.glb",
            new Vector3(9f, 0f, 5.5f), 5.0f, matCream, matRoofSlate);
        PlaceTownBuilding(root, "House_R3", "Assets/Models/shop.glb",
            new Vector3(9f, 0f, 12f), 4.8f, matBrick, matRoofBrown);

        // ユーザー提供の花スプライト（背景透過済み）。無ければ発光キューブで代替。
        Sprite blueFlower = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flower_blue.png");
        Sprite whiteFlower = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/flower_white.png");

        // --- 中央の花壇（フェンス囲い）---
        BuildFlowerPlot(root, new Vector3(-5.5f, 0f, -3.5f), blueFlower, whiteFlower);

        // --- 散らばった花 ---
        if (blueFlower != null || whiteFlower != null)
        {
            ScatterFlowers(root, new Vector3(9f, 0f, 2.5f), 2.0f, 5,
                blueFlower, whiteFlower, 0.5f);
            ScatterFlowers(root, new Vector3(-10.5f, 0f, 3.5f), 2.0f, 4,
                blueFlower, whiteFlower, 0.5f);
        }
        else
        {
            var blueMat = MakeMat("BlueFlower", new Color(0.45f, 0.75f, 0.95f), 0.2f,
                emission: new Color(0.25f, 0.55f, 0.85f) * 1.5f);
            CreateBox("Flowers_Blue", root, new Vector3(-6.5f, 0.06f, -3.5f),
                new Vector3(1.6f, 0.12f, 1.6f), blueMat, collider: false);
        }

        // --- 木 ---
        BuildTree(root, new Vector3(-12f, 0f, 2f));
        BuildTree(root, new Vector3(13f, 0f, -4f));
        BuildTree(root, new Vector3(4f, 0f, 12f));
        BuildTree(root, new Vector3(-3f, 0f, -10f));

        // --- 暖かい窓明かり（建物の正面に点光源）---
        AddWindowGlow(root, new Vector3(-8f, 2.2f, 5.2f));
        AddWindowGlow(root, new Vector3(-1.5f, 2.4f, 7.2f));
        AddWindowGlow(root, new Vector3(-8.5f, 1.8f, -1.8f));
        AddWindowGlow(root, new Vector3(8.5f, 2.6f, -3.2f), 3.0f);

        // --- 道沿いの石灯籠 ---
        BuildLantern(root, new Vector3(-3.0f, 0f, -1f));
        BuildLantern(root, new Vector3(3.0f, 0f, 2f));
        BuildLantern(root, new Vector3(-3.0f, 0f, 7f));
        BuildLantern(root, new Vector3(3.0f, 0f, -5f));

        // --- 野花を追加（道脇に多めに）---
        if (blueFlower != null || whiteFlower != null)
        {
            ScatterFlowers(root, new Vector3(-6f, 0f, 1.5f), 2.6f, 6, blueFlower, whiteFlower, 0.5f);
            ScatterFlowers(root, new Vector3(6.5f, 0f, 4.5f), 2.6f, 6, blueFlower, whiteFlower, 0.5f);
            ScatterFlowers(root, new Vector3(-9f, 0f, -4f), 2.2f, 5, blueFlower, whiteFlower, 0.5f);
        }

        // --- 秋の落ち葉（地面に散乱）---
        ScatterGroundLeaves(root, 70);

        // --- 背景のランドマーク（赤い橋・石段・多層の赤屋根・看板）---
        BuildBackgroundLandmark(root);
    }

    /// <summary>
    /// GLB の建物を配置（yaw 180＝正面をカメラ側へ）。無ければプリミティブで代替。
    /// </summary>
    private static void PlaceTownBuilding(Transform parent, string name, string model,
        Vector3 pos, float width, Material wallFb, Material roofFb)
    {
        if (PlaceBuilding(parent, name, model, pos, 180f, width) == null)
            BuildHouse(parent, name, pos,
                new Vector3(width * 0.78f, 3f, width * 0.78f), wallFb, roofFb);
    }

    /// <summary>
    /// GLB（glTFast でインポート済み）の建物モデルを配置する。
    /// 指定した幅(targetWidth)になるよう等倍スケールし、底面を地面に接地させ、
    /// 通り抜け防止の BoxCollider を付ける。アセットが無ければ null を返す
    /// （呼び出し側がプリミティブにフォールバックする）。
    /// </summary>
    private static GameObject PlaceBuilding(Transform parent, string name,
        string assetPath, Vector3 groundPos, float yaw, float targetWidth)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null) return null;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (go == null) return null;
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.position = groundPos;

        // 目標の幅に合わせて等倍スケール
        if (TryGetWorldBounds(go, out Bounds b) && b.size.x > 0.0001f)
        {
            float s = targetWidth / b.size.x;
            go.transform.localScale = Vector3.one * s;
        }

        // 底面を地面（groundPos.y）へ接地
        if (TryGetWorldBounds(go, out b))
        {
            float lift = groundPos.y - b.min.y;
            go.transform.position += new Vector3(0f, lift, 0f);
        }

        // 通り抜け防止：ワールドAABBに合わせた箱コライダー（モデル本体とは別オブジェクト）
        if (TryGetWorldBounds(go, out b))
        {
            var colGo = new GameObject(name + "_Collider");
            colGo.transform.SetParent(parent);
            colGo.transform.position = b.center;
            var bc = colGo.AddComponent<BoxCollider>();
            bc.size = b.size;
        }

        return go;
    }

    /// <summary>子孫の全 Renderer を内包するワールド空間 AABB を求める。</summary>
    private static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { bounds = default; return false; }
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    private static void BuildHouse(Transform parent, string name, Vector3 pos,
        Vector3 size, Material wall, Material roof)
    {
        var go = new GameObject(name).transform;
        go.SetParent(parent);
        go.position = pos;

        // 壁（本体）
        CreateBox(name + "_Body", go,
            pos + new Vector3(0f, size.y * 0.5f, 0f), size, wall);

        // 屋根（少し大きい平たい箱）
        CreateBox(name + "_Roof", go,
            pos + new Vector3(0f, size.y + 0.35f, 0f),
            new Vector3(size.x * 1.15f, 0.7f, size.z * 1.15f), roof);

        // ドア
        CreateBox(name + "_Door", go,
            pos + new Vector3(0f, 0.7f, -size.z * 0.5f - 0.02f),
            new Vector3(0.9f, 1.4f, 0.1f),
            MakeMat("Door", new Color(0.40f, 0.27f, 0.18f), 0.1f));

        // 窓（暖色に灯る）
        var winMat = MakeMat("Window", new Color(0.95f, 0.78f, 0.45f), 0.3f,
            emission: new Color(0.9f, 0.6f, 0.28f) * 1.3f);
        float wz = -size.z * 0.5f - 0.03f;
        CreateBox(name + "_WinL", go, pos + new Vector3(-size.x * 0.27f, size.y * 0.62f, wz),
            new Vector3(0.7f, 0.7f, 0.08f), winMat, collider: false);
        CreateBox(name + "_WinR", go, pos + new Vector3(size.x * 0.27f, size.y * 0.62f, wz),
            new Vector3(0.7f, 0.7f, 0.08f), winMat, collider: false);
    }

    private static void BuildWindmill(Transform parent, Vector3 pos, Material wallMat)
    {
        var root = new GameObject("Windmill").transform;
        root.SetParent(parent);
        root.position = pos;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Windmill_Body";
        body.transform.SetParent(root);
        body.transform.position = pos + new Vector3(0f, 2.2f, 0f);
        body.transform.localScale = new Vector3(2.4f, 2.2f, 2.4f);
        body.GetComponent<Renderer>().sharedMaterial = wallMat;

        // 羽（薄い箱を十字に）
        var bladeMat = MakeMat("Blade", new Color(0.55f, 0.42f, 0.32f), 0.1f);
        var hub = new GameObject("Windmill_Blades").transform;
        hub.SetParent(root);
        hub.position = pos + new Vector3(0f, 4.6f, -1.3f);
        for (int i = 0; i < 4; i++)
        {
            var blade = CreateBox("Blade" + i, hub, hub.position,
                new Vector3(0.5f, 3.2f, 0.1f), bladeMat);
            blade.transform.RotateAround(hub.position, Vector3.forward, i * 45f);
        }
    }

    private static void BuildFlowerPlot(Transform parent, Vector3 center,
        Sprite blueFlower, Sprite whiteFlower)
    {
        var root = new GameObject("FlowerPlot").transform;
        root.SetParent(parent);
        root.position = center;

        var fenceMat = MakeTexturedMat("Fence", WoodTexture(), new Vector2(3f, 1f), 0.1f);
        float half = 2.2f;
        // 4辺のフェンス（支柱＋横木）
        for (int s = 0; s < 4; s++)
        {
            bool horizontal = s < 2;
            float sign = (s % 2 == 0) ? 1f : -1f;
            Vector3 p = horizontal
                ? center + new Vector3(0f, 0.4f, half * sign)
                : center + new Vector3(half * sign, 0.4f, 0f);
            Vector3 size = horizontal
                ? new Vector3(half * 2f, 0.5f, 0.1f)
                : new Vector3(0.1f, 0.5f, half * 2f);
            CreateBox("Fence_" + s, root, p, size, fenceMat);
        }
        // 支柱
        var postMat = MakeMat("FencePost", new Color(0.42f, 0.3f, 0.2f), 0.1f);
        Vector3[] corners =
        {
            new Vector3(half, 0.5f, half), new Vector3(-half, 0.5f, half),
            new Vector3(half, 0.5f, -half), new Vector3(-half, 0.5f, -half)
        };
        foreach (var c in corners)
            CreateBox("Post", root, center + c, new Vector3(0.2f, 0.9f, 0.2f), postMat);

        // 花壇の土（ユーザーの土タイルを使用）
        Texture2D soilTex = LoadTex("Assets/Textures/dirt_tile.png") ?? DirtTexture();
        CreateBox("PlotSoil", root, center + new Vector3(0f, 0.05f, 0f),
            new Vector3(half * 1.9f, 0.1f, half * 1.9f),
            MakeTexturedMat("PlotSoil", soilTex, new Vector2(2f, 2f), 0.04f),
            collider: false);

        // 花壇に花スプライトを配置（左を青、右を白に寄せる）。スプライトが無ければ
        // 発光キューブで代替。
        if (blueFlower != null || whiteFlower != null)
        {
            var rng = new System.Random(20260627);
            for (int i = 0; i < 22; i++)
            {
                float fx = (float)(rng.NextDouble() * 2 - 1) * (half - 0.4f);
                float fz = (float)(rng.NextDouble() * 2 - 1) * (half - 0.4f);
                bool isBlue = fx < (rng.NextDouble() - 0.5) * 0.6;
                Sprite s = isBlue ? (blueFlower ?? whiteFlower) : (whiteFlower ?? blueFlower);
                float worldH = 0.55f + (float)rng.NextDouble() * 0.25f;
                PlaceFlowerSprite(root, s, center + new Vector3(fx, 0.1f, fz), worldH);
            }
        }
        else
        {
            var blue = MakeMat("CrystalBlue", new Color(0.40f, 0.72f, 0.96f), 0.4f,
                emission: new Color(0.30f, 0.62f, 0.95f) * 2.2f);
            var rng = new System.Random(20260627);
            for (int i = 0; i < 22; i++)
            {
                float fx = (float)(rng.NextDouble() * 2 - 1) * (half - 0.4f);
                float fz = (float)(rng.NextDouble() * 2 - 1) * (half - 0.4f);
                float h = 0.2f + (float)rng.NextDouble() * 0.2f;
                CreateBox("FlowerBlue", root, center + new Vector3(fx, 0.1f + h * 0.5f, fz),
                    new Vector3(0.18f, h, 0.18f), blue, collider: false);
            }
        }
    }

    /// <summary>地面に立つ花スプライト（Y軸ビルボード）を1本置く。</summary>
    private static void PlaceFlowerSprite(Transform parent, Sprite sprite,
        Vector3 groundPos, float worldHeight)
    {
        if (sprite == null) return;
        var go = new GameObject("Flower");
        go.transform.SetParent(parent);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        float h = sprite.bounds.size.y;
        float s = (h > 0.001f) ? worldHeight / h : 1f;
        go.transform.localScale = Vector3.one * s;
        // スプライトの中心ピボット → 底面を地面に合わせるため半分持ち上げる
        go.transform.position = groundPos + new Vector3(0f, worldHeight * 0.5f, 0f);
        go.AddComponent<Billboard>();
    }

    /// <summary>指定中心の周りに花スプライトを散らす。</summary>
    private static void ScatterFlowers(Transform parent, Vector3 center, float radius,
        int count, Sprite blue, Sprite white, float baseHeight)
    {
        var rng = new System.Random(center.GetHashCode());
        for (int i = 0; i < count; i++)
        {
            float fx = (float)(rng.NextDouble() * 2 - 1) * radius;
            float fz = (float)(rng.NextDouble() * 2 - 1) * radius;
            Sprite s = (rng.NextDouble() < 0.5) ? (blue ?? white) : (white ?? blue);
            float worldH = baseHeight + (float)rng.NextDouble() * 0.25f;
            PlaceFlowerSprite(parent, s, center + new Vector3(fx, 0f, fz), worldH);
        }
    }

    // ===================================================================
    //  空気感（舞う光粒・降り注ぐ光）
    // ===================================================================
    private static void BuildAtmosphere(Camera cam)
    {
        if (cam == null) return;

        Texture2D mote = MakeMoteTexture();
        Material moteMat = MakeAdditiveMaterial("MoteMat", mote,
            new Color(1f, 0.95f, 0.78f, 1f));

        // --- 舞う光粒（記事 2.5）。カメラ前方に矩形で漂わせる ---
        var motesGo = new GameObject("LightMotes");
        motesGo.transform.SetParent(cam.transform, false);
        motesGo.transform.localPosition = new Vector3(0f, 0f, 16f); // カメラ前方
        motesGo.transform.localRotation = Quaternion.identity;
        var ps = motesGo.AddComponent<ParticleSystem>();
        ps.Stop();

        var main = ps.main;
        main.loop = true;
        main.duration = 10f;
        main.startLifetime = 9f;
        main.startSpeed = 0f;                  // 自分からは動かず、漂わせる
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = new Color(1f, 0.94f, 0.78f, 0.5f);
        main.maxParticles = 400;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 22f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 22f, 1f);

        var vel = ps.velocityOverLifetime;     // ふわふわ漂う
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.07f, 0.07f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.03f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        var col = ps.colorOverLifetime;        // フェードイン・アウト
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.material = moteMat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortingOrder = 20;
        ps.Play();

        // --- 降り注ぐ光（簡易ゴッドレイ）。画面の上端から斜めに差し込む、
        //     ごく薄い加算スジ。中央を照らすスポットにならないよう端へ寄せる。 ---
        var shaftMat = MakeAdditiveMaterial("ShaftMat", mote,
            new Color(1f, 0.93f, 0.72f, 0.05f));
        var shaftGo = new GameObject("LightShaft");
        shaftGo.transform.SetParent(cam.transform, false);
        shaftGo.transform.localPosition = new Vector3(-7f, 9f, 20f); // 画面左上の奥
        shaftGo.transform.localRotation = Quaternion.Euler(0f, 0f, 24f);
        shaftGo.transform.localScale = new Vector3(10f, 30f, 1f);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "ShaftQuad";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(shaftGo.transform, false);
        quad.GetComponent<Renderer>().sharedMaterial = shaftMat;

        // --- 舞い散る落ち葉 ---
        Texture2D leafTex = MakeLeafTexture();
        Material leafMat = MakeAlphaParticleMaterial("LeafMat", leafTex, Color.white);
        var leavesGo = new GameObject("FallingLeaves");
        leavesGo.transform.position = new Vector3(0f, 9f, 4f);
        var lp = leavesGo.AddComponent<ParticleSystem>();
        lp.Stop();
        var lmain = lp.main;
        lmain.loop = true;
        lmain.startLifetime = 12f;
        lmain.startSpeed = 0.2f;
        lmain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        lmain.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
        lmain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.45f, 0.18f), new Color(0.80f, 0.25f, 0.15f));
        lmain.gravityModifier = 0.12f;
        lmain.maxParticles = 200;
        lmain.simulationSpace = ParticleSystemSimulationSpace.World;
        var lem = lp.emission; lem.rateOverTime = 10f;
        var lsh = lp.shape;
        lsh.shapeType = ParticleSystemShapeType.Box;
        lsh.scale = new Vector3(46f, 1f, 46f);
        var lrot = lp.rotationOverLifetime; lrot.enabled = true;
        lrot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
        var lvel = lp.velocityOverLifetime; lvel.enabled = true;
        lvel.space = ParticleSystemSimulationSpace.World;
        lvel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        lvel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        var lpsr = lp.GetComponent<ParticleSystemRenderer>();
        lpsr.material = leafMat;
        lpsr.renderMode = ParticleSystemRenderMode.Billboard;
        lp.Play();
    }

    private static Texture2D MakeMoteTexture()
    {
        EnsureFolder("Assets/FX");
        string path = "Assets/FX/mote.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * a;                  // 中心が明るく外周はふんわり消える
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;    // 粒は柔らかく
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material MakeAdditiveMaterial(string key, Texture2D tex, Color color)
    {
        string path = MatFolder + "/" + key + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { existing.SetTexture("_BaseMap", tex); existing.SetColor("_BaseColor", color); return existing; }

        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Surface", 1f);           // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // 加算
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material MakeAlphaParticleMaterial(string key, Texture2D tex, Color color)
    {
        string path = MatFolder + "/" + key + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Surface", 1f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Color[] LeafPixels(int S)
    {
        var px = new Color[S * S];
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float nx = (x - c) / (S * 0.32f);
                float ny = (y - c) / (S * 0.46f);
                bool inside = nx * nx + ny * ny <= 1f;       // 縦長の葉形
                Color col = new Color(0, 0, 0, 0);
                if (inside)
                {
                    col = new Color(0.86f, 0.46f, 0.18f, 1f);
                    if (Mathf.Abs(x - c) < 0.8f) col *= 0.8f; // 中央の葉脈
                }
                px[y * S + x] = col;
            }
        return px;
    }

    private static Texture2D MakeLeafTexture()
    {
        EnsureFolder("Assets/FX");
        string path = "Assets/FX/leaf.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        const int S = 24;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.SetPixels(LeafPixels(S));
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Sprite MakeLeafSprite()
    {
        const int S = 24;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        tex.SetPixels(LeafPixels(S));
        tex.Apply();
        return WriteSprite("leaf", tex, SpriteAlignment.Center);
    }

    // 暖かい窓明かり（点光源）。窓から光が漏れる雰囲気＋Bloom のもとになる。
    private static void AddWindowGlow(Transform parent, Vector3 pos, float intensity = 2.2f)
    {
        var go = new GameObject("WindowGlow");
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1.0f, 0.78f, 0.42f);
        l.intensity = intensity;
        l.range = 8f;
        l.shadows = LightShadows.None;
    }

    // 石灯籠（道沿い）。台座＋柱＋火袋（発光）＋笠。淡い点光源付き。
    private static void BuildLantern(Transform parent, Vector3 pos)
    {
        var root = new GameObject("Lantern").transform;
        root.SetParent(parent);
        root.position = pos;
        var stone = MakeMat("LanternStone", new Color(0.62f, 0.60f, 0.55f), 0.1f);
        CreateBox("Base", root, pos + new Vector3(0f, 0.12f, 0f), new Vector3(0.5f, 0.24f, 0.5f), stone);
        CreateBox("Post", root, pos + new Vector3(0f, 0.62f, 0f), new Vector3(0.18f, 0.8f, 0.18f), stone);
        var glow = MakeMat("LanternGlow", new Color(1f, 0.82f, 0.5f), 0.2f,
            emission: new Color(1f, 0.66f, 0.32f) * 2.2f);
        CreateBox("Fire", root, pos + new Vector3(0f, 1.12f, 0f), new Vector3(0.34f, 0.34f, 0.34f), glow, collider: false);
        CreateBox("Cap", root, pos + new Vector3(0f, 1.36f, 0f), new Vector3(0.6f, 0.16f, 0.6f), stone);
        AddWindowGlow(root, pos + new Vector3(0f, 1.12f, 0f), 1.3f);
    }

    // 地面に散らばる落ち葉（フラットなスプライト）。
    private static void ScatterGroundLeaves(Transform parent, int count)
    {
        Sprite leaf = MakeLeafSprite();
        if (leaf == null) return;
        var holder = new GameObject("GroundLeaves").transform;
        holder.SetParent(parent);
        var rng = new System.Random(424242);
        Color[] tints =
        {
            new Color(0.85f, 0.45f, 0.18f), new Color(0.78f, 0.28f, 0.16f),
            new Color(0.86f, 0.62f, 0.22f), new Color(0.6f, 0.35f, 0.18f)
        };
        for (int i = 0; i < count; i++)
        {
            float x = (float)(rng.NextDouble() * 2 - 1) * 22f;
            float z = (float)(rng.NextDouble() * 2 - 1) * 18f;
            var go = new GameObject("Leaf");
            go.transform.SetParent(holder);
            go.transform.position = new Vector3(x, 0.05f, z);
            go.transform.rotation = Quaternion.Euler(90f, (float)rng.NextDouble() * 360f, 0f);
            go.transform.localScale = Vector3.one * (0.25f + (float)rng.NextDouble() * 0.18f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = leaf;
            sr.color = tints[rng.Next(tints.Length)];
        }
    }

    // 背景のランドマーク：赤い橋・石段・多層の赤屋根の建物・ポケボール看板。
    private static void BuildBackgroundLandmark(Transform parent)
    {
        var root = new GameObject("Landmark").transform;
        root.SetParent(parent);

        Texture2D stoneTex = LoadTex("Assets/Textures/stone_tile.png") ?? CobbleTexture();
        var stairMat = MakeTexturedMat("StairStone", stoneTex, new Vector2(2f, 1f), 0.1f);
        var red = MakeMat("RedPaint", new Color(0.74f, 0.16f, 0.14f), 0.15f);
        var redRoof = MakeMat("RedRoofTier", new Color(0.66f, 0.18f, 0.16f), 0.08f);
        var wall = MakeTexturedMat("WallCream",
            PlasterTexture("PlasterCream", new Color(0.90f, 0.86f, 0.74f)), new Vector2(2f, 2f), 0.04f);

        // 石段（中央奥へ上る）
        for (int i = 0; i < 10; i++)
            CreateBox("Stair" + i, root, new Vector3(0f, 0.15f + i * 0.28f, 15f + i * 0.95f),
                new Vector3(6f, 0.3f, 1.1f), stairMat);

        // 赤い橋（石段の手前）
        CreateBox("BridgeDeck", root, new Vector3(0f, 0.25f, 13f), new Vector3(4.5f, 0.2f, 2.4f),
            MakeMat("BridgeWood", new Color(0.5f, 0.34f, 0.22f), 0.1f));
        CreateBox("BridgeRailL", root, new Vector3(0f, 0.7f, 12.0f), new Vector3(4.5f, 0.7f, 0.16f), red);
        CreateBox("BridgeRailR", root, new Vector3(0f, 0.7f, 14.0f), new Vector3(4.5f, 0.7f, 0.16f), red);

        // 石段の上、霧の奥に建物（GLB モデル）。土台だけ石で作り、その上に建物を載せる。
        Vector3 b = new Vector3(2.5f, 0f, 27f);
        CreateBox("PC_Base", root, b + new Vector3(0f, 2.6f, 0f), new Vector3(9f, 5.2f, 8f), stairMat);
        if (PlaceBuilding(root, "PC_Building", "Assets/Models/house2.glb",
                b + new Vector3(0f, 5.2f, 0f), 180f, 8f) == null)
        {
            CreateBox("PC_Tier1", root, b + new Vector3(0f, 6.4f, 0f), new Vector3(7.5f, 2.4f, 6.5f), wall);
            CreateBox("PC_Roof1", root, b + new Vector3(0f, 7.9f, 0f), new Vector3(8.6f, 0.7f, 7.6f), redRoof);
        }
        AddWindowGlow(root, b + new Vector3(0f, 6.5f, -3.5f), 3.0f);

        // ポケボール看板（柱＋白板＋赤い半円）
        Vector3 sp = new Vector3(6.5f, 0f, 22f);
        CreateBox("Sign_Post", root, sp + new Vector3(0f, 1.2f, 0f), new Vector3(0.18f, 2.4f, 0.18f),
            MakeMat("SignPost", new Color(0.4f, 0.28f, 0.18f), 0.1f));
        CreateBox("Sign_BoardW", root, sp + new Vector3(0f, 2.6f, 0f), new Vector3(1.4f, 0.7f, 0.12f),
            MakeMat("SignWhite", new Color(0.95f, 0.95f, 0.95f), 0.1f));
        CreateBox("Sign_BoardR", root, sp + new Vector3(0f, 2.95f, -0.02f), new Vector3(1.4f, 0.35f, 0.13f),
            MakeMat("SignRed", new Color(0.8f, 0.18f, 0.16f), 0.1f,
                emission: new Color(0.7f, 0.15f, 0.13f)));
        CreateBox("Sign_Dot", root, sp + new Vector3(0f, 2.6f, -0.03f), new Vector3(0.22f, 0.22f, 0.14f),
            MakeMat("SignDot", new Color(0.1f, 0.1f, 0.1f), 0.1f));
    }

    private static void BuildTree(Transform parent, Vector3 pos)
    {
        var root = new GameObject("Tree").transform;
        root.SetParent(parent);
        root.position = pos;

        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(root);
        trunk.transform.position = pos + new Vector3(0f, 1f, 0f);
        trunk.transform.localScale = new Vector3(0.4f, 1f, 0.4f);
        trunk.GetComponent<Renderer>().sharedMaterial =
            MakeMat("Trunk", new Color(0.4f, 0.28f, 0.18f), 0.1f);

        // ふっくらした葉（球を3つ重ねてモコモコに）
        var foliageMat = MakeMat("Foliage", new Color(0.30f, 0.45f, 0.26f), 0.05f);
        var foliageMat2 = MakeMat("FoliageHi", new Color(0.38f, 0.54f, 0.30f), 0.05f);
        AddFoliageBlob(root, pos + new Vector3(0f, 2.6f, 0f), 2.6f, foliageMat);
        AddFoliageBlob(root, pos + new Vector3(-0.9f, 2.2f, 0.3f), 1.7f, foliageMat2);
        AddFoliageBlob(root, pos + new Vector3(0.9f, 2.3f, -0.2f), 1.7f, foliageMat2);
    }

    private static void AddFoliageBlob(Transform parent, Vector3 pos, float diameter, Material mat)
    {
        var foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foliage.name = "Foliage";
        foliage.transform.SetParent(parent);
        foliage.transform.position = pos;
        foliage.transform.localScale = Vector3.one * diameter;
        foliage.GetComponent<Renderer>().sharedMaterial = mat;
        Object.DestroyImmediate(foliage.GetComponent<Collider>());
    }

    // ===================================================================
    //  プレイヤー
    // ===================================================================
    private static GameObject BuildPlayer()
    {
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 0.9f, -4f);

        // 物理
        var rb = player.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation |
                         RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        var box = player.AddComponent<BoxCollider>();
        box.size = new Vector3(0.7f, 1.6f, 0.7f);
        box.center = new Vector3(0f, 0f, 0f);

        player.AddComponent<PlayerController>();

        // 接地影（地面に寝かせた柔らかい円スプライト）
        var shadowGo = new GameObject("Shadow");
        shadowGo.transform.SetParent(player.transform);
        shadowGo.transform.localPosition = new Vector3(0f, -0.88f, 0f);
        shadowGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 地面に水平
        shadowGo.transform.localScale = Vector3.one * 1.6f;
        var shadowSr = shadowGo.AddComponent<SpriteRenderer>();
        shadowSr.sprite = GenerateBlobShadowSprite();
        shadowSr.color = new Color(0f, 0f, 0f, 0.35f);
        shadowSr.sortingOrder = -1; // 常にキャラの背面へ

        // スプライト（子オブジェクト・ビルボード）
        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(player.transform);
        spriteGo.transform.localPosition = new Vector3(0f, -0.9f, 0f); // 足元を地面へ
        var sr = spriteGo.AddComponent<SpriteRenderer>();
        // SpriteRenderer の既定スプライトマテリアルを使用する。
        // （スプライトのテクスチャを正しく表示し、不透明物に対し深度テストも行う。
        //  URP/Lit を割り当てると _BaseMap が空でキャラが真っ白になるため使わない。）

        // ユーザー提供のスプライトシートがあれば、自動スライスして
        // 向き別の歩行アニメーションに使う。無ければ生成キャラにフォールバック。
        var rows = PlayerSheetSlicer.EnsureSlicedRows(SpriteFolder + "/player_sheet.png");
        if (rows != null && rows.Count > 0 && rows[0].Length > 0)
        {
            Sprite first = rows[0][0];
            sr.sprite = first;
            var anim = spriteGo.AddComponent<PlayerSpriteAnimator>();
            anim.Setup(rows);
            // キャラの足元から頭まで約 2.0 ユニットになるよう等倍スケール
            float hUnits = first.bounds.size.y;
            float scale = (hUnits > 0.001f) ? 2.0f / hUnits : 2.3f;
            spriteGo.transform.localScale = Vector3.one * scale;
        }
        else
        {
            sr.sprite = GenerateCharacterSprite();
            spriteGo.transform.localScale = Vector3.one * 2.3f;
        }

        spriteGo.AddComponent<Billboard>();

        return player;
    }

    // ===================================================================
    //  カメラ
    // ===================================================================
    private static void ConfigureCamera(Transform target)
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
        }

        cam.transform.position = new Vector3(0f, 9f, -12f);
        cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        cam.fieldOfView = 34f; // 望遠寄りで HD-2D の圧縮感を出す
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.93f, 0.82f, 0.66f); // 暖色の空（霧色と一致）

        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;

        var follow = cam.gameObject.AddComponent<CameraController>();
        follow.target = target;
        follow.useStartOffset = true;
        follow.smoothing = 6f;
    }

    // ===================================================================
    //  ポストプロセス（Global Volume）— HD-2D の肝になるカラーグレード
    // ===================================================================
    private static void BuildGlobalVolume()
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        // フィルム調トーン（HD-2D の濃い階調の土台）
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        // 光の溢れ（ハイライトだけ柔らかくにじむ程度に）
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.3f);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.75f);
        bloom.tint.Override(new Color(1f, 0.96f, 0.88f));

        // ティルトシフト風の強い被写界深度（記事: Aperture 最小・Focal 大）。
        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(12f);
        dof.focalLength.Override(145f);
        dof.aperture.Override(1.4f);       // 最小絞り＝極浅い被写界深度

        // ホワイトバランス（記事: Temperature やや暖色 / Tint 少し緑）
        var wb = profile.Add<WhiteBalance>(true);
        wb.temperature.Override(11f);
        wb.tint.Override(-4f);

        // 暖色のホワイトバランス（オクトパスの黄金色）
        // 色調整：明るく・暖かく・鮮やかに（中間を暗くしない）
        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.45f);   // 全体を明るく
        ca.contrast.Override(10f);
        ca.hueShift.Override(-3f);
        ca.saturation.Override(24f);       // 鮮やかに
        ca.colorFilter.Override(new Color(1.0f, 0.98f, 0.92f));

        // Lift / Gamma / Gain：暗部を持ち上げ・中間も少し明るく（締めすぎない）
        var lgg = profile.Add<LiftGammaGain>(true);
        lgg.lift.Override(new Vector4(1f, 1f, 1.02f, 0.05f));
        lgg.gamma.Override(new Vector4(1f, 1f, 1f, 0.05f));
        lgg.gain.Override(new Vector4(1f, 1f, 1f, 0.03f));

        // スプリットトーン：影をわずかに寒色、ハイライトを暖色に（控えめ）
        var split = profile.Add<SplitToning>(true);
        split.shadows.Override(new Color(0.40f, 0.50f, 0.60f));
        split.highlights.Override(new Color(1.0f, 0.82f, 0.55f));
        split.balance.Override(0f);

        // ビネット（端をほんのり落とす程度に・軽め）
        var vig = profile.Add<Vignette>(true);
        vig.color.Override(new Color(0.06f, 0.05f, 0.05f));
        vig.intensity.Override(0.2f);
        vig.smoothness.Override(0.7f);
        vig.rounded.Override(true);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var volGo = new GameObject("Global Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.sharedProfile = profile;
    }

    // ===================================================================
    //  マテリアル・ヘルパー
    // ===================================================================
    private static GameObject CreateBox(string name, Transform parent,
        Vector3 worldPos, Vector3 size, Material mat, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent);
        go.transform.position = worldPos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
        }
        return go;
    }

    private static Material MakeMat(string key, Color color, float smoothness,
        Color? emission = null)
    {
        string path = MatFolder + "/" + key + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);

        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor", emission.Value);
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material MakeTexturedMat(string key, Texture2D tex, Vector2 tiling,
        float smoothness)
    {
        string path = MatFolder + "/" + key + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", tiling);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ===================================================================
    //  手続き生成テクスチャ（ピクセルアート）
    // ===================================================================
    private static Texture2D GrassTexture()
    {
        return MakeTexture("grass", 64, (x, y, rng) =>
        {
            float n = (float)rng.NextDouble();
            // みずみずしい深緑〜明るい緑
            Color baseCol = Color.Lerp(
                new Color(0.26f, 0.46f, 0.24f), new Color(0.40f, 0.62f, 0.32f), n);
            // 濃い草の束（影になった草むら）
            if (rng.NextDouble() < 0.10)
                baseCol *= 0.74f;
            // ハイライトの草先
            else if (rng.NextDouble() < 0.06)
                baseCol = Color.Lerp(baseCol, new Color(0.55f, 0.74f, 0.40f), 0.6f);
            // 小さな白/青の花のドット
            if (rng.NextDouble() < 0.012)
                baseCol = (rng.NextDouble() < 0.5)
                    ? new Color(0.92f, 0.95f, 0.95f)
                    : new Color(0.55f, 0.80f, 0.95f);
            return baseCol;
        });
    }

    private static Texture2D DirtTexture()
    {
        return MakeTexture("dirt", 64, (x, y, rng) =>
        {
            float n = (float)rng.NextDouble();
            Color baseCol = Color.Lerp(
                new Color(0.55f, 0.44f, 0.30f), new Color(0.66f, 0.54f, 0.38f), n);
            if (rng.NextDouble() < 0.05) baseCol *= 0.78f; // 小石・轍
            return baseCol;
        });
    }

    private static Texture2D CobbleTexture()
    {
        return MakeTexture("cobble", 64, (x, y, rng) =>
        {
            int cx = x / 16, cy = y / 16;
            // セルごとに色を固定したいので、座標から擬似乱数を生成
            float h = Frac(Mathf.Sin(cx * 12.9898f + cy * 78.233f) * 43758.5453f);
            Color stone = Color.Lerp(
                new Color(0.62f, 0.62f, 0.60f), new Color(0.80f, 0.79f, 0.74f), h);
            // 目地（溝）
            if (x % 16 < 2 || y % 16 < 2)
                stone = new Color(0.34f, 0.33f, 0.32f);
            stone *= 0.92f + 0.08f * (float)rng.NextDouble();
            return stone;
        });
    }

    private static Texture2D BrickTexture()
    {
        return MakeTexture("brick", 64, (x, y, rng) =>
        {
            int row = y / 16;
            int offset = (row % 2 == 0) ? 0 : 16;
            int bx = (x + offset) % 32;
            // モルタル目地
            if (y % 16 < 2 || bx < 2)
                return new Color(0.78f, 0.74f, 0.66f);
            float h = (float)rng.NextDouble();
            return Color.Lerp(
                new Color(0.62f, 0.34f, 0.27f), new Color(0.74f, 0.42f, 0.33f), h);
        });
    }

    private static Texture2D PlasterTexture(string key, Color tint)
    {
        return MakeTexture(key.ToLowerInvariant(), 64, (x, y, rng) =>
        {
            float n = ((float)rng.NextDouble() - 0.5f) * 0.10f;
            Color c = tint + new Color(n, n, n);
            // ほのかな横木のライン（木組み風）
            if (y % 32 == 0) c *= 0.9f;
            return c;
        });
    }

    private static Texture2D RoofTexture(string key, Color tint)
    {
        return MakeTexture(key.ToLowerInvariant(), 64, (x, y, rng) =>
        {
            int rowH = 12;
            int yy = y % rowH;
            // 瓦の段：上側を濃く、下端をハイライト
            float shade = Mathf.Lerp(0.78f, 1.08f, yy / (float)rowH);
            int seamOffset = ((y / rowH) % 2 == 0) ? 0 : 8;
            Color c = tint * shade;
            if ((x + seamOffset) % 16 < 2) c *= 0.7f;     // 瓦の縦の継ぎ目
            if (yy < 1) c *= 0.6f;                          // 段の影
            c *= 0.96f + 0.04f * (float)rng.NextDouble();
            return c;
        });
    }

    private static Texture2D WoodTexture()
    {
        return MakeTexture("wood", 64, (x, y, rng) =>
        {
            float grain = Mathf.Sin(x * 0.6f) * 0.5f + 0.5f;
            Color c = Color.Lerp(
                new Color(0.46f, 0.33f, 0.21f), new Color(0.58f, 0.43f, 0.28f), grain);
            c *= 0.95f + 0.05f * (float)rng.NextDouble();
            return c;
        });
    }

    /// <summary>共通テクスチャ生成：Repeat/Point/無圧縮で取り込み Texture2D を返す。</summary>
    private static Texture2D MakeTexture(string key, int size,
        System.Func<int, int, System.Random, Color> fn)
    {
        string path = TexFolder + "/" + key + ".png";

        var rng = new System.Random(key.GetHashCode());
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = fn(x, y, rng);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();

        string abs = Path.Combine(Directory.GetCurrentDirectory(), path);
        File.WriteAllBytes(abs, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.filterMode = FilterMode.Point;     // くっきりしたドット
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = true;              // 遠景のチラつき防止
        ti.sRGBTexture = true;
        ti.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static float Frac(float v) => v - Mathf.Floor(v);

    // ===================================================================
    //  キャラクター・スプライト生成
    // ===================================================================
    /// <summary>
    /// 主人公（赤キャップの旅人）のドット絵スプライトを生成して返す。
    /// アウトラインと陰影付きで、24x32 解像度。
    /// </summary>
    private static Sprite GenerateCharacterSprite()
    {
        const int W = 24, H = 32;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                tex.SetPixel(x, y, clear);

        Color cap = new Color(0.84f, 0.22f, 0.20f);
        Color capDark = new Color(0.62f, 0.15f, 0.15f);
        Color skin = new Color(0.96f, 0.80f, 0.62f);
        Color jacket = new Color(0.30f, 0.55f, 0.78f);   // 青いマント風の上着
        Color jacketDark = new Color(0.22f, 0.42f, 0.62f);
        Color pack = new Color(0.78f, 0.55f, 0.30f);
        Color pants = new Color(0.30f, 0.30f, 0.36f);
        Color shoe = new Color(0.16f, 0.13f, 0.12f);
        Color eye = new Color(0.15f, 0.12f, 0.18f);

        // y=0 が足元。上に積み上げる。
        FillRect(tex, 7, 0, 11, 3, shoe);        // 左足
        FillRect(tex, 13, 0, 17, 3, shoe);       // 右足
        FillRect(tex, 7, 3, 11, 7, pants);       // 左脚
        FillRect(tex, 13, 3, 17, 7, pants);      // 右脚
        FillRect(tex, 7, 6, 17, 12, pants);      // 腰
        FillRect(tex, 6, 12, 18, 21, jacket);    // 胴（上着）
        FillRect(tex, 5, 13, 7, 19, jacket);     // 左腕
        FillRect(tex, 17, 13, 19, 19, jacket);   // 右腕
        FillRect(tex, 16, 13, 20, 20, pack);     // 背負い袋（右側）
        FillRect(tex, 10, 21, 14, 23, skin);     // 首
        FillRect(tex, 8, 22, 16, 28, skin);      // 顔
        FillRect(tex, 7, 27, 17, 29, cap);       // つば
        FillRect(tex, 8, 28, 16, 32, cap);       // 帽子本体
        FillRect(tex, 8, 27, 16, 28, capDark);   // 帽子の影
        FillRect(tex, 10, 25, 11, 26, eye);      // 左目
        FillRect(tex, 13, 25, 14, 26, eye);      // 右目

        // 簡単な陰影：右側（x>=13）を少し暗く
        Shade(tex, 13, 0, W, H, 0.86f);
        // 上着の縦ライン（前合わせ）
        FillRect(tex, 11, 12, 13, 21, jacketDark);

        // 1px ダークアウトライン
        AddOutline(tex, new Color(0.10f, 0.08f, 0.12f, 1f));

        tex.Apply();
        return WriteSprite("player", tex, SpriteAlignment.BottomCenter);
    }

    /// <summary>足元に敷く柔らかい円形の接地影スプライト。</summary>
    private static Sprite GenerateBlobShadowSprite()
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        float c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a; // 中心が濃く外周がふんわり消える
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
            }
        tex.Apply();
        return WriteSprite("blob_shadow", tex, SpriteAlignment.Center);
    }

    private static Sprite WriteSprite(string key, Texture2D tex, SpriteAlignment align)
    {
        string path = SpriteFolder + "/" + key + ".png";
        string abs = Path.Combine(Directory.GetCurrentDirectory(), path);
        File.WriteAllBytes(abs, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // PixelSpriteImportSettings が /Sprites/ を Point/Sprite に強制するが、
        // alignment だけここで上書きする。
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.spritePixelsPerUnit = 32f;

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)align;
        ti.SetTextureSettings(settings);
        ti.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static void FillRect(Texture2D tex, int x0, int y0, int x1, int y1, Color c)
    {
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                    tex.SetPixel(x, y, c);
    }

    /// <summary>指定範囲の不透明ピクセルを暗くする（簡易陰影）。</summary>
    private static void Shade(Texture2D tex, int x0, int y0, int x1, int y1, float mul)
    {
        for (int y = y0; y < y1 && y < tex.height; y++)
            for (int x = x0; x < x1 && x < tex.width; x++)
            {
                Color c = tex.GetPixel(x, y);
                if (c.a > 0.5f)
                    tex.SetPixel(x, y, new Color(c.r * mul, c.g * mul, c.b * mul, c.a));
            }
    }

    /// <summary>透明ピクセルのうち、不透明ピクセルに隣接する箇所をアウトライン色で塗る。</summary>
    private static void AddOutline(Texture2D tex, Color outline)
    {
        int w = tex.width, h = tex.height;
        var src = tex.GetPixels();
        bool Opaque(int x, int y)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) return false;
            return src[y * w + x].a > 0.5f;
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (src[y * w + x].a > 0.5f) continue;
                if (Opaque(x - 1, y) || Opaque(x + 1, y) ||
                    Opaque(x, y - 1) || Opaque(x, y + 1))
                    tex.SetPixel(x, y, outline);
            }
    }

    // ===================================================================
    //  汎用ヘルパー
    // ===================================================================
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void AddSceneToBuildSettings(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);
        foreach (var s in scenes)
            if (s.path == path) return;
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
