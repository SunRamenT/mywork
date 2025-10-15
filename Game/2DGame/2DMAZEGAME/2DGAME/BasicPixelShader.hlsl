// BasicPixelShader.hlsl

struct PSInput
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
};

float4 BasicPS(PSInput input) : SV_TARGET
{
    // C++から渡された色（input.color）を使ってピクセルを塗る
    return input.color;
}