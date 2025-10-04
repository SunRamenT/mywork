using UnityEngine;
using UnityEngine.UI;
using UniRx; // ★ UniRxを使う
using System;

[RequireComponent(typeof(CanvasRenderer))]
public class ECGWaveform : Graphic
{
    [Header("波形設定")]
    public int resolution = 100;
    public float amplitude = 20f;
    public float decaySpeed = 3f;
    public float lineThickness = 2f;

    private float[] waveData;
    private float currentSpike = 0f;

    protected override void Awake()
    {
        base.Awake();
        waveData = new float[resolution];
    }

    protected new void Start()
    {
        // SoundPacket を購読
        MessageBroker.Default
            .Receive<SoundPacket>()
            .Subscribe(packet => OnSoundHeard(packet))
            .AddTo(this); // このUIが破棄されると自動で購読解除
    }

    private void OnSoundHeard(SoundPacket packet)
    {
        // プレイヤー関連の音だけを波形に反映
        if (packet.Type == SoundType.PlayerFootstep || packet.Type == SoundType.PlayerAction)
        {
            currentSpike += packet.Volume; // 音量を加算

            // 波形全体の色を音量で段階的に変更
            if (packet.Volume < 5f)
            {
                color = Color.green;
            }
            else if (packet.Volume < 10f)
            {
                color = Color.yellow;
            }
            else
            {
                color = Color.red;
            }
        }
    }


    void Update()
    {
        // データを左にシフト
        for (int i = 0; i < resolution - 1; i++)
            waveData[i] = waveData[i + 1];

        // 最新のスパイクを追加
        waveData[resolution - 1] = currentSpike;

        // 減衰
        currentSpike = Mathf.Lerp(currentSpike, 0f, Time.deltaTime * decaySpeed);

        // 再描画リクエスト
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;

        for (int i = 0; i < resolution - 1; i++)
        {
            float x1 = (i / (float)(resolution - 1)) * width;
            float y1 = waveData[i] * amplitude + height * 0.5f;
            float x2 = ((i + 1) / (float)(resolution - 1)) * width;
            float y2 = waveData[i + 1] * amplitude + height * 0.5f;

            DrawLine(vh, new Vector2(x1, y1), new Vector2(x2, y2), lineThickness, color);
        }
    }

    private void DrawLine(VertexHelper vh, Vector2 p1, Vector2 p2, float thickness, Color c)
    {
        Vector2 dir = (p2 - p1).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x) * thickness * 0.5f;

        UIVertex v0 = UIVertex.simpleVert; v0.color = c; v0.position = p1 - normal;
        UIVertex v1 = UIVertex.simpleVert; v1.color = c; v1.position = p1 + normal;
        UIVertex v2 = UIVertex.simpleVert; v2.color = c; v2.position = p2 + normal;
        UIVertex v3 = UIVertex.simpleVert; v3.color = c; v3.position = p2 - normal;

        int idx = vh.currentVertCount;
        vh.AddVert(v0); vh.AddVert(v1); vh.AddVert(v2); vh.AddVert(v3);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }
}
