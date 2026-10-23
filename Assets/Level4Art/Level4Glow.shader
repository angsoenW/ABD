// Level 4 only. Soft rounded-rectangle glow drawn behind the target cup,
// with a slow, gentle pulse. Purely decorative (no collider).
Shader "Level4/SoftGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Glow Colour", Color) = (1, 0.85, 0.4, 0.25)
        _Softness ("Edge Softness", Range(0.05, 1)) = 0.6
        _Pulse ("Pulse Amount", Range(0, 0.5)) = 0.15
        _PulseSpeed ("Pulse Speed", Float) = 1.5
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

    CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        half _Softness;
        half _Pulse;
        float _PulseSpeed;
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
        float2 d = abs(i.uv - 0.5) * 2.0;               // 0 centre, 1 edge on each axis
        float edge = max(d.x, d.y);
        half a = 1.0 - smoothstep(1.0 - _Softness, 1.0, edge);
        a *= 1.0 + _Pulse * sin(_Time.y * _PulseSpeed);
        return half4(_Color.rgb * i.color.rgb, saturate(a * _Color.a * i.color.a));
    }
    ENDHLSL

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
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
