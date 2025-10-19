using UnityEngine;
using UnityEngine.UI; // Buttonを使うために必要
using TMPro;          // TextMeshProUGUIを使うために必要
using System.Collections.Generic; // Listを使うために必要
using UnityEngine.SceneManagement;

// ステップ1で説明した「キャラクターの名簿」の設計図
[System.Serializable]
public class CharacterData
{
    public string characterName;
    public GameObject characterPrefab;
    [Tooltip("このキャラクターを識別するための一意のID（例: 'Ghost_B', 'Knight'）")]
    public string characterID;
    [Tooltip("チェックを入れると、このキャラクターはアンロックが必要になります")]
    public bool isLockedByDefault;
}

public class CharacterSelector : MonoBehaviour
{
    [Header("キャラクターリスト")]
    [Tooltip("選択可能なキャラクターのリスト")]
    public List<CharacterData> characterList;

    [Header("UI参照")]
    [Tooltip("キャラクター名を表示するテキスト")]
    public TextMeshProUGUI characterNameText;

    [Header("遷移設定")]
    [Tooltip("遷移先のゲームプレイシーン名")]
    public string gameSceneName = "DefaultArea";
    [Tooltip("キャラクターがロックされている時に表示するオブジェクト（「LOCKED」という文字など）")]
    public GameObject lockedOverlayObject;
    [Tooltip("決定ボタン。ロックされている時は無効化する")]
    public Button confirmButton;

    // 現在選択されているキャラクターのインデックス（リストの何番目か）
    private int currentIndex = 0;

    void Start()
    {
        // 最初のキャラクターを表示する
        UpdateCharacterDisplay();
    }

    /// <summary>
    /// 右ボタン（次へ）を押した時に呼び出される
    /// </summary>
    public void SelectNext()
    {
        // インデックスを1つ進める
        currentIndex++;
        
        // もしインデックスがリストの数を超えたら、最初に戻る（ループさせる）
        if (currentIndex >= characterList.Count)
        {
            currentIndex = 0;
        }

        UpdateCharacterDisplay();
    }

    /// <summary>
    /// 左ボタン（前へ）を押した時に呼び出される
    /// </summary>
    public void SelectPrevious()
    {
        // インデックスを1つ戻す
        currentIndex--;

        // もしインデックスが0より小さくなったら、一番最後に戻る（ループさせる）
        if (currentIndex < 0)
        {
            currentIndex = characterList.Count - 1;
        }

        UpdateCharacterDisplay();
    }

    /// <summary>
    // 真ん中のキャラクター名ボタン（決定）を押した時に呼び出される
    /// </summary>
    public void ConfirmSelection()
    {
        // 選択されたキャラクターのプレハブをGameDataManagerに保存
        CharacterData selectedChar = characterList[currentIndex];
        if (!selectedChar.isLockedByDefault || UnlockManager.Instance.IsCharacterUnlocked(selectedChar.characterID))
        {
            GameDataManager.Instance.SelectedCharacterPrefab = selectedChar.characterPrefab;
            Debug.Log($"{characterList[currentIndex].characterName} が選択されました。");
        }
        else
        {
            Debug.Log("GameDataManagerが見つからないか、キャラクターがリストにいません！");
            return;
        }
    }

    /// <summary>
    /// UIのテキストを現在のキャラクター名で更新する
    /// </summary>
    private void UpdateCharacterDisplay()
    {
        if (characterList.Count == 0) return;

        CharacterData currentCharacter = characterList[currentIndex];

        // アンロック状態に応じて表示を切り替えるロジック
        bool isUnlocked = !currentCharacter.isLockedByDefault || UnlockManager.Instance.IsCharacterUnlocked(currentCharacter.characterID);

        if (isUnlocked)
        {
            // --- アンロックされている場合 ---
            characterNameText.text = currentCharacter.characterName;
            if(lockedOverlayObject != null) lockedOverlayObject.SetActive(false);
            if(confirmButton != null) confirmButton.interactable = true; // ボタンを有効化
        }
        else
        {
            // --- ロックされている場合 ---
            characterNameText.text = "？？？"; // 名前を隠す
            if(lockedOverlayObject != null) lockedOverlayObject.SetActive(true);
            if(confirmButton != null) confirmButton.interactable = false; // ボタンを無効化
        }
    }
}