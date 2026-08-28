Shader "JH/UI/VertexAndUVFlow"
{
    // 定义图标纹理、流动外观和UGUI裁剪需要的逐材质参数。
    Properties
    {
        // 保留CanvasRenderer为每个Graphic写入的Sprite纹理。
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        // 使用标准UGUI颜色作为Sprite纹理和顶点颜色的附加乘数。
        _Color ("Tint", Color) = (1, 1, 1, 1)

        // 隐藏旧版终点UV属性，仅用于既有材质序列化兼容，不再参与运行时计算。
        [HideInInspector] _EndPointUV ("Legacy End Point UV", Vector) = (0, 0, 0, 0)

        // 隐藏旧版逐材质进度属性，当前进度由每个Graphic通过uv0.z独立传入。
        [HideInInspector] _EffectAmount ("Legacy Effect", Range(0, 1)) = 0

        // 控制两组屏幕空间波之间的交叉流动强度。
        _TwistIntensity ("Twist Intensity", Range(0, 2)) = 0.55

        // 控制屏幕空间流场在单位屏幕高度上的重复次数。
        _SinFrequency ("Flow Frequency", Range(0.5, 12)) = 4.5

        // 控制Sprite纹理的最大UV折射距离，默认值保持克制以防止图集串色。
        _SinAmplitude ("Refraction Amplitude", Range(0, 0.02)) = 0.004

        // 控制流场相位随动效进度推进的速度。
        _FlowSpeed ("Flow Speed", Range(0, 4)) = 1.15

        // 控制原始纹理和折射纹理之间的混合比例。
        _RefractionBlend ("Refraction Blend", Range(0, 1)) = 0.58

        // 控制流场对图标亮度产生的轻微起伏，零表示完全关闭。
        _FlowHighlight ("Flow Highlight", Range(0, 0.15)) = 0.035

        // 控制动效后段开始淡出的进度位置。
        _FadeStart ("Fade Start", Range(0, 0.98)) = 0.68

        // 控制尾段淡出的缓急程度，大于一时会保留更久的可辨认图标。
        _FadePower ("Fade Power", Range(0.5, 4)) = 1.35

        // 以下属性与UGUI默认Shader保持一致，用于支持Mask组件的Stencil裁剪。
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255

        // 允许CanvasRenderer按UGUI规则控制颜色通道写入。
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15

        // 允许需要硬Alpha边缘的UI元素启用标准裁剪关键字。
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // 允许Canvas根据当前渲染模式写入正确的UI深度测试方式。
        [HideInInspector] unity_GUIZTestMode ("ZTest Mode", Float) = 4
    }

    // 声明一个适用于透明UGUI图形的SubShader。
    SubShader
    {
        // 使用UGUI标准透明标签，并允许Sprite Atlas参与批处理。
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        // 将Mask组件写入材质的参数转交给GPU Stencil状态。
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        // UI图标不参与光照，也不需要写入深度或背面裁剪。
        Cull Off
        Lighting Off
        ZWrite Off

        // 使用Canvas提供的深度测试模式，并保持标准直通Alpha混合。
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        // 绘制CPU已经生成并细分完成的UI曲面。
        Pass
        {
            // 在Frame Debugger中使用清晰名称标识顶点和UV流动绘制。
            Name "VertexAndUVFlow"

            // 开始声明跨内置管线与URP兼容的HLSL程序。
            HLSLPROGRAM

            // 桌面独显目标允许使用稳定的屏幕空间三角函数流场。
            #pragma target 3.0

            // 指定当前Pass的顶点与片元入口。
            #pragma vertex Vertex
            #pragma fragment Fragment

            // 编译UGUI矩形裁剪和Alpha裁剪的局部变体。
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            // 引入Unity通用坐标变换、屏幕投影和立体渲染宏。
            #include "UnityCG.cginc"

            // 声明CanvasRenderer为每个UI元素提供的Sprite纹理。
            sampler2D _MainTex;

            // 由Unity自动写入Sprite纹理尺寸，用于将折射限制在图集安全像素范围内。
            float4 _MainTex_TexelSize;

            // 将逐材质参数集中到同一常量缓冲区，保持SRP Batcher兼容布局。
            CBUFFER_START(UnityPerMaterial)
                // 保存UGUI颜色乘数。
                half4 _Color;

                // 保存Alpha8等特殊Sprite纹理需要叠加的采样修正值。
                half4 _TextureSampleAdd;

                // 保存交叉流场混合强度。
                float _TwistIntensity;

                // 保存屏幕空间波形频率。
                float _SinFrequency;

                // 保存最大UV折射幅度。
                float _SinAmplitude;

                // 保存流场相位推进速度。
                float _FlowSpeed;

                // 保存原始纹理和折射纹理的混合比例。
                float _RefractionBlend;

                // 保存流体高光的最大亮度变化。
                float _FlowHighlight;

                // 保存尾段淡出的起始进度。
                float _FadeStart;

                // 保存尾段淡出的幂指数。
                float _FadePower;

                // 保存RectMask2D当前传入的裁剪矩形。
                float4 _ClipRect;

                // 保存RectMask2D水平方向的软边宽度。
                float _UIMaskSoftnessX;

                // 保存RectMask2D垂直方向的软边宽度。
                float _UIMaskSoftnessY;
            CBUFFER_END

            // 描述CanvasRenderer传入顶点Shader的数据。
            struct Attributes
            {
                // 保存CPU高细分曲面的对象空间顶点位置。
                float4 positionOS : POSITION;

                // 保存Graphic颜色和CanvasRenderer顶点颜色。
                half4 color : COLOR;

                // xy保存Sprite纹理坐标，z保存当前Graphic独立的流动进度。
                float4 uv : TEXCOORD0;

                // 声明GPU Instancing需要的实例ID。
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 描述顶点Shader传递给片元Shader的数据。
            struct Varyings
            {
                // 保存光栅化阶段需要的裁剪空间位置。
                float4 positionCS : SV_POSITION;

                // 保存已经叠加材质Tint的最终顶点颜色。
                half4 color : COLOR;

                // xy保存Sprite纹理坐标，z保存无需逐材质实例即可插值的流动进度。
                float3 uv : TEXCOORD0;

                // 保存RectMask2D软裁剪计算需要的数据。
                half4 mask : TEXCOORD1;

                // 保存由裁剪空间计算的屏幕投影坐标。
                float4 screenPosition : TEXCOORD2;

                // 声明单通道立体渲染需要的眼睛索引。
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // 将CPU高细分曲面转换到裁剪空间并准备UGUI Mask数据。
            Varyings Vertex(Attributes input)
            {
                // 创建零初始化的输出，避免未使用语义产生未定义值。
                Varyings output = (Varyings)0;

                // 初始化当前UI实例对应的GPU Instancing状态。
                UNITY_SETUP_INSTANCE_ID(input);

                // 初始化单通道立体渲染输出数据。
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // CPU已经完成曲面形变，GPU只负责标准对象到裁剪空间变换。
                output.positionCS = UnityObjectToClipPos(input.positionOS);

                // 从裁剪空间位置生成透视正确的屏幕投影坐标。
                output.screenPosition = ComputeScreenPos(output.positionCS);

                // 保留Sprite UV并把CPU写入的独立进度传递到当前Graphic的片元。
                output.uv = input.uv.xyz;

                // 将材质Tint与CanvasRenderer顶点颜色合并。
                output.color = input.color * _Color;

                // 根据投影矩阵估算当前UI顶点覆盖的屏幕像素尺寸。
                float2 pixelSize = output.positionCS.w;

                // 将裁剪空间像素尺寸转换为RectMask2D软边计算尺度。
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                // 限制极端裁剪矩形，避免无Mask状态下出现浮点溢出。
                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);

                // 预计算当前位置相对于裁剪矩形中心和软边宽度的数据。
                output.mask = half4(
                    input.positionOS.xy * 2.0 - clampedRect.xy - clampedRect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                // 返回已经准备完成的UI顶点输出。
                return output;
            }

            // 根据屏幕位置和动效进度计算连续、有限的二维流场。
            float2 CalculateScreenFlow(float2 screenUV, float progress)
            {
                // 按屏幕高宽比校正横向坐标，避免宽屏上波形被拉扁。
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);

                // 将流场锚定在屏幕空间，使CPU曲面移动时纹理扰动不会附着在单一三角形上。
                float2 screenCoordinate = float2(screenUV.x * aspect, screenUV.y);

                // 对旧材质中的极端频率做运行时保护，避免历史参数重新产生高频闪烁。
                float frequency = clamp(_SinFrequency, 0.5, 12.0);

                // 将动效进度转换为可连续推进的周期相位。
                float phase = progress * UNITY_TWO_PI * clamp(_FlowSpeed, 0.0, 4.0);

                // 第一组斜向波提供主要横向流动。
                float primaryWave = sin(
                    (screenCoordinate.y + screenCoordinate.x * 0.37) * frequency * UNITY_TWO_PI + phase);

                // 第二组不同方向和速度的波避免形成规则的棋盘纹理。
                float secondaryWave = cos(
                    (screenCoordinate.x - screenCoordinate.y * 0.29) * frequency * UNITY_TWO_PI - phase * 0.83);

                // 交叉波提供少量旋转感，但强度始终受到兼容参数限制。
                float crossWave = sin(
                    dot(screenCoordinate, float2(0.79, 1.13)) * frequency * UNITY_TWO_PI + phase * 1.27);

                // 将旧Twist参数重新解释为克制的交叉流比例，而非大范围UV放射距离。
                float crossStrength = clamp(_TwistIntensity, 0.0, 2.0) * 0.22;

                // 合成二维流向并限制长度，保证任意屏幕位置的折射幅度都有确定上界。
                float2 flow = float2(primaryWave, secondaryWave) +
                              float2(crossWave, -crossWave) * crossStrength;

                // 只在流向长度超过一时归一，保留低能区域的柔和变化。
                return flow / max(1.0, length(flow));
            }

            // 输出保留图标主体、应用屏幕空间微折射并符合UGUI裁剪约定的颜色。
            half4 Fragment(Varyings input) : SV_Target
            {
                // 恢复单通道立体渲染当前片元对应的眼睛索引。
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 限制当前Graphic顶点通道中的独立进度，合批后不同元素仍然互不串值。
                float progress = saturate(input.uv.z);

                // 通过插值后的投影坐标恢复当前片元对应的归一化屏幕坐标。
                float2 screenUV = input.screenPosition.xy / max(input.screenPosition.w, 0.00001);

                // 将屏幕坐标转换为当前XR眼睛对应的纹理区域。
                screenUV = UnityStereoTransformScreenSpaceTex(screenUV);

                // 读取未经扰动的Sprite，确保主体轮廓和图集区域始终作为稳定基准。
                half4 originalSample = tex2D(_MainTex, input.uv.xy) + _TextureSampleAdd;

                // 使用正弦包络使折射在起点和终点都自然归零，不留下突然跳变的尾帧。
                float flowEnvelope = sin(progress * UNITY_PI);

                // 透明边缘不进行UV位移，减少Sprite Atlas相邻图块串色和黑边。
                float opaqueCore = smoothstep(0.03, 0.35, originalSample.a);

                // 根据当前片元屏幕位置取得稳定、连续的二维流向。
                float2 flow = CalculateScreenFlow(screenUV, progress);

                // 对历史材质中的超大振幅做硬上限保护，彻底避免跨越整张纹理的放射采样。
                float amplitude = clamp(_SinAmplitude, 0.0, 0.02);

                // 最多移动两个纹理像素，兼容高分辨率Sprite Atlas的边距和挤出区域。
                float2 safeAmplitude = min(
                    amplitude.xx,
                    abs(_MainTex_TexelSize.xy) * 2.0);

                // 只在图标实体内部应用小范围折射，并在动效两端自动归零。
                float2 refractedUV =
                    input.uv.xy + flow * safeAmplitude * flowEnvelope * opaqueCore;

                // 读取受到屏幕空间流场轻微折射后的Sprite颜色。
                half4 refractedSample = tex2D(_MainTex, refractedUV) + _TextureSampleAdd;

                // 原始图标始终保留一部分权重，避免折射破坏工具轮廓和细节辨识度。
                float refractionWeight = saturate(_RefractionBlend) * flowEnvelope * opaqueCore;
                half4 spriteSample = lerp(originalSample, refractedSample, refractionWeight);

                // 将流场变化转化为幅度很小的亮度起伏，补充柔和液体高光感。
                float highlight = 1.0 + flow.x * clamp(_FlowHighlight, 0.0, 0.15) *
                                  flowEnvelope * opaqueCore;

                // 应用Sprite采样、顶点颜色和材质Tint。
                half4 color = spriteSample * input.color;
                color.rgb *= highlight;

                // 将淡出起点限制在有效范围，并为尾段保留非零计算区间。
                float fadeStart = clamp(_FadeStart, 0.0, 0.98);
                float fadeProgress = saturate((progress - fadeStart) / max(1.0 - fadeStart, 0.0001));

                // 使用可调幂曲线让高细分曲面在收束末端柔和消失，不再依赖UV硬裁切。
                float endFade = pow(fadeProgress, clamp(_FadePower, 0.5, 4.0));
                color.a *= 1.0 - endFade;

                // 启用RectMask2D时计算水平和垂直方向的软裁剪系数。
                #ifdef UNITY_UI_CLIP_RECT
                    half2 maskFactor = saturate(
                        (_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);

                    // 将两个方向的软裁剪系数应用到最终透明度。
                    color.a *= maskFactor.x * maskFactor.y;
                #endif

                // 启用Alpha Clip时丢弃完全透明的UI片元。
                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif

                // 返回符合UGUI标准透明混合约定的直通Alpha颜色。
                return color;
            }

            // 结束HLSL程序。
            ENDHLSL
        }
    }
}
