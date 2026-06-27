using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// スプライトシートから切り出したコマを、移動方向に合わせて再生する。
/// ビルボードで常にカメラ正面を向く前提なので、シートの各行を
/// 「下(手前)・上(奥)・左・右」の歩行に割り当てて切り替える。
///
/// frames は全コマを行順（row-major）に平坦化して保持し、
/// rowStart / rowCount で各行を参照する。row{Down,Up,Left,Right} は
/// どの行をその向きに使うかのインデックス（Inspector で変更可）。
/// </summary>
public class PlayerSpriteAnimator : MonoBehaviour
{
    [Header("コマ（行順に平坦化）")]
    public Sprite[] frames;
    public int[] rowStart;
    public int[] rowCount;

    [Header("向き→行の割り当て")]
    public int rowDown = 0;   // 手前（カメラ側）へ歩く
    public int rowUp = 1;     // 奥へ歩く
    public int rowLeft = 2;
    public int rowRight = 2;
    [Tooltip("右移動時に左の行を左右反転して使う")]
    public bool flipRightFromLeft = true;

    [Header("再生")]
    public float framesPerSecond = 8f;

    private SpriteRenderer _sr;
    private int _dir = 0;      // 0=down,1=up,2=left,3=right
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
            // 支配的な軸で向きを決定
            if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
                _dir = input.x > 0 ? 3 : 2;
            else
                _dir = input.y > 0 ? 1 : 0;

            _timer += Time.deltaTime;
        }
        else
        {
            _timer = 0f; // 停止時は各行の先頭（立ち）コマ
        }

        int row = RowForDir(_dir);
        bool flip = (_dir == 3 && rowRight == rowLeft && flipRightFromLeft);

        int count = (row < rowCount.Length) ? rowCount[row] : 0;
        if (count <= 0) return;

        int frame = moving ? (int)(_timer * framesPerSecond) % count : 0;
        int idx = rowStart[row] + frame;
        if (idx >= 0 && idx < frames.Length && frames[idx] != null)
            _sr.sprite = frames[idx];
        _sr.flipX = flip;
    }

    private int RowForDir(int dir)
    {
        switch (dir)
        {
            case 1: return Mathf.Clamp(rowUp, 0, RowMax());
            case 2: return Mathf.Clamp(rowLeft, 0, RowMax());
            case 3: return Mathf.Clamp(rowRight, 0, RowMax());
            default: return Mathf.Clamp(rowDown, 0, RowMax());
        }
    }

    private int RowMax() => Mathf.Max(0, (rowStart != null ? rowStart.Length : 1) - 1);

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
    /// <summary>ビルド時にスライス結果（行ごとのコマ配列）を流し込む。</summary>
    public void Setup(System.Collections.Generic.List<Sprite[]> rows)
    {
        var flat = new System.Collections.Generic.List<Sprite>();
        rowStart = new int[rows.Count];
        rowCount = new int[rows.Count];
        for (int r = 0; r < rows.Count; r++)
        {
            rowStart[r] = flat.Count;
            rowCount[r] = rows[r].Length;
            flat.AddRange(rows[r]);
        }
        frames = flat.ToArray();

        // 行数に応じて妥当な既定マッピング
        int n = rows.Count;
        rowDown = 0;
        rowUp = n > 1 ? 1 : 0;
        rowLeft = n > 2 ? 2 : 0;
        rowRight = rowLeft;
        flipRightFromLeft = true;
    }
#endif
}
