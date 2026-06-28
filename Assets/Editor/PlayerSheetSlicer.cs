using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// AI生成のキャラクターシートのように、各コマが等間隔グリッドに乗っておらず、
/// さらに背景が「透明」ではなく市松模様のグレーで描き込まれているシートを、
///   1) 背景グレーを縁からの塗りつぶしで透明化（キャラ内部の白等は残す）
///   2) 残った不透明領域（＝各キャラ）を島検出してコマに切り出す
/// という2段階で処理する。Unity の Sprite Editor「Automatic」スライスを、
/// 背景キーイング込みでコードで再現したもの。
///
/// 透明化した結果は player_sheet_keyed.png に書き出し、それを分割して使う
/// （元の player_sheet.png は触らない）。
/// </summary>
public static class PlayerSheetSlicer
{
    private const byte AlphaThreshold = 100;
    private const int MinW = 16, MinH = 22;     // これ未満の塊はノイズとして無視

    [MenuItem("Tools/HD2D/Slice Player Sheet")]
    public static void SliceMenu()
    {
        var rows = EnsureSlicedRows("Assets/Sprites/player_sheet.png");
        if (rows == null) { Debug.LogWarning("[HD2D] player_sheet.png が見つかりません。"); return; }
        Debug.Log($"[HD2D] スライス完了: {rows.Count} 行 / 合計 {rows.Sum(r => r.Length)} コマ");
    }

    /// <summary>背景が市松グレーかどうかの候補判定（透明も背景候補）。</summary>
    private static bool IsBackgroundCandidate(Color32 c)
    {
        if (c.a < AlphaThreshold) return true;
        int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        int mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
        int avg = (c.r + c.g + c.b) / 3;
        bool gray = (mx - mn) <= 20;            // ほぼ無彩色
        bool band = avg >= 85 && avg <= 210;    // 市松の明度帯
        return gray && band;
    }

    /// <summary>
    /// シートを透明化＆スライスし、行ごとの Sprite 配列リストを返す。
    /// アセットが無ければ null。
    /// </summary>
    public static List<Sprite[]> EnsureSlicedRows(string assetPath)
    {
        string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(abs)) return null;

        // ピクセルを直接デコード（importer の readable に依存しない）
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(abs));
        int w = tex.width, h = tex.height;
        Color32[] px = tex.GetPixels32();       // 原点は左下、index = y*w + x
        Object.DestroyImmediate(tex);

        // --- 1) 縁から市松グレーを塗りつぶして背景マスクを作る ---
        bool[] background = FloodBackgroundFromBorder(px, w, h);

        // --- 2) 背景を透明にしたテクスチャを書き出してインポート ---
        var keyed = new Color32[px.Length];
        for (int i = 0; i < px.Length; i++)
        {
            keyed[i] = px[i];
            if (background[i]) keyed[i].a = 0;
        }
        string keyedPath = Path.GetDirectoryName(assetPath).Replace('\\', '/') + "/player_sheet_keyed.png";
        WritePng(keyedPath, keyed, w, h);

        // --- 3) 前景（=非背景）の島を検出してコマ化 ---
        var fg = new bool[px.Length];
        for (int i = 0; i < px.Length; i++) fg[i] = !background[i];
        var boxes = DetectIslands(fg, w, h);
        if (boxes.Count == 0) return null;

        var rowsOfBoxes = GroupIntoRows(boxes);

        var rects = new List<SpriteRect>();
        var nameByRowCol = new List<(int row, int col, string name)>();
        for (int r = 0; r < rowsOfBoxes.Count; r++)
        {
            var row = rowsOfBoxes[r].OrderBy(b => b.xMin).ToList();
            for (int c = 0; c < row.Count; c++)
            {
                var b = row[c];
                string name = $"player_{r}_{c}";
                rects.Add(new SpriteRect
                {
                    name = name,
                    spriteID = GUID.Generate(),
                    rect = new Rect(b.xMin, b.yMin, b.Width, b.Height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0f),   // 足元中央
                    border = Vector4.zero
                });
                nameByRowCol.Add((r, c, name));
            }
        }

        ApplyRects(keyedPath, rects);

        var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(keyedPath)
            .OfType<Sprite>()
            .ToDictionary(s => s.name, s => s);

        var result = new List<Sprite[]>();
        for (int r = 0; r < rowsOfBoxes.Count; r++)
        {
            var inRow = nameByRowCol.Where(t => t.row == r)
                .OrderBy(t => t.col)
                .Select(t => sprites.TryGetValue(t.name, out var s) ? s : null)
                .Where(s => s != null)
                .ToArray();
            if (inRow.Length > 0) result.Add(inRow);
        }
        return result.Count > 0 ? result : null;
    }

    // --- 縁から背景グレーを塗りつぶし（キャラ内部のグレーは残る）---------
    private static bool[] FloodBackgroundFromBorder(Color32[] px, int w, int h)
    {
        var cand = new bool[px.Length];
        for (int i = 0; i < px.Length; i++) cand[i] = IsBackgroundCandidate(px[i]);

        var bg = new bool[px.Length];
        var stack = new Stack<int>();
        void Seed(int x, int y)
        {
            int p = y * w + x;
            if (cand[p] && !bg[p]) { bg[p] = true; stack.Push(p); }
        }
        for (int x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
        for (int y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };
        while (stack.Count > 0)
        {
            int p = stack.Pop();
            int x = p % w, y = p / w;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + dx[k], ny = y + dy[k];
                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                int np = ny * w + nx;
                if (cand[np] && !bg[np]) { bg[np] = true; stack.Push(np); }
            }
        }
        return bg;
    }

    // --- 連結成分（前景の島）検出 ----------------------------------------
    private struct Box
    {
        public int xMin, yMin, xMax, yMax;
        public int Width => xMax - xMin + 1;
        public int Height => yMax - yMin + 1;
        public float YCenter => (yMin + yMax) * 0.5f;
    }

    private static List<Box> DetectIslands(bool[] fg, int w, int h)
    {
        // 1px の隙間でキャラが分裂しないよう軽く膨張（3x3）
        var mask = Dilate(fg, w, h);

        var visited = new bool[w * h];
        var boxes = new List<Box>();
        var stack = new Stack<int>();
        int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || visited[start]) continue;
            int xMin = w, yMin = h, xMax = 0, yMax = 0;
            stack.Push(start);
            visited[start] = true;
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                int x = p % w, y = p / w;
                if (x < xMin) xMin = x; if (x > xMax) xMax = x;
                if (y < yMin) yMin = y; if (y > yMax) yMax = y;
                for (int k = 0; k < 8; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                    int np = ny * w + nx;
                    if (mask[np] && !visited[np]) { visited[np] = true; stack.Push(np); }
                }
            }
            var b = new Box { xMin = xMin, yMin = yMin, xMax = xMax, yMax = yMax };
            if (b.Width >= MinW && b.Height >= MinH) boxes.Add(b);
        }
        return boxes;
    }

    private static bool[] Dilate(bool[] src, int w, int h)
    {
        var dst = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool on = false;
                for (int j = -1; j <= 1 && !on; j++)
                    for (int i = -1; i <= 1 && !on; i++)
                    {
                        int nx = x + i, ny = y + j;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        if (src[ny * w + nx]) on = true;
                    }
                dst[y * w + x] = on;
            }
        return dst;
    }

    private static List<List<Box>> GroupIntoRows(List<Box> boxes)
    {
        // y中心の降順（テクスチャは下が y=0 なので、画像の上の行＝y大が先頭）
        var sorted = boxes.OrderByDescending(b => b.YCenter).ToList();
        float avgH = (float)boxes.Average(b => b.Height);
        float gap = avgH * 0.6f;

        var rows = new List<List<Box>>();
        var current = new List<Box>();
        float rowRef = sorted[0].YCenter;
        foreach (var b in sorted)
        {
            if (current.Count > 0 && rowRef - b.YCenter > gap)
            {
                rows.Add(current);
                current = new List<Box>();
            }
            current.Add(b);
            rowRef = b.YCenter;
        }
        if (current.Count > 0) rows.Add(current);
        return rows;
    }

    // --- 書き出し / インポート -------------------------------------------
    private static void WritePng(string assetPath, Color32[] pixels, int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels32(pixels);
        t.Apply();
        string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        File.WriteAllBytes(abs, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }

    private static void ApplyRects(string assetPath, List<SpriteRect> rects)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.spritePixelsPerUnit = 96f;
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
        dp.InitSpriteEditorDataProvider();
        dp.SetSpriteRects(rects.ToArray());

        var nameProvider = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (nameProvider != null)
        {
            var pairs = rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList();
            nameProvider.SetNameFileIdPairs(pairs);
        }

        dp.Apply();
        (dp.targetObject as AssetImporter).SaveAndReimport();
    }
}
