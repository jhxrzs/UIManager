Shader "JH/UI/TwistUIEffect"
{
    Properties
    {
        [HideInInspector] _MainTex ("Texture", 2D) = "white" {}
        _EndScreenPoint("EndScreenPoint", Vector) = (0,0,0,0)
        _EffectAmount("Effect",Range(0,1)) = 0
        _TwistIntensity("TwistIntensity",Range(0,3)) = 1.65
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
        }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                half2 uv : TEXCOORD0;
            };

            struct v2f
            {
                half2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;

            cbuffer UnityPerMaterial
            {
                half4 _MainTex_ST;
                half4 _EndScreenPoint;
                half _EffectAmount;
                half _TwistIntensity;
            }

            float4x4 unity_CameraInvView;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                //记录原始UV
                float2 originalUV = i.uv;

                // 进行三角函数偏移
                half2 sinOffset = sin(i.uv * _EffectAmount * 8) * 0.1;

                // UV首次偏移
                i.uv += float2(sinOffset.y, sinOffset.x);

                // 偏移向量
                half2 offset = normalize(originalUV - _EndScreenPoint.xy / _ScreenParams.xy);

                //进行偏移
                i.uv += offset * _EffectAmount * _TwistIntensity;

                // 采样
                half4 col = tex2D(_MainTex, i.uv);

                //计算alpha值，下面的一堆嵌套等价于：
                // if (i.uv.x < 0 || i.uv.x > 1 || i.uv.y < 0 || i.uv.y > 1) col.a = 0;
                half alpha = step(0.0, i.uv.x) * step(0.0, i.uv.y) * step(i.uv.x, 1.0) * step(i.uv.y, 1.0);
                col.a *= alpha;

                return col;
            }
            ENDHLSL
        }
    }
}