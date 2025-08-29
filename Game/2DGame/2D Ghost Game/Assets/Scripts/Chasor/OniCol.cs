using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OniCol : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Rigidbody2D を持つオブジェクトなら処理
        Rigidbody2D rb = other.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            // 速度を「大きさ 1」に正規化
            if (rb.velocity != Vector2.zero)
            {
                rb.velocity = rb.velocity.normalized * 1f;
            }
        }
    }
}
