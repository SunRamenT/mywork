using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BallSound : MonoBehaviour
{
    private AudioSource audioSource;

    private void Awake()
    {
        // AudioSourceコンポーネントを取得
        audioSource = GetComponent<AudioSource>();
        if (audioSource.clip == null)
        {
            Debug.LogWarning("AudioClipが設定されていません。Inspectorで設定してください。");
        }
    }

    // Triggerに何かが入ったとき
    private void OnTriggerEnter(Collider other)
    {
        // もし音が設定されていれば再生
        if (audioSource.clip != null)
        {
            audioSource.Play();
        }

        // 必要ならどのオブジェクトが入ったか確認
        // Debug.Log(other.name + "がTriggerに入りました");
    }
}
