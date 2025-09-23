using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class DamageFloor : MonoBehaviour
{
    [Header("ダメージ設定")]
    [Tooltip("1秒あたりに与えるダメージ量")]
    public int damagePerSecond = 10;
    [Tooltip("ダメージを与える間隔（秒）")]
    public float damageInterval = 1.0f;

    // 床に乗っているキャラクターのリスト
    private List<StatusManager> targetsOnFloor = new List<StatusManager>();
    // ダメージ処理を実行中かどうかのフラグ
    private Coroutine damageCoroutine;

    // オブジェクトが有効になった時に呼ばれる
    private void OnEnable()
    {
        // 既に誰か乗っていた場合（ゲームの途中セーブなど）も考慮
        if (targetsOnFloor.Count > 0)
        {
            damageCoroutine = StartCoroutine(DealDamageOverTime());
        }
    }

    // 他のコライダーがこのトリガーに入った時に呼ばれる
    private void OnTriggerEnter(Collider other)
    {
        // 接触したオブジェクトからStatusManagerを取得
        if (other.TryGetComponent<StatusManager>(out StatusManager targetStatus))
        {
            // リストに既になければ追加
            if (!targetsOnFloor.Contains(targetStatus))
            {
                targetsOnFloor.Add(targetStatus);
            }

            // まだダメージ処理が動いていなければ、開始する
            if (damageCoroutine == null)
            {
                damageCoroutine = StartCoroutine(DealDamageOverTime());
            }
        }
    }

    // 他のコライダーがこのトリガーから出た時に呼ばれる
    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<StatusManager>(out StatusManager targetStatus))
        {
            // リストから削除
            targetsOnFloor.Remove(targetStatus);

            // もし誰も床に乗っていなくなったら、ダメージ処理を停止
            if (targetsOnFloor.Count == 0 && damageCoroutine != null)
            {
                StopCoroutine(damageCoroutine);
                damageCoroutine = null;
            }
        }
    }

    // 一定間隔でダメージを与え続けるコルーチン
    private IEnumerator DealDamageOverTime()
    {
        // 無限ループ
        while (true)
        {
            // 現在床に乗っている全てのターゲットに対して処理
            // forループを逆順にすることで、リストから要素が削除されてもエラーを防ぐ
            for (int i = targetsOnFloor.Count - 1; i >= 0; i--)
            {
                StatusManager target = targetsOnFloor[i];
                if (target != null)
                {
                    // ダメージを与える（攻撃者はいないのでnullを渡す）
                    target.TakeDamage(Mathf.CeilToInt(damagePerSecond * damageInterval), null);
                }
                else
                {
                    // ターゲットが（倒されるなどして）消滅していたらリストから除去
                    targetsOnFloor.RemoveAt(i);
                }
            }

            // 指定した間隔だけ待機
            yield return new WaitForSeconds(damageInterval);
        }
    }
}