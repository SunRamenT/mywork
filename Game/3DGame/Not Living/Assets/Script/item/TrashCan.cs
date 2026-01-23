using UnityEngine;
using System.Collections;

public class TrashCan : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("ゴミを捨てた時の回復量")]
    public int healAmount = 15;
    
    [Tooltip("ゴミを捨てた時の効果音")]
    public AudioClip disposeSound;

    private AudioSource audioSource;

    private PlayerController interactingPlayer;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            // AudioSourceがなければ追加しておく
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // 他のコライダーが入ってきた時に呼ばれる
    void OnTriggerEnter(Collider other)
    {
        // 入ってきたのが「ゴミ」かどうかチェック
        // TrashItemコンポーネントがついているかで判断
        TrashItem trash = other.GetComponent<TrashItem>();

        if (trash != null)
        {
            // --- ゴミ処理実行 ---

            // 1. もしプレイヤーが持っている最中なら、強制的に手から離させる処理が必要だが、
            //    UnityはDestroyされたオブジェクトへの参照をnullとして扱うため、
            //    PlayerController側は「currentHeldItem == null」となり自然に解決することが多い。
            //    念のため、物理挙動を切ったりする必要はない（Destroyで消えるため）。
            
            // 2. ゴミを削除
            Destroy(trash.gameObject);

            // 3. 音を鳴らす
            if (disposeSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(disposeSound);
            }

            // 4. 善行イベントを発行（AlignmentManagerが受け取って善悪値を更新）
            GameEvents.TriggerGoodDeedPerformed();

            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.moveSpeed += 0.05f;
                interactingPlayer = player;
            }


            // 3. 乗っ取り中のNPCの評判を上げる
            if (interactingPlayer != null && interactingPlayer.IsPossessing())
            {
                StatusManager possessedStatus = interactingPlayer.GetPossessedStatusManager();
                if (possessedStatus != null)
                {
                    possessedStatus.AddReputation(5);
                }
            }

            // 5. じんわり回復を開始
            StartCoroutine(GradualHeal(healAmount));
            
            Debug.Log("ゴミを捨てました！ 善行により霊魂が回復します。");
        }
    }

    // TaskMachineと同じ「じんわり回復」ロジック
    private IEnumerator GradualHeal(int totalAmount)
    {
        ReikonManager manager = FindFirstObjectByType<ReikonManager>();
        
        if (manager != null)
        {
            int steps = 5;                  // 回復を5回に分ける
            float interval = 0.2f;          // 各回の間隔（秒）。TaskMachineより少し早めに設定例
            int healPerStep = totalAmount / steps;

            for (int i = 0; i < steps; i++)
            {
                manager.Heal(healPerStep);
                yield return new WaitForSeconds(interval);
            }

            // 端数があれば最後にまとめて回復
            int remainder = totalAmount % steps;
            if (remainder > 0)
            {
                manager.Heal(remainder);
            }
        }
    }
}