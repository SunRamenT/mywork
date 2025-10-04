using UnityEngine;
using System.Collections;
using UniRx; // UniRxを使うために必要

public class Bomb : MonoBehaviour
{
    [Header("爆弾設定")]
    public float delay = 3f;
    public float explosionRadius = 5f;
    public float knockbackForce = 10f; // 吹き飛ばしの強さ
    public float knockbackDuration = 0.5f; // 吹き飛ばされる時間
    public int damage = 50;

    [Header("エフェクト設定")]
    public GameObject explosionEffectPrefab;

    private AudioSource audioSource;
    public AudioClip BombSound;

    [Tooltip("特殊能力の音が聞こえる半径")]
    public float specialAbilityVolume = 15f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        StartCoroutine(ExplodeAfterDelay());
    }

    private IEnumerator ExplodeAfterDelay()
    {
        yield return new WaitForSeconds(delay);
        Explode();
    }

    private void Explode()
    {
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);

        foreach (Collider hit in colliders)
        {
            if (hit.TryGetComponent<StatusManager>(out StatusManager targetStatus))
            {
                // ダメージを与える
                targetStatus.TakeDamage(damage, null);

                // ▼▼▼ 吹き飛ばし処理をこちらに変更 ▼▼▼
                // 爆心地から対象への方向を計算
                Vector3 knockbackDirection = (hit.transform.position - transform.position).normalized;
                // StatusManagerの吹き飛ばしメソッドを呼び出す
                targetStatus.ApplyKnockback(knockbackDirection, knockbackForce, knockbackDuration);
            }
        }
        if (BombSound != null)
        {
            audioSource.PlayOneShot(BombSound);
        }
        // 2. Chaserに聞こえるように、音の情報をMessageBrokerで発信する
        MessageBroker.Default.Publish(new SoundPacket(transform.position, specialAbilityVolume, SoundType.PlayerAction));
        // デバッグ用にログを表示
        Debug.Log($"<color=lightblue>{gameObject.name} が音を発生させました (大きさ: {specialAbilityVolume})</color>");

        // オブジェクトを「非表示・無効化」にする
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            renderer.enabled = false;

        foreach (var collider in GetComponentsInChildren<Collider>())
            collider.enabled = false;

        Destroy(gameObject, 2f);
    }
}