using UnityEngine;

public class UnlockManager : MonoBehaviour
{
    public static UnlockManager Instance { get; private set; }

    // PlayerPrefsに保存する時のキーの接頭辞
    private const string UnlockKeyPrefix = "Character_Unlocked_";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 指定されたIDのキャラクターをアンロック状態にして保存する
    /// </summary>
    public void UnlockCharacter(string characterID)
    {
        string key = UnlockKeyPrefix + characterID;
        PlayerPrefs.SetInt(key, 1); // 1 = true (アンロック済み)
        PlayerPrefs.Save();
        Debug.Log($"キャラクター「{characterID}」がアンロックされました！");
    }

    /// <summary>
    /// 指定されたIDのキャラクターがアンロックされているか確認する
    /// </summary>
    public bool IsCharacterUnlocked(string characterID)
    {
        string key = UnlockKeyPrefix + characterID;
        // PlayerPrefsにキーが存在し、その値が1ならアンロック済み
        return PlayerPrefs.GetInt(key, 0) == 1;
    }
}