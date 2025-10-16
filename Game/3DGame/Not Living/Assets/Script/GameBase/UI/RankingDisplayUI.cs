using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI; // Scrollbarを使うために必要
using System.Collections; // Coroutineを使うために必要

public class RankingDisplayUI : MonoBehaviour
{
    [Header("UI参照")]
    [Tooltip("ランキングの各行が生成される親オブジェクト")]
    public Transform listContainer;

    [Tooltip("ランキング一行分のUIプレハブ")]
    public GameObject rankingRowPrefab;
    [Tooltip("制御するスクロールバー")]
    public Scrollbar scrollBar;

    [Header("API")]
    [Tooltip("ApiManagerがアタッチされたGameObject")]
    public ApiManager apiManager;

    private void OnEnable()
    {
        // ApiManagerからのデータ受信イベントを購読
        ApiManager.OnRankingDataReceived += UpdateUI;
    }

    private void OnDisable()
    {
        // イベントの購読を解除
        ApiManager.OnRankingDataReceived -= UpdateUI;
    }

    private void Start()
    {
        // 起動時にランキング取得をリクエスト
        if (apiManager != null)
        {
            apiManager.GetRanking();
        }
    }

    private void UpdateUI(List<RankingEntry> rankingList)
    {
        // 既存のランキング表示を全て削除
        foreach (Transform child in listContainer)
        {
            Destroy(child.gameObject);
        }

        // 上位10件までを表示
        int rankCount = Mathf.Min(rankingList.Count, 10);
        for (int i = 0; i < rankCount; i++)
        {
            // プレハブを生成
            GameObject rowObject = Instantiate(rankingRowPrefab, listContainer);
            RankingRowUI rowUI = rowObject.GetComponent<RankingRowUI>();

            // データをセット
            rowUI.SetData(i + 1, rankingList[i]);
        }
        // UI更新後にコルーチンを開始
        StartCoroutine(ResetScrollPositionCoroutine());
    }
    // スクロール位置を一番上にリセットするメソッド
    private IEnumerator ResetScrollPositionCoroutine()
    {
        // UIのレイアウトが更新されるのを1フレーム待つ
        yield return new WaitForEndOfFrame();

        // スクロールバーの位置を一番上(1)に設定
        if (scrollBar != null)
        {
            scrollBar.value = 1f;
        }
    }
}