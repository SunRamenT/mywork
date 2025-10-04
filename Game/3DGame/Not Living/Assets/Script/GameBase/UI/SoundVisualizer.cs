using UnityEngine;
using UniRx; // UniRxを使うために必要
using System.Linq; // ToList()を使うために必要

[RequireComponent(typeof(LineRenderer))]
public class SoundVisualizer : MonoBehaviour
{
    [Header("波形設定")]
    [Tooltip("波形のデータ点の数（解像度）")]
    public int resolution = 100;
    [Tooltip("音量に対する波形の高さの倍率")]
    public float amplitude = 2.0f;
    [Tooltip("波形が自然に減衰していく速さ")]
    public float decaySpeed = 3.0f;
    [Tooltip("UIの横幅")]
    public float graphWidth = 4.0f;

    private LineRenderer lineRenderer;
    private float[] waveData;
    private float currentSpike = 0f; // 音による現在のスパイクの高さ

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        waveData = new float[resolution]; // データを保持する配列を初期化
        lineRenderer.positionCount = resolution; // LineRendererの頂点数を設定
    }

    void Start()
    {
        // MessageBrokerを購読して、SoundPacket型のメッセージを受け取る
        MessageBroker.Default
            .Receive<SoundPacket>()
            .Subscribe(packet => OnSoundHeard(packet))
            .AddTo(this); // このオブジェクトが破棄される時に自動で購読を終了
    }

    private void OnSoundHeard(SoundPacket packet)
    {
        // プレイヤーが出した音（足音やアクション音）にのみ反応する
        if (packet.Type == SoundType.PlayerFootstep || packet.Type == SoundType.PlayerAction)
        {
            // 受け取った音の大きさを、現在のスパイクの高さに加算する
            // これにより、複数の音が同時に鳴っても波形が反応する
            currentSpike += packet.Volume;
        }
    }

    void Update()
    {
        // --- データの更新 ---

        // 配列の全要素を1つ左にずらす（スクロールさせる）
        for (int i = 0; i < resolution - 1; i++)
        {
            waveData[i] = waveData[i + 1];
        }

        // 配列の末尾に、現在のスパイクの高さを新しいデータとして追加
        waveData[resolution - 1] = currentSpike;

        // スパイクの高さを時間経過で滑らかに減衰させる
        currentSpike = Mathf.Lerp(currentSpike, 0f, Time.deltaTime * decaySpeed);

        // --- 描画の更新 ---
        currentSpike = Mathf.Sin(Time.time * 10f) * 0.5f;

        // 配列のデータに基づいて、LineRendererの各頂点の位置を更新
        Vector3[] positions = new Vector3[resolution];
        for (int i = 0; i < resolution; i++)
        {
            // X座標：グラフの左端(0)から右端(graphWidth)まで
            // Y座標：波形のデータ(waveData)に高さ倍率(amplitude)を掛け合わせる
            float x = (float)i / (resolution - 1) * graphWidth;
            float y = waveData[i] * amplitude;
            positions[i] = new Vector3(x, y, 0);
        }
        lineRenderer.SetPositions(positions);
    }
}