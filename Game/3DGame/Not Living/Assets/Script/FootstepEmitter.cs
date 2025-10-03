using UnityEngine;
using UniRx;

public class FootstepEmitter : MonoBehaviour
{
    private StatusManager statusManager;
    private NPCMove npcMove;

    private void Awake()
    {
        // 自分のキャラクターが持つコンポーネントを取得
        statusManager = GetComponent<StatusManager>();
        npcMove = GetComponent<NPCMove>();
    }

    /// <summary>
    /// このメソッドを、NPCの歩行アニメーションのイベントから呼び出す
    /// </summary>
    public void EmitFootstepSound()
    {
        // StatusManagerやNPCMoveが見つからない、または乗っ取られていない場合は何もしない
        if (statusManager == null || npcMove == null || !npcMove.isNottoried)
        {
            return;
        }

        // StatusManagerから、このキャラクター固有の足音の大きさを取得
        float volume = statusManager.footstepVolume;

        // MessageBrokerに、足音が発生したことを通知する
        MessageBroker.Default.Publish(new SoundPacket(transform.position, volume, SoundType.PlayerFootstep));
        
        // デバッグ用にログを表示
        //Debug.Log($"<color=lightblue>{gameObject.name} が足音を発生させました (大きさ: {volume})</color>");
    }
}