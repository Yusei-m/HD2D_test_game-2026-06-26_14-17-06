using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// スプライトシートから切り出したコマを、移動方向に合わせて再生する。
/// ビルボードで常にカメラ正面を向く前提。
///
/// このシートは AI 生成で「行＝向き」になっていない（前向きの行に後ろ向きの
/// コマが混ざる等）ため、向きごとに使うコマを明示的に指定する方式にしている。
/// {down,up,left,right}Frames は frames 配列へのインデックス。
/// flip{...} はそのコマを左右反転して使うか（右向きコマを反転して左向きに流用）。
/// 値は Inspector で調整可能。
/// </summary>
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("全コマ（行順に平坦化）")]
    public Sprite[] frames;

    [Header("向きごとに使うコマ（frames のインデックス）")]
    public int[] downFrames;
    public int[] upFrames;
    public int[] leftFrames;
    public int[] rightFrames;

    [Header("左右反転して使うか")]
    public bool flipDown;
    public bool flipUp;
    public bool flipLeft = true;   // 右向きコマを反転して左向きに
    public bool flipRight;

    [Header("再生")]
    public float framesPerSecond = 8f;

    private SpriteRenderer _sr;
    private int _dir = 0;          // 0=down,1=up,2=left,3=right
    private float _timer;

    private void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0 || _sr == null) return;

        Vector2 input = ReadInput();
        bool moving = input.sqrMagnitude > 0.01f;

        if (moving)
        {
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                _dir = input.x > 0 ? 3 : 2;
            else
                _dir = input.y > 0 ? 1 : 0;
            _timer += Time.deltaTime;
        }
        else
        {
            _timer = 0f; // 停止時は各向きの先頭（立ち）コマ
        }

        int[] seq = SeqForDir(_dir);
        bool flip = FlipForDir(_dir);
        if (seq == null || seq.Length == 0) return;

        int frame = moving ? (int)(_timer * framesPerSecond) % seq.Length : 0;
        int idx = seq[frame];
        if (idx >= 0 && idx < frames.Length && frames[idx] != null)
            _sr.sprite = frames[idx];
        _sr.flipX = flip;
    }

    private int[] SeqForDir(int dir)
    {
        switch (dir)
        {
            case 1: return (upFrames != null && upFrames.Length > 0) ? upFrames : downFrames;
            case 2: return (leftFrames != null && leftFrames.Length > 0) ? leftFrames : downFrames;
            case 3: return (rightFrames != null && rightFrames.Length > 0) ? rightFrames : downFrames;
            default: return downFrames;
        }
    }

    private bool FlipForDir(int dir)
    {
        switch (dir)
        {
            case 1: return flipUp;
            case 2: return flipLeft;
            case 3: return flipRight;
            default: return flipDown;
        }
    }

    private static Vector2 ReadInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        float x = 0f, y = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
        return new Vector2(x, y);
    }

#if UNITY_EDITOR
    /// <summary>
    /// ビルド時にスライス結果を流し込み、このシート向けの向き別コマを設定する。
    /// 行構成は [7,7,7,6,5] を想定（PlayerSheetSlicer が上の行から順に格納）。
    /// </summary>
    public void Setup(System.Collections.Generic.List<Sprite[]> rows)
    {
        var flat = new System.Collections.Generic.List<Sprite>();
        var rowStart = new int[rows.Count];
        for (int r = 0; r < rows.Count; r++)
        {
            rowStart[r] = flat.Count;
            flat.AddRange(rows[r]);
        }
        frames = flat.ToArray();

        int Flat(int r, int c)
        {
            if (r < 0 || r >= rows.Count) return -1;
            if (c < 0 || c >= rows[r].Length) return -1;
            return rowStart[r] + c;
        }
        int[] Pick(params (int r, int c)[] cells)
        {
            var list = new System.Collections.Generic.List<int>();
            foreach (var (r, c) in cells)
            {
                int f = Flat(r, c);
                if (f >= 0) list.Add(f);
            }
            return list.ToArray();
        }

        // このシートのコマ割りに合わせ、足が交互に前後する歩行サイクルを選定。
        // 前向き：上段の歩行コマ。
        downFrames = Pick((0, 0), (0, 1), (0, 2), (0, 3), (0, 4), (0, 5));
        // 後ろ向き：左足前(2,3)→直立(1,0)→右足前(2,6)→直立(1,0) で交互に。
        upFrames = Pick((2, 3), (1, 0), (2, 6), (1, 0));
        // 右向き：左右の足が明確に入れ替わる下段の歩行コマ。4,4 を中割りに挟む。
        rightFrames = Pick((4, 1), (4, 4), (4, 3), (4, 4));
        // 左向き：右向きコマを左右反転して使う。
        leftFrames = rightFrames;

        flipDown = false;
        flipUp = false;
        flipRight = false;
        flipLeft = true;

        // 万一どれかが空なら前向きで代替
        if (downFrames.Length == 0 && frames.Length > 0)
            downFrames = new[] { 0 };
        if (upFrames.Length == 0) upFrames = downFrames;
        if (rightFrames.Length == 0) rightFrames = downFrames;
        if (leftFrames.Length == 0) leftFrames = downFrames;
    }
#endif
}
