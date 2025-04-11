Shader "JH/UI/VertexAndUVFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _EndPointUV("EndPointUV", Vector) = (0,0,0,0)
        _EffectAmount("Effect",Range(0,1)) = 0
        _TwistIntensity("TwistIntensity",Range(0,3)) = 1.65
        _SinFrequency("SinFrequency",Range(0,20)) = 8
        _SinAmplitude("SinAmplitude",Range(0,0.5)) = 0.1

        // UI Shader常用属性
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
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
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // UI需要颜色
                UNITY_VERTEX_INPUT_INSTANCE_ID // UI实例化支持
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR; // UI需要颜色
                float4 worldPosition : TEXCOORD1; // UI遮罩用
                UNITY_VERTEX_OUTPUT_STEREO // UI VR支持
            };

            sampler2D _MainTex;

            cbuffer UnityPerMaterial
            {
                float4 _MainTex_ST;
                float4 _EndPointUV;
                float _EffectAmount;
                float _TwistIntensity;
                float _SinFrequency;
                float _SinAmplitude;
                float4 _ClipRect;
            }

            float4x4 unity_CameraInvView;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;

                //重新计算裁剪空间坐标
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                //记录原始UV
                float2 originalUV = i.uv;

                // 进行三角函数偏移
                float2 sinOffset = sin(i.uv * _EffectAmount * _SinFrequency) * _SinAmplitude;

                // UV首次偏移
                i.uv += float2(sinOffset.y, sinOffset.x);

                // 偏移向量
                float2 offset = normalize(originalUV - _EndPointUV.xy);

                //进行偏移
                i.uv += offset * _EffectAmount * _TwistIntensity;

                // 采样并应用顶点颜色
                float4 col = tex2D(_MainTex, i.uv) * i.color;

                // 透明度变化
                col.a = smoothstep(col.a, 0, _EffectAmount);

                //计算alpha值，下面的一堆嵌套等价于：
                //if (i.uv.x < 0 || i.uv.x > 1 || i.uv.y < 0 || i.uv.y > 1) col.a = 0;
                float alpha = step(0.0, i.uv.x) * step(0.0, i.uv.y) * step(i.uv.x, 1.0) * step(i.uv.y, 1.0);
                col.a *= alpha;


                return col;
            }
            ENDHLSL
        }
    }
}