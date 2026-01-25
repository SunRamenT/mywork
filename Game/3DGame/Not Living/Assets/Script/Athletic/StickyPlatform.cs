using UnityEngine;
using System.Collections.Generic;

public class StickyPlatform : MonoBehaviour
{
    private Vector3 lastPosition;
    
    // キャラクター（人）用リスト
    private List<CharacterController> characterPassengers = new List<CharacterController>();
    // 物理オブジェクト（ゴミなど）用リスト
    private List<Rigidbody> rbPassengers = new List<Rigidbody>();

    void Start()
    {
        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        // 床の移動量を計算
        Vector3 platformMovement = transform.position - lastPosition;

        if (platformMovement != Vector3.zero)
        {
            // 1. キャラクターを動かす
            for (int i = characterPassengers.Count - 1; i >= 0; i--)
            {
                var cc = characterPassengers[i];
                if (cc != null && cc.enabled)
                {
                    cc.Move(platformMovement);
                }
                else
                {
                    characterPassengers.RemoveAt(i); // 無効なものはリストから削除
                }
            }

            // 2. 物理オブジェクト（Rigidbody）を動かす
            for (int i = rbPassengers.Count - 1; i >= 0; i--)
            {
                var rb = rbPassengers[i];
                if (rb != null && !rb.isKinematic)
                {
                    // 物理演算を壊さないように、Transformを直接足す
                    // （Rigidbody.MovePositionを使うと慣性がリセットされることがあるため、単純な追従ならこれが安定します）
                    rb.transform.position += platformMovement;
                }
                else
                {
                    rbPassengers.RemoveAt(i);
                }
            }
        }

        lastPosition = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 判定用コライダー自体は無視
        if (other.isTrigger) return;

        // A. キャラクターの場合
        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            if (!characterPassengers.Contains(cc))
            {
                characterPassengers.Add(cc);
            }
            return; // キャラクターならここで終了
        }

        // B. 物理オブジェクト（ゴミなど）の場合
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            if (!rbPassengers.Contains(rb))
            {
                rbPassengers.Add(rb);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // A. キャラクターの解除
        CharacterController cc = other.GetComponent<CharacterController>();
        if (cc != null)
        {
            if (characterPassengers.Contains(cc))
            {
                characterPassengers.Remove(cc);
            }
            return;
        }

        // B. 物理オブジェクトの解除
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            if (rbPassengers.Contains(rb))
            {
                rbPassengers.Remove(rb);
            }
        }
    }
}