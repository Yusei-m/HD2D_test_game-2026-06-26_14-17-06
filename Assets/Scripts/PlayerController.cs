using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// HD-2D ウォーキングゲーム用のプレイヤー移動コントローラ。
/// 矢印キー / WASD 入力で 3D 空間の X-Z 平面を滑らかに移動する。
/// このプロジェクトは新 Input System を使用しているため、
/// Keyboard.current から直接キー状態を読む（プロジェクト設定に依存しない）。
/// 回転は全軸固定し、向きは Billboard が管理する。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("歩行速度 (m/s)")]
    public float moveSpeed = 4.5f;

    [Tooltip("加減速の滑らかさ。大きいほど機敏に追従する。")]
    public float acceleration = 20f;

    private Rigidbody _rb;
    private Vector3 _currentVelocity;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // 物理演算による回転を全軸固定。歩行ゲームでは転倒・落下させない。
        _rb.constraints = RigidbodyConstraints.FreezeRotation |
                          RigidbodyConstraints.FreezePositionY;
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

        // 目標速度へ滑らかに補間（遅延の無い、しかし急発進しすぎない歩行感）
        _currentVelocity = Vector3.MoveTowards(
            _currentVelocity, targetVelocity, acceleration * Time.fixedDeltaTime);

        _rb.MovePosition(_rb.position + _currentVelocity * Time.fixedDeltaTime);
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
