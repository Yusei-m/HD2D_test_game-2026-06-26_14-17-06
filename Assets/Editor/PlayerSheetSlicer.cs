using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// AI生成のキャラクターシートのように、各コマが等間隔グリッドに乗っていない
/// スプライトシートを、透明部分でキャラを自動検出してコマに切り出す。
/// Unity の Sprite Editor「Automatic」スライスを、コードで再現したもの。
///
/// 検出したコマは上の行から順に並べ、行ごとの Sprite 配列として返す
/// （PlayerSpriteAnimator がそのまま向き別アニメに使う）。
/// </summary>
public static class PlayerSheetSlicer
{
    private const byte AlphaThreshold = 100;   // これ以上の α を不透明とみなす
    private const int MinW = 16, MinH = 22;    // これ未満の塊はノイズとして無視

    [MenuItem("Tools/HD2D/Slice Player Sheet")]
    public static void SliceMenu()
    {
        var rows = EnsureSlicedRows("Assets/Sprites/player_sheet.png");
        if (rows == null) { Debug.LogWarning("[HD2D] player_sheet.png が見つかりません。"); return; }
        Debug.Log($"[HD2D] スライス完了: {rows.Count} 行 / 合計 {rows.Sum(r => r.Length)} コマ");
    }

    /// <summary>
    /// シートをスライスし、行ごとの Sprite 配列リストを返す。
    /// アセットが無ければ null。
    /// </summary>
    public static List<Sprite[]> EnsureSlicedRows(string assetPath)
    {
        string abs = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        if (!File.Exists(abs)) return null;

        // 透明判定のためにピクセルを読む（importer の readable に依存しないよう直接デコード）
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(abs));
        int w = tex.width, h = tex.height;
        Color32[] px = tex.GetPixels32();   // 原点は左下、index = y*w + x
        Object.DestroyImmediate(tex);

        var boxes = DetectIslands(px, w, h);
        if (boxes.Count == 0) return null;

        // 行にまとめる（y中心が近いものを同じ行に）。テクスチャは下が y=0 なので
        // 「上の行」= y が大きい。降順に並べて行を切る。
        var rowsOfBoxes = GroupIntoRows(boxes);

        // SpriteRect を構築（ピボットは足元＝下中央）
        var rects = new List<SpriteRect>();
        var nameByRowCol = new List<(int row, int col, string name)>();
        for (int r = 0; r < rowsOfBoxes.Count; r++)
        {
            var row = rowsOfBoxes[r].OrderBy(b => b.xMin).ToList();
            for (int c = 0; c < row.Count; c++)
            {
                var b = row[c];
                string name = $"player_{r}_{c}";
                var sr = new SpriteRect
                {
                    name = name,
                    spriteID = GUID.Generate(),
                    rect = new Rect(b.xMin, b.yMin, b.Width, b.Height),
                    alignment = SpriteAlignment.Custom,
                    pivot = new Vector2(0.5f, 0f),
                    border = Vector4.zero
                };
                rects.Add(sr);
                nameByRowCol.Add((r, c, name));
            }
        }

        ApplyRects(assetPath, rects);

        // スライス後の Sprite を読み直し、名前から行ごとに束ねる
        var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
            .OfType<Sprite>()
            .ToDictionary(s => s.name, s => s);

        int rowCountTotal = rowsOfBoxes.Count;
        var result = new List<Sprite[]>();
        for (int r = 0; r < rowCountTotal; r++)
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

    // --- 連結成分（不透明の島）検出 ---------------------------------------
    private struct Box
    {
        public int xMin, yMin, xMax, yMax;
        public int Width => xMax - xMin + 1;
        public int Height => yMax - yMin + 1;
        public float YCenter => (yMin + yMax) * 0.5f;
    }

    private static List<Box> DetectIslands(Color32[] px, int w, int h)
    {
        var opaque = new bool[w * h];
        for (int i = 0; i < px.Length; i++)
            opaque[i] = px[i].a >= AlphaThreshold;

        // 1px の隙間でキャラが分裂しないよう軽く膨張（3x3）
        var mask = Dilate(opaque, w, h);

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
        // y中心の降順（上の行が先）。隣接する箱の y中心差が大きい所で行を分割。
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
            rowRef = b.YCenter; // 行内の最後の箱を基準に追従
        }
        if (current.Count > 0) rows.Add(current);
        return rows;
    }

    // --- 切り出し結果を importer に書き込む ------------------------------
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

        // 名前↔ID の対応も登録（新しい Unity の警告回避）
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
