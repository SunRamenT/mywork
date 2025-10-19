using UnityEngine;
using UnityEngine.AI;

public class PlayerSpawner : MonoBehaviour
{
    [Header("フォールバック設定")]
    [Tooltip("タイトルから選ばれなかった場合（直接シーンを再生した時など）に登場させるデフォルトのキャラクター")]
    public GameObject defaultCharacterPrefab;

    private void Awake()
    {
        GameObject characterToSpawn = null;

        // 共有メモ帳(GameDataManager)が存在し、キャラクターが選ばれているか確認
        if (GameDataManager.Instance != null && GameDataManager.Instance.SelectedCharacterPrefab != null)
        {
            // 選ばれたキャラクターを登場させる
            characterToSpawn = GameDataManager.Instance.SelectedCharacterPrefab;
            Debug.Log($"タイトルで選ばれた {characterToSpawn.name} をスポーンさせます。");
        }
        else
        {
            // 何も選ばれていなければ、デフォルトのキャラクターを登場させる（テスト時に便利）
            characterToSpawn = defaultCharacterPrefab;
            Debug.LogWarning($"キャラクターが選択されていなかったため、デフォルトの {characterToSpawn.name} をスポーンさせます。");
        }

        // 実際にキャラクターをインスタンス化（生成）する
        if (characterToSpawn != null)
        {
            // 1. キャラクターを生成し、そのオブジェクトを変数に保存する
            GameObject spawnedCharacter = Instantiate(characterToSpawn, transform.position, transform.rotation);

            // 2. 生成したキャラクターからNavMeshAgentコンポーネントを探す
            NavMeshAgent agent = spawnedCharacter.GetComponent<NavMeshAgent>();

            // 3. もしNavMeshAgentが付いていたら、速度を0にする
            if (agent != null)
            {
                agent.speed = 0f;
                Debug.Log($"{spawnedCharacter.name} のNavMeshAgentの速度を0に設定しました。");
            }
        }
        else
        {
            Debug.LogError("スポーンさせるキャラクターが設定されていません！");
        }

        // スポナー自身の役目は終わったので、自身を破棄する
        Destroy(gameObject);
    }
}