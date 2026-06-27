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
        // 参考画像のような、霧に包まれた緑がかった森の朝の空気感。
        // 近めで濃いソフトな霧をかけ、遠景を白緑に溶かす。
        Color skyColor = new Color(0.84f, 0.90f, 0.84f); // 霧の白緑
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = skyColor;
        RenderSettings.fogStartDistance = 10f;
        RenderSettings.fogEndDistance = 52f;

        // 三色アンビエント：空は明るい白緑、地面は深い緑の影色。
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.86f, 0.92f, 0.84f);
        RenderSettings.ambientEquatorColor = new Color(0.58f, 0.66f, 0.56f);
        RenderSettings.ambientGroundColor = new Color(0.20f, 0.26f, 0.20f);

        var light = Object.FindFirstObjectByType<Light>();
        if (light != null)
        {
            light.type = LightType.Directional;
            // 霧を通した柔らかい白い陽光（やや緑寄り）。
            light.color = new Color(0.96f, 0.98f, 0.92f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.4f;           // 境界の曖昧な柔らかい影
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }

    // ===================================================================
    //  地面
    // ===================================================================
    private static void BuildGround()
    {
        var grass = MakeTexturedMat("Grass", GrassTexture(), new Vector2(20f, 20f), 0.03f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(6f, 1f, 6f); // 60 x 60
        ground.GetComponent<Renderer>().sharedMaterial = grass;

        // 土の道（薄い箱を地面に重ねる）
        var dirtTex = DirtTexture();
        var roadMat = MakeTexturedMat("DirtRoad", dirtTex, new Vector2(2f, 18f), 0.04f);
        CreateBox("Road_Main", null, new Vector3(0f, 0.02f, 0f),
            new Vector3(3.5f, 0.04f, 36f), roadMat, collider: false);
        var roadMatCross = MakeTexturedMat("DirtRoadCross", dirtTex, new Vector2(11f, 2f), 0.04f);
        CreateBox("Road_Cross", null, new Vector3(0f, 0.02f, 4f),
            new Vector3(22f, 0.04f, 3.5f), roadMatCross, collider: false);

        // 石畳（右の建物前）
        var stoneMat = MakeTexturedMat("StonePath", CobbleTexture(), new Vector2(3f, 3f), 0.12f);
        CreateBox("StonePath", null, new Vector3(8f, 0.03f, -2f),
            new Vector3(7f, 0.04f, 8f), stoneMat, collider: false);
    }

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

        // --- 家 A（左奥）---
        BuildHouse(root, "TownHouseA", new Vector3(-8f, 0f, 7f),
            new Vector3(4f, 3f, 4f), matCream, matRoofSlate);

        // --- 家 B（中央奥）---
        BuildHouse(root, "TownHouseB", new Vector3(-1.5f, 0f, 9f),
            new Vector3(4f, 3.2f, 4f), matStone, matRoofSlate);

        // --- 店（左手前）---
        BuildHouse(root, "Store", new Vector3(-8.5f, 0f, 0f),
            new Vector3(3.6f, 2.6f, 3.6f), matBrick, matRoofBrown);
        // 店のひさし
        CreateBox("Store_Awning", root, new Vector3(-8.5f, 2.0f, -1.9f),
            new Vector3(3.8f, 0.25f, 1.2f),
            MakeMat("Awning", new Color(0.8f, 0.32f, 0.3f), 0.1f));

        // --- 右の大きな建物（教会風・アーチ）---
        BuildHouse(root, "ChapelHouse", new Vector3(8.5f, 0f, -1f),
            new Vector3(5f, 4f, 5.5f), matStone, matRoofSlate);
        // アーチ（円柱を横倒し）
        var arch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arch.name = "Arch";
        arch.transform.SetParent(root);
        arch.transform.position = new Vector3(6.0f, 2.2f, -3.2f);
        arch.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        arch.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
        arch.GetComponent<Renderer>().sharedMaterial = matBrick;

        // --- 風車（右奥）---
        BuildWindmill(root, new Vector3(11.5f, 0f, 8f), matCream);

        // --- 中央の花壇（フェンス囲い）---
        BuildFlowerPlot(root, new Vector3(-5.5f, 0f, -3.5f));

        // --- 散らばった花（青いクリスタル花 / 白い花）---
        var blueFlower = MakeMat("BlueFlower", new Color(0.45f, 0.75f, 0.95f), 0.2f,
            emission: new Color(0.25f, 0.55f, 0.85f) * 1.5f);
        var whiteFlower = MakeMat("WhiteFlower", new Color(0.95f, 0.97f, 0.98f), 0.2f,
            emission: new Color(0.5f, 0.55f, 0.6f));
        CreateBox("Flowers_Blue", root, new Vector3(-6.5f, 0.06f, -3.5f),
            new Vector3(1.6f, 0.12f, 1.6f), blueFlower, collider: false);
        CreateBox("Flowers_White", root, new Vector3(-4.5f, 0.06f, -3.5f),
            new Vector3(1.6f, 0.12f, 1.6f), whiteFlower, collider: false);
        CreateBox("Flowers_White2", root, new Vector3(9f, 0.06f, 2.5f),
            new Vector3(1.4f, 0.12f, 1.2f), whiteFlower, collider: false);

        // --- 木 ---
        BuildTree(root, new Vector3(-12f, 0f, 2f));
        BuildTree(root, new Vector3(13f, 0f, -4f));
        BuildTree(root, new Vector3(4f, 0f, 12f));
        BuildTree(root, new Vector3(-3f, 0f, -10f));
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

    private static void BuildFlowerPlot(Transform parent, Vector3 center)
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

        // 花壇の土
        CreateBox("PlotSoil", root, center + new Vector3(0f, 0.05f, 0f),
            new Vector3(half * 1.9f, 0.1f, half * 1.9f),
            MakeTexturedMat("PlotSoil", DirtTexture(), new Vector2(2f, 2f), 0.04f),
            collider: false);

        // 参考画像のような、青いクリスタル花と白い花を多数自然に配置。
        var blue = MakeMat("CrystalBlue", new Color(0.40f, 0.72f, 0.96f), 0.4f,
            emission: new Color(0.30f, 0.62f, 0.95f) * 2.2f);
        var white = MakeMat("FlowerWhite", new Color(0.95f, 0.97f, 0.98f), 0.3f,
            emission: new Color(0.55f, 0.62f, 0.66f) * 1.2f);
        var rng = new System.Random(20260627);
        // 左半分を青、右半分を白に寄せつつ、ばらつかせる。
        for (int i = 0; i < 26; i++)
        {
            float fx = (float)(rng.NextDouble() * 2 - 1) * (half - 0.4f);
            float fz = (float)(rng.NextDouble() * 2 - 1) * (half - 0.4f);
            bool isBlue = fx < (rng.NextDouble() - 0.5) * 0.6;
            float h = 0.18f + (float)rng.NextDouble() * 0.22f;
            CreateBox(isBlue ? "FlowerBlue" : "FlowerWhite", root,
                center + new Vector3(fx, 0.1f + h * 0.5f, fz),
                new Vector3(0.18f, h, 0.18f),
                isBlue ? blue : white, collider: false);
        }
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
        Sprite sprite = GenerateCharacterSprite();
        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(player.transform);
        spriteGo.transform.localPosition = new Vector3(0f, -0.9f, 0f); // 足元を地面へ
        spriteGo.transform.localScale = Vector3.one * 2.3f;
        var sr = spriteGo.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        // SpriteRenderer の既定スプライトマテリアルを使用する。
        // （スプライトのテクスチャを正しく表示し、不透明物に対し深度テストも行う。
        //  URP/Lit を割り当てると _BaseMap が空でキャラが真っ白になるため使わない。）
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
        cam.backgroundColor = new Color(0.84f, 0.90f, 0.84f); // 霧の白緑（霧色と一致）

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

        // 光の溢れ（参考画像のような、画面全体が光の霧に溶けるソフト発光）
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(3.8f);   // 大幅に強く
        bloom.threshold.Override(0.7f);   // 明るい部分が広くボケて輝く
        bloom.scatter.Override(0.85f);    // 光が大きく広がる
        bloom.tint.Override(new Color(0.95f, 1f, 0.96f)); // やや緑寄りの白

        // 背景ボケ（ミニチュア感・背景を大きく自然にボカす）
        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(13f);  // プレイヤー付近にピント
        dof.focalLength.Override(125f);   // 背景を大きくボカす
        dof.aperture.Override(2.0f);      // 絞り開放＝浅い被写界深度

        // フィルム調のトーン（白飛び・黒つぶれを抑え、立体的な階調に）
        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        // 緑がかった涼やかなホワイトバランス＝霧の朝
        var wb = profile.Add<WhiteBalance>(true);
        wb.temperature.Override(-8f);
        wb.tint.Override(10f);            // 緑方向へ

        // 霧の空気に合わせ、彩度はやや控えめ・緑のフィルタ
        var ca = profile.Add<ColorAdjustments>(true);
        ca.postExposure.Override(0.18f);
        ca.contrast.Override(8f);
        ca.saturation.Override(4f);
        ca.colorFilter.Override(new Color(0.92f, 1.0f, 0.95f));

        // 四隅を落として中央（プレイヤー）へ視線を集める
        var vig = profile.Add<Vignette>(true);
        vig.color.Override(new Color(0.06f, 0.09f, 0.07f));
        vig.intensity.Override(0.28f);
        vig.smoothness.Override(0.6f);
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
