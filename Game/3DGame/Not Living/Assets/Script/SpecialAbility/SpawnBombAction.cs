using UnityEngine;

public class SpawnBombAction : MonoBehaviour, ISpecialAction
{
    [Header("能力設定")]
    [Tooltip("UIに表示する能力名")]
    public string abilityName = "ボム設置";
    [Tooltip("この能力のクールタイム（秒）")]
    public float cooldownDuration = 10f;

    [Header("ボム設定")]
    [Tooltip("生成するボムのプレハブ")]
    public GameObject bombPrefab;
    [Tooltip("ボムを生成する場所（キャラクターの前方など）")]
    public Transform spawnPoint;

    // --- 内部変数 ---
    private float nextActionTime = 0f;
    
    // --- インターフェースの実装 ---
    public string AbilityName => abilityName;
    public float CooldownProgress
    {
        get
        {
            if (Time.time >= nextActionTime) return 1.0f;
            float startTime = nextActionTime - cooldownDuration;
            float elapsedTime = Time.time - startTime;
            return elapsedTime / cooldownDuration;
        }
    }
    
    public void PerformAction(PlayerController playerController)
    {
        // クールタイムが終わっていなければ何もしない
        if (Time.time < nextActionTime)
        {
            Debug.Log("ボムはクールタイム中です。");
            return;
        }

        // ボムと生成場所が設定されていれば
        if (bombPrefab != null && spawnPoint != null)
        {
            Debug.Log("ボムを設置！");
            // 指定した場所にボムを生成する
            Instantiate(bombPrefab, spawnPoint.position, spawnPoint.rotation);
            
            // 次に使用可能になる時間を記録
            nextActionTime = Time.time + cooldownDuration;
        }
        else
        {
            Debug.LogWarning("ボムのプレハブ、または生成場所が設定されていません！");
        }
    }
}