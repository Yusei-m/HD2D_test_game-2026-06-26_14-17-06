using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HD-2D ウォーキングゲーム用のプレイヤー移動コントローラ。
/// 矢印キー / WASD 入力で 3D 空間の X-Z 平面を滑らかに移動する。
/// さらに、足元の「歩ける面」(WalkableSurface) の高さに追従して Y を合わせるので、
/// 階段スロープや橋を登り降りできる。建物などには WalkableSurface を付けないため、
/// 壁として水平移動を止める。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("歩行速度 (m/s)")]
    public float moveSpeed = 4.5f;

    [Tooltip("加減速の滑らかさ。大きいほど機敏に追従する。")]
    public float acceleration = 20f;

    [Tooltip("足元（原点）が地面から浮く高さ。スプライトの足元が地面に来る値。")]
    public float groundOffset = 0.9f;

    [Tooltip("段差を登り降りする速さ。")]
    public float climbSpeed = 7f;

    private Rigidbody _rb;
    private Vector3 _currentVelocity;
    private readonly RaycastHit[] _hits = new RaycastHit[16];

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // 回転は全軸固定。Y位置は地面追従で自分で制御するので固定しない。
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void FixedUpdate()
    {
        Vector2 input = ReadInput();
        Vector3 dir = new Vector3(input.x, 0f, input.y);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        Vector3 targetVelocity = dir * moveSpeed;
        _currentVelocity = Vector3.MoveTowards(
            _currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);

        Vector3 next = _rb.position + _currentVelocity * Time.fixedDeltaTime;

        // 移動先の真上から下へレイを飛ばし、歩ける面の高さに Y を合わせる
        float y = next.y;
        if (TryGetGroundHeight(next.x, next.z, next.y, out float groundY))
        {
            float targetY = groundY + groundOffset;
            y = Mathf.MoveTowards(next.y, targetY, climbSpeed * Time.fixedDeltaTime);
        }

        _rb.MovePosition(new Vector3(next.x, y, next.z));
    }

    /// <summary>(x,z) の真上から下へレイを飛ばし、WalkableSurface の最も高い接点の高さを返す。</summary>
    private bool TryGetGroundHeight(float x, float z, float currentY, out float groundY)
    {
        groundY = 0f;
        var origin = new Vector3(x, currentY + 5f, z);
        int n = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, 30f,
            ~0, QueryTriggerInteraction.Ignore);
        bool found = false;
        float best = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            var col = _hits[i].collider;
            if (col == null) continue;
            if (col.GetComponentInParent<WalkableSurface>() == null) continue;
            if (_hits[i].point.y > best) { best = _hits[i].point.y; found = true; }
        }
        if (found) groundY = best;
        return found;
    }

    /// <summary>WASD と矢印キーから移動入力 (-1..1) を読む。</summary>
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
}
