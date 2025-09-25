// SoundEmitter.cs
using UnityEngine;
using UniRx;

public class SoundEmitter : MonoBehaviour
{
    [Header("サウンド設定")]
    public SoundType soundType = SoundType.PlayerAction;
    [Tooltip("基本の音の大きさ（乗っ取り中でない場合や、相手に設定がない場合）")]
    public float volume = 10f;
    [Tooltip("プレイヤーがNPCに乗っ取られている時だけ音を発生させる")]
    public bool possessOnly = false;

    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    public void EmitSound()
    {
        if (possessOnly && (playerController == null || !playerController.IsPossessing()))
        {
            return;
        }

        // ▼▼▼ 乗っ取り中のキャラクターから音量を取得するロジックを追加 ▼▼▼
        float finalVolume = this.volume; // まずは基本の音量を設定

        // もし乗っ取り中で、足音タイプの音なら
        if (playerController != null && playerController.IsPossessing() && this.soundType == SoundType.PlayerFootstep)
        {
            // 乗っ取っているNPCのStatusManagerを取得
            StatusManager possessedStatus = playerController.GetPossessedStatusManager();
            if (possessedStatus != null)
            {
                // そのNPCに設定されている足音の大きさを採用する
                finalVolume = possessedStatus.footstepVolume;
            }
        }
        // ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲

        // 最終的に決まった音量でメッセージを送信
        MessageBroker.Default.Publish(new SoundPacket(transform.position, finalVolume, soundType));
        Debug.Log($"<color=lightblue>音を発生させました: {soundType} at {transform.position} with volume {finalVolume}</color>");
    }
}