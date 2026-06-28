using UnityEngine;

/// <summary>
/// プレイヤーが上を歩ける面（地面・階段スロープ・橋など）に付ける目印。
/// PlayerController はこの目印が付いたコライダーだけを地面として高さ追従する。
/// 建物などには付けないので、建物は壁として水平移動を止める。
/// </summary>
public class WalkableSurface : MonoBehaviour
{
}
