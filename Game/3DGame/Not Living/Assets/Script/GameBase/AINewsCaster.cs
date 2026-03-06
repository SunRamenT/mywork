using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class AINewsCaster : MonoBehaviour
{
    // API仕様に合わせたクラス定義（v1betaのリクエストフォーマットに基づく）
    [System.Serializable]
    public class GeminiRequest
    {
        public List<Content> contents;
        public Content system_instruction; // キー名を仕様通りに変更
    }

    [System.Serializable]
    public class Content 
    { 
        public string role; // user, model, system など
        public List<Part> parts; 
    }

    [System.Serializable]
    public class Part { public string text; }

    [Header("設定")]
    // 実際にアクセス可能なモデル名に変更してください
    [SerializeField] private string modelName = "gemini-2.5-flash"; 
    
    [SerializeField] private string apiKey = ""; 

    private bool isGenerating = false;

    public void GenerateNews(Action<string> onSuccess, Action<string> onError)
    {
        if (isGenerating) return;
        
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("[AINewsCaster] APIキーが設定されていません。");
            onError?.Invoke("通信エラー: APIキー未設定");
            return;
        }

        StartCoroutine(PostNewsRequest(onSuccess, onError));
    }

    private IEnumerator PostNewsRequest(Action<string> onSuccess, Action<string> onError)
    {
        isGenerating = true;

        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}";
        string prompt = BuildPrompt();
        // 修正する箇所：AINewsCaster.cs の PostNewsRequest メソッド内

        // 修正案：@ を用いて改行を含む文字列をスッキリと記述し、指示を構造化（Markdown風）する
        string systemPrompt = @"You are a professional and cheerful news anchor in a ghost-themed survival game.
                        Generate a short, immersive news script (within 15 seconds) based on the provided world status.

                        [STRICT FORMATTING RULES]
                        1. The output MUST be in Japanese Hiragana, Katakana, and Arabic numerals (0-9) ONLY.
                        2. NEVER use Kanji, Katakana, or English alphabets.
                        3. DO NOT output any labels, prefixes, or titles like 'へっどらいん:', 'にゅーす:', or 'みだし:'. Start the news script immediately.

                        [WORLD STATE INTERPRETATION]
                        - Do NOT output the raw parameter names (e.g., 'ぜんあく', 'かおす', 'しすう', 'ぱらめーた') or their exact values.
                        - If Chaos Index > 0: Describe chaotic or paranormal events occurring in the town (街).
                        - If Chaos Index = 0: Describe the town as peaceful or quiet.
                        - If Good/Evil Index > 0: Describe the town's public order as degrading, dangerous, or hostile.
                        - If Good/Evil Index < 0: Describe the town's public order as safe and stable.

                        Translate the parameters into realistic situations without stating the numbers. 
                        Example format: 
                        1にちめ。さいきん、まちのちあんがわるくなっています。よるのおでかけはひかえてください。";
        // API仕様（v1beta）に則ったデータ構造の構築
        var requestBody = new GeminiRequest
        {
            contents = new List<Content> 
            { 
                new Content 
                { 
                    role = "user",
                    parts = new List<Part> { new Part { text = prompt } } 
                } 
            },
            system_instruction = new Content 
            { 
                role = "system", // システムインストラクションとして明示
                parts = new List<Part> { new Part { text = systemPrompt } } 
            }
        };

        string jsonPayload = JsonUtility.ToJson(requestBody);
        byte[] postData = Encoding.UTF8.GetBytes(jsonPayload);

        int maxRetries = 5;
        for (int i = 0; i < maxRetries; i++)
        {
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(postData);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string responseText = request.downloadHandler.text;
                    string newsOutput = ExtractTextFromResponse(responseText);
                    onSuccess?.Invoke(newsOutput);
                    isGenerating = false;
                    yield break;
                }
                else
                {
                    // 【重要】なぜ失敗したのか、サーバーからのレスポンスを必ず出力する
                    Debug.LogError($"[通信エラー {i+1}回目] {request.error}\nサーバー応答: {request.downloadHandler.text}");

                    if (i == maxRetries - 1)
                    {
                        onError?.Invoke("通信エラー: 全てのリトライに失敗しました。詳細はコンソールを確認してください。");
                    }
                    else
                    {
                        yield return new WaitForSeconds(Mathf.Pow(2, i));
                    }
                }
            }
        }

        isGenerating = false;
    }

    private string BuildPrompt()
    {
        // 1. 各Managerのインスタンスを安全に取得
        var tm = GameTimeManager.Instance;
        var am = AlignmentManager.Instance;

        // フェイルセーフ: Managerがない場合
        if (tm == null || am == null)
        {
            Debug.LogWarning("[AINewsCaster] Managerがロードされていません。テスト用の固定値を送信します。");
            return "【ワールド統計データ】\n- 経過日数: 1日目\n- 現在時刻: 12:00\n- 善悪指数: 0 (ステータス: 普通)\n- カオス指数: 0 (状況: 安定的)\n\nこのデータを元にニュースを作成してください。";
        }

        // 2. 値を取得
        int days = tm.daysSurvived;
        string timeStr = tm.GetTimeAsString();

        // 【修正箇所】AlignmentManagerがまだVector3を返しているため、x,y,zで取得します
        // x: 時間, y: 善悪, z: カオス (AlignmentManagerの定義に基づく)
        Vector3 alignment = am.CurrentAlignment; 
        float goodEvil = alignment.y;
        float chaos = alignment.z;

        // 3. 数値の定性的な解釈
        string orderStatus = goodEvil > 50 ? "治安崩壊（暴動寸前）" :
                             goodEvil > 10 ? "不穏（事件多発）" :
                             goodEvil > -10 ? "普通" : "極めて良好";

        string chaosStatus = chaos > 50 ? "超常現象が頻発" : 
                             chaos > 0 ? "奇妙な事件が発生" : "安定的";

        // 4. テキスト構築
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("【ワールド統計データ】");
        sb.AppendLine($"- 経過日数: {days}日目");
        sb.AppendLine($"- 現在時刻: {timeStr}");
        sb.AppendLine($"- 善悪指数: {goodEvil} (ステータス: {orderStatus})");
        sb.AppendLine($"- カオス指数: {chaos} (状況: {chaosStatus})");
        sb.AppendLine("\nこのデータを元に、街の住人に向けたニュースを1つ作成してください。");
        
        return sb.ToString();
    }

    private string ExtractTextFromResponse(string json)
    {
        try {
            int start = json.IndexOf("\"text\": \"") + 9;
            int end = json.IndexOf("\"", start);
            return json.Substring(start, end - start).Replace("\\n", "\n");
        } catch {
            return "ニュースのパースに失敗しました。";
        }
    }
}