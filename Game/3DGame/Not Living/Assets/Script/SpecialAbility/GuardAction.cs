// GuardAction.cs
using UnityEngine;

public class GuardAction : MonoBehaviour, ISpecialAction
{
    [Header("ガード設定")]
    [Tooltip("ガードが成功した後のクールタイム（秒）")]
    public float cooldownDuration = 5f;
    [Tooltip("UIに表示する能力名")] // 表示名用の変数を追加 
    public string abilityName = "ムテキ";

    private float nextActionTime = 0f;

    // インターフェースの新しいルールを実装 
    public string AbilityName => abilityName;
    // 

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
                status.StartCoroutine(status.BecomeInvincible());
                nextActionTime = Time.time + cooldownDuration;
            }
        }
    }
}