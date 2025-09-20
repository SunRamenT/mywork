Shader "Custom/InsideBlackShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        // パス1: オブジェクトの表側を普通に描画する
        Pass
        {
            Cull Back // 裏面をカリング（非表示に）

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                return col;
            }
            ENDCG
        }

        // パス2: オブジェクトの裏側を真っ黒に描画する
        Pass
        {
            Cull Front // 表面をカリング（非表示に）

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct vertInput {
                float4 pos : POSITION;
            };

            struct vertOutput {
                float4 pos : SV_POSITION;
            };

            vertOutput vert(vertInput input) {
                vertOutput o;
                o.pos = UnityObjectToClipPos(input.pos);
                return o;
            }

            float4 frag(vertOutput output) : SV_Target {
                return float4(0, 0, 0, 1); // 真っ黒を返す
            }
            ENDCG
        }
    }
}