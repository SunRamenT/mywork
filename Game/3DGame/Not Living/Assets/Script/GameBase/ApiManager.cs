using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text; // JSONをバイト配列に変換するために必要

//JSONの送受信に使うデータ構造を定義
[System.Serializable]
public class RankingEntry
{
    public string name;
    public int score;
}

public class ApiManager : MonoBehaviour
{
    private const string ApiBaseUrl = "https://feupsy.com";// ここを実際のAPIのベースURLに置き換える

    // --- ランキング取得 ---
    public void GetRanking()
    {
        StartCoroutine(GetRankingCoroutine());
    }

    private IEnumerator GetRankingCoroutine()
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(ApiBaseUrl + "/ranking"))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = webRequest.downloadHandler.text;
                Debug.Log("ランキング取得成功:\n" + jsonResponse);
            }
            else
            {
                Debug.LogError("ランキング取得失敗: " + webRequest.error);
            }
        }
    }

    // --- ランキング送信 ---
    public void PostRanking(string playerName, int score)
    {
        StartCoroutine(PostRankingCoroutine(playerName, score));
    }

    private IEnumerator PostRankingCoroutine(string playerName, int score)
    {
        // 送信するデータを作成
        RankingEntry entry = new RankingEntry { name = playerName, score = score };
        string json = JsonUtility.ToJson(entry);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        // POSTリクエストを作成
        using (UnityWebRequest webRequest = new UnityWebRequest(ApiBaseUrl + "/ranking", "POST"))
        {
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // リクエストを送信
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("スコア送信成功: " + webRequest.downloadHandler.text);
                // TODO: 送信成功時のUI表示など
            }
            else
            {
                Debug.LogError("スコア送信失敗: " + webRequest.error);
                // TODO: 送信失敗時のUI表示など
            }
        }
    }
}