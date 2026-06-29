using UnityEngine;

/// <summary>
/// Y軸ビルボード。3D空間内でカメラがどう動いても、
/// 2DスプライトがカメラのYaw（水平回転）に同期して常に正面を向く。
/// 垂直（ピッチ）は同期しないため、キャラクターは直立を保ち
/// "ペラペラに平坦" には見えない。
/// </summary>
public class Billboard : MonoBehaviour
{
    private Transform _cam;

    private void Start()
    {
        if (Camera.main != null)
            _cam = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (_cam == null)
        {
            if (Camera.main == null) return;
            _cam = Camera.main.transform;
        }

        // カメラのYaw成分のみを取り出してスプライトに適用する。
        Vector3 e = _cam.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, e.y, 0f);
    }
}
