using UnityEngine;

public class TimeStopAction : MonoBehaviour, ISpecialAction
{
    [Header("時間停止設定")]
    [Tooltip("時間停止後のクールタイム（秒）")]
    public float cooldownDuration = 60f;
    [Tooltip("時間停止できる時間（秒）")] 
    public float timeStopDuration = 2f;
    [Tooltip("UIに表示する能力名")]
    public string abilityName = "時間停止";

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
                Debug.Log($"<color=cyan>[{status.gameObject.name}] 時間停止発動！ {timeStopDuration}秒間時が止まります。</color>");
                if (PlayerTimeManager.Instance != null)
                {
                    float dodgeStopMagnitude = 0.1f;
                    float dodgeStopDuration = 10f;
                    PlayerTimeManager.Instance.StartSlowMotion(dodgeStopMagnitude, dodgeStopDuration);
                }
                nextActionTime = Time.time + cooldownDuration;
            }
        }
        else
        {
            Debug.Log("時間停止はクールタイム中です。");
        }
    }
}
