// Level 4 only. Shades each round water drop like a small glossy bead:
// bright core, soft highlight and a slightly deeper rim, so a pool of drops
// reads as shiny water. The tint still comes from WaterDropVisual (calm -> foam).
Shader "Level4/WaterDrop"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _CoreBoost ("Core Brightness", Range(0, 1)) = 0.3
        _RimDarken ("Rim Darkening", Range(0, 1)) = 0.25
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
        half _CoreBoost;
        half _RimDarken;
    CBUFFER_END

    struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

    Varyings Vert(Attributes v)
    {
        Varyings o;
        UNITY_SETUP_INSTANCE_ID(v);
        SetUpSpriteInstanceProperties();
        v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
        o.positionCS = TransformObjectToHClip(v.positionOS);
        o.color = v.color * unity_SpriteColor;
        o.uv = v.uv;
        return o;
    }

    half4 Frag(Varyings i) : SV_Target
    {
        half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
        float r = length(i.uv - 0.5) * 2.0;             // 0 centre, 1 edge
        half3 col = i.color.rgb * (1.0 - _RimDarken * smoothstep(0.45, 1.0, r));
        col = lerp(col, half3(1, 1, 1), _CoreBoost * smoothstep(0.55, 0.0, r));
        return half4(col, alpha);
    }
    ENDHLSL

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
