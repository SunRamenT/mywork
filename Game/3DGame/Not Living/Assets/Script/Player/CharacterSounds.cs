using UnityEngine;
using UniRx; // UniRxを使うために必要

// このスクリプトはAudioSourceを直接使わなくなるので、[RequireComponent]は不要

public class CharacterSounds : MonoBehaviour
{
    [Header("特殊能力サウンド設定")]
    [Tooltip("特殊能力使用時に再生する音")]
    public AudioClip specialAbilitySound;
    [Tooltip("特殊能力の音が聞こえる半径")]
    public float specialAbilityVolume = 15f;

    [Header("着地サウンド設定")]
    
    [Tooltip("着地音が聞こえる半径")]
    public float landingVolume = 8f;

    // --- 音を実際に再生するためのAudioSource ---
    private AudioSource audioSource;

    private void Awake()
    {
        // AudioSourceを自分自身から取得、またはなければ追加する
        audioSource = GetComponent<AudioSource>();
    }

    /// <summary>
    /// PlayerControllerから、特殊能力使用時に呼び出す
    /// </summary>
    public void PlaySpecialAbilitySound()
    {
        // 1. 自分のスピーカーで音を鳴らす
        if (specialAbilitySound != null)
        {
            audioSource.PlayOneShot(specialAbilitySound);
        }

        // 2. Chaserに聞こえるように、音の情報をMessageBrokerで発信する
        MessageBroker.Default.Publish(new SoundPacket(transform.position, specialAbilityVolume, SoundType.PlayerAction));
        // デバッグ用にログを表示
        //Debug.Log($"<color=lightblue>{gameObject.name} が音を発生させました (大きさ: {specialAbilityVolume})</color>");
    }

    /// <summary>
    /// PlayerControllerから、着地時に呼び出す
    /// </summary>
    public void PlayLandingSound()
    {

        // 2. Chaserに聞こえるように、音の情報をMessageBrokerで発信する
        MessageBroker.Default.Publish(new SoundPacket(transform.position, landingVolume, SoundType.PlayerAction));
        //Debug.Log($"<color=lightblue>{gameObject.name} が音を発生させました (大きさ: {landingVolume})</color>");
    }
}