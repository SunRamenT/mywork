using UnityEngine;
using System.Collections.Generic;

public class StickyPlatform : MonoBehaviour
{
    private Vector3 lastPosition;
    private List<CharacterController> passengers = new List<CharacterController>();

    void Start()
    {
        lastPosition = transform.position;
    }

    // すべての移動処理（FixedUpdateやUpdate）が終わった後に位置を補正する
    void LateUpdate()
    {
        // 1. 今回のフレームでの床の移動量を計算
        Vector3 platformMovement = transform.position - lastPosition;

        // 2. 乗っているキャラクター全員を、床と同じだけ動かす
        if (platformMovement != Vector3.zero)
        {
            foreach (var controller in passengers)
            {
                // キャラクターが生きていて有効な場合のみ
                if (controller != null && controller.enabled)
                {
                    // CharacterController.Moveを使うことで、壁のめり込みも防ぎつつ移動できる
                    controller.Move(platformMovement);
                }
            }
        }

        // 次のフレームのために位置を更新
        lastPosition = transform.position;
    }

    // --- 乗っている判定 ---

    private void OnTriggerEnter(Collider other)
    {
        // CharacterControllerを持っている相手ならリストに追加
        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            if (!passengers.Contains(cc))
            {
                passengers.Add(cc);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // エリアから出たらリストから削除
        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            if (passengers.Contains(cc))
            {
                passengers.Remove(cc);
            }
        }
    }
}