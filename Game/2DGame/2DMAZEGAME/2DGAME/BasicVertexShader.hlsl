// BasicVertexShader.hlsl を更新
cbuffer SceneConstantBuffer : register(b0)
{
    float4x4 projectionMatrix;
};

struct VSInput
{
    float4 pos : POSITION;
    // インスタンスごとのデータを入力として追加
    float3 instancePos : INSTANCE_POSITION;
    float4 instanceColor : INSTANCE_COLOR;
};

struct PSInput
{
    float4 pos : SV_POSITION;
    float4 color : COLOR;
};

PSInput BasicVS(VSInput input)
{
    PSInput result;

    // ワールド座標を計算
    float4 worldPos = float4(input.pos.xyz + input.instancePos, 1.0f);
    // 行列を使って最終的なスクリーン座標に変換 
    result.pos = mul(worldPos, projectionMatrix); // ← "pos" に修正
    
    result.color = input.instanceColor;
    return result;
}