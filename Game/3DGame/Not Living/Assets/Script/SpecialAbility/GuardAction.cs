using UnityEngine;

public class GuardAction : MonoBehaviour, ISpecialAction
{
    [Header("ガード設定")]
    [Tooltip("ガードが成功した後のクールタイム（秒）")]
    public float cooldownDuration = 5f;
    [Tooltip("ガードによって無敵になる時間（秒）")] // ▼▼▼ 無敵時間用の変数を追加 ▼▼▼
    public float guardInvincibilityDuration = 2f;
    [Tooltip("UIに表示する能力名")]
    public string abilityName = "ガード";

    private float nextActionTime = 0f;
    
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
        if (Time.time >= nextActionTime)
        {
            StatusManager status = playerController.GetPossessedStatusManager();
            if (status != null)
            {
                Debug.Log($"<color=cyan>[{status.gameObject.name}] ガード発動！ {guardInvincibilityDuration}秒間無敵になります。</color>");
                
                // ▼▼▼ 引数ありのBecomeInvincibleを、独自の無敵時間で呼び出す ▼▼▼
                status.StartCoroutine(status.BecomeInvincible(guardInvincibilityDuration));

                nextActionTime = Time.time + cooldownDuration;
            }
        }
        else
        {
            Debug.Log("ガードはクールタイム中です。");
        }
    }
}