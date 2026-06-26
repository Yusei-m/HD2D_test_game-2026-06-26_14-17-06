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
///   - 地面・建物・フェンス・花壇・木などの 3D 環境
///   - SpriteRenderer + Rigidbody + BoxCollider を備えたプレイヤー
///   - 俯瞰アイソメトリック視点のカメラ（追従付き）
///   - Bloom / Depth of Field を含む URP Global Volume
///   - ドット絵用のテクスチャインポート設定
/// をすべて生成し、即プレイ可能な状態で保存する。
/// </summary>
public static class HD2DSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/HD2D_Town.unity";
    private const string MatFolder = "Assets/Materials";
    private const string SpriteFolder = "Assets/Sprites";
    private const string ProfilePath = "Assets/Settings/HD2D_VolumeProfile.asset";

    [MenuItem("Tools/HD2D/Build Walking Game Scene")]
    public static void Build()
    {
        EnsureFolder(MatFolder);
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
    //  環境（霧・ライト・アンビエント）
    // ===================================================================
    private static void ConfigureEnvironment()
    {
        // 背景のボケと空気感のための霧
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.82f, 0.87f, 0.83f);
        RenderSettings.fogStartDistance = 14f;
        RenderSettings.fogEndDistance = 60f;

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.62f, 0.66f, 0.62f);

        var light = Object.FindFirstObjectByType<Light>();
        if (light != null)
        {
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.86f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }
    }

    // ===================================================================
    //  地面
    // ===================================================================
    private static void BuildGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(6f, 1f, 6f); // 60 x 60
        ground.GetComponent<Renderer>().sharedMaterial =
            MakeMat("Grass", new Color(0.42f, 0.55f, 0.34f), 0.05f);

        // 土の道（薄い箱を地面に重ねる）
        var roadMat = MakeMat("DirtRoad", new Color(0.62f, 0.52f, 0.36f), 0.05f);
        CreateBox("Road_Main", null, new Vector3(0f, 0.02f, 0f),
            new Vector3(3.5f, 0.04f, 36f), roadMat, collider: false);
        CreateBox("Road_Cross", null, new Vector3(0f, 0.02f, 4f),
            new Vector3(22f, 0.04f, 3.5f), roadMat, collider: false);

        // 石畳（右の建物前）
        var stoneMat = MakeMat("StonePath", new Color(0.74f, 0.74f, 0.7f), 0.1f);
        CreateBox("StonePath", null, new Vector3(8f, 0.03f, -2f),
            new Vector3(7f, 0.04f, 8f), stoneMat, collider: false);
    }

    // ===================================================================
    //  街並み
    // ===================================================================
    private static void BuildTown()
    {
        var root = new GameObject("Town").transform;

        Color wallCream = new Color(0.90f, 0.88f, 0.80f);
        Color wallStone = new Color(0.70f, 0.74f, 0.76f);
        Color roofSlate = new Color(0.45f, 0.52f, 0.62f);
        Color roofBrown = new Color(0.40f, 0.30f, 0.24f);

        // --- 家 A（左奥）---
        BuildHouse(root, "TownHouseA", new Vector3(-8f, 0f, 7f),
            new Vector3(4f, 3f, 4f), wallCream, roofSlate);

        // --- 家 B（中央奥）---
        BuildHouse(root, "TownHouseB", new Vector3(-1.5f, 0f, 9f),
            new Vector3(4f, 3.2f, 4f), wallStone, roofSlate);

        // --- 店（左手前）---
        BuildHouse(root, "Store", new Vector3(-8.5f, 0f, 0f),
            new Vector3(3.6f, 2.6f, 3.6f), wallCream, roofBrown);
        // 店のひさし
        CreateBox("Store_Awning", root, new Vector3(-8.5f, 2.0f, -1.9f),
            new Vector3(3.8f, 0.25f, 1.2f),
            MakeMat("Awning", new Color(0.8f, 0.32f, 0.3f), 0.1f));

        // --- 右の大きな建物（教会風・アーチ）---
        BuildHouse(root, "ChapelHouse", new Vector3(8.5f, 0f, -1f),
            new Vector3(5f, 4f, 5.5f), wallStone, roofSlate);
        // アーチ（円柱を横倒し）
        var arch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arch.name = "Arch";
        arch.transform.SetParent(root);
        arch.transform.position = new Vector3(6.0f, 2.2f, -3.2f);
        arch.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        arch.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
        arch.GetComponent<Renderer>().sharedMaterial =
            MakeMat("ArchStone", new Color(0.66f, 0.62f, 0.55f), 0.1f);

        // --- 風車（右奥）---
        BuildWindmill(root, new Vector3(11.5f, 0f, 8f));

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
        Vector3 size, Color wall, Color roof)
    {
        var go = new GameObject(name).transform;
        go.SetParent(parent);
        go.position = pos;

        // 壁（本体）
        CreateBox(name + "_Body", go,
            pos + new Vector3(0f, size.y * 0.5f, 0f), size,
            MakeMat("Wall_" + ColorKey(wall), wall, 0.05f));

        // 屋根（少し大きい平たい箱）
        CreateBox(name + "_Roof", go,
            pos + new Vector3(0f, size.y + 0.35f, 0f),
            new Vector3(size.x * 1.15f, 0.7f, size.z * 1.15f),
            MakeMat("Roof_" + ColorKey(roof), roof, 0.05f));

        // ドア
        CreateBox(name + "_Door", go,
            pos + new Vector3(0f, 0.7f, -size.z * 0.5f - 0.02f),
            new Vector3(0.9f, 1.4f, 0.1f),
            MakeMat("Door", new Color(0.45f, 0.32f, 0.22f), 0.1f));
    }

    private static void BuildWindmill(Transform parent, Vector3 pos)
    {
        var root = new GameObject("Windmill").transform;
        root.SetParent(parent);
        root.position = pos;

        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Windmill_Body";
        body.transform.SetParent(root);
        body.transform.position = pos + new Vector3(0f, 2.2f, 0f);
        body.transform.localScale = new Vector3(2.4f, 2.2f, 2.4f);
        body.GetComponent<Renderer>().sharedMaterial =
            MakeMat("Wall_windmill", new Color(0.88f, 0.85f, 0.78f), 0.05f);

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

        var fenceMat = MakeMat("Fence", new Color(0.5f, 0.38f, 0.26f), 0.1f);
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

        var foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foliage.name = "Foliage";
        foliage.transform.SetParent(root);
        foliage.transform.position = pos + new Vector3(0f, 2.6f, 0f);
        foliage.transform.localScale = new Vector3(2.4f, 2.8f, 2.4f);
        foliage.GetComponent<Renderer>().sharedMaterial =
            MakeMat("Foliage", new Color(0.30f, 0.45f, 0.26f), 0.05f);
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
        cam.backgroundColor = new Color(0.80f, 0.85f, 0.82f);

        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;

        var follow = cam.gameObject.AddComponent<CameraController>();
        follow.target = target;
        follow.useStartOffset = true;
        follow.smoothing = 6f;
    }

    // ===================================================================
    //  ポストプロセス（Global Volume）
    // ===================================================================
    private static void BuildGlobalVolume()
    {
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ProfilePath);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.3f);
        bloom.threshold.Override(0.85f);
        bloom.scatter.Override(0.78f);
        bloom.tint.Override(new Color(1f, 0.98f, 0.92f));

        var dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(13f);  // プレイヤー付近にピント
        dof.focalLength.Override(110f);   // 背景を大きくボカす
        dof.aperture.Override(5.6f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        var volGo = new GameObject("Global Volume");
        var vol = volGo.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 1f;
        vol.sharedProfile = profile;
    }

    // ===================================================================
    //  ヘルパー
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

    /// <summary>
    /// 主人公（赤キャップのトレーナー風）のドット絵スプライトを
    /// プログラムで生成し、Point/None 設定でインポートして返す。
    /// </summary>
    private static Sprite GenerateCharacterSprite()
    {
        string path = SpriteFolder + "/player.png";

        const int W = 16, H = 24;
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
                tex.SetPixel(x, y, clear);

        Color cap = new Color(0.83f, 0.20f, 0.20f);
        Color skin = new Color(0.96f, 0.80f, 0.62f);
        Color jacket = new Color(0.88f, 0.88f, 0.92f);
        Color pack = new Color(0.80f, 0.30f, 0.28f);
        Color pants = new Color(0.20f, 0.26f, 0.45f);
        Color shoe = new Color(0.15f, 0.15f, 0.18f);

        // y=0 が足元。上に向かって積み上げる。
        FillRect(tex, 4, 0, 7, 2, shoe);      // 左足
        FillRect(tex, 8, 0, 11, 2, shoe);     // 右足
        FillRect(tex, 4, 2, 11, 8, pants);    // ズボン
        FillRect(tex, 3, 8, 12, 15, jacket);  // 上着
        FillRect(tex, 11, 9, 13, 14, pack);   // バックパック
        FillRect(tex, 5, 15, 10, 19, skin);   // 顔
        FillRect(tex, 4, 19, 11, 21, cap);    // キャップ本体
        FillRect(tex, 4, 18, 12, 19, cap);    // つば
        FillRect(tex, 5, 21, 10, 23, cap);    // キャップ上部

        tex.Apply();

        string abs = Path.Combine(Directory.GetCurrentDirectory(), path);
        File.WriteAllBytes(abs, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.spritePixelsPerUnit = 32f;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
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

    private static string ColorKey(Color c)
    {
        return Mathf.RoundToInt(c.r * 255) + "_" +
               Mathf.RoundToInt(c.g * 255) + "_" +
               Mathf.RoundToInt(c.b * 255);
    }
}
