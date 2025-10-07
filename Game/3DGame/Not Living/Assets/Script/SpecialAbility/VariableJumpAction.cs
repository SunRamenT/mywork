using UnityEngine;

public class VariableJumpAction : MonoBehaviour, ISpecialAction
{
    [Header("能力設定")]
    [Tooltip("UIに表示する能力名")]
    public string abilityName = "ジャンプ";
    [Tooltip("ジャンプの初速")]
    public float jumpPower = 15f;
    [Tooltip("ジャンプアニメーションのトリガー名")]
    public string jumpTriggerName = "Jump";

    // --- インターフェースの実装 ---
    public string AbilityName => abilityName;
    // このジャンプにはクールタイムがないので、常に1.0（完了）を返す
    public float CooldownProgress => 1.0f; 

    public void PerformAction(PlayerController playerController)
    {
        // PlayerControllerから、現在操作しているAnimatorを取得
        Animator animator = playerController.GetCurrentAnimator();

        // ジャンプアニメーションのトリガーを起動
        if (animator != null && playerController.HasParameter(animator, jumpTriggerName))
        {
            animator.SetTrigger(jumpTriggerName);
        }
        
        // PlayerControllerにジャンプの初速を与えるよう命令
        playerController.ApplyJumpForce(jumpPower);
    }
}