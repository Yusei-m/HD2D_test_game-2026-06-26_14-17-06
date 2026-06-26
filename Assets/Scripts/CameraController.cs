using UnityEngine;

/// <summary>
/// HD-2D の俯瞰アイソメトリック視点を維持したまま、
/// プレイヤーを Vector3.Lerp で滑らかに追従するカメラ。
/// 角度（Rotation）は初期セットアップ時のものを保持し、位置のみ追従する。
/// </summary>
public class CameraController : MonoBehaviour
{
    [Tooltip("追従対象（プレイヤー）")]
    public Transform target;

    [Tooltip("対象からのオフセット。未設定なら起動時の相対位置を自動採用。")]
    public Vector3 offset = new Vector3(0f, 8f, -7f);

    [Tooltip("追従の滑らかさ。大きいほど素早く追いつく。")]
    public float smoothing = 6f;

    [Tooltip("起動時のカメラ-プレイヤー相対位置をオフセットとして使う")]
    public bool useStartOffset = true;

    private void Start()
    {
        if (target != null && useStartOffset)
        {
            offset = transform.position - target.position;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(
            transform.position, desired, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
    }
}
