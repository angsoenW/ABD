// Level 4 only. Unlit sprite/tilemap shader that replaces flat colours with a
// world-space vertical gradient, soft noise, faint strata and sparse pebbles.
// Colours come from the material (the tile/sprite colour is ignored, alpha is kept),
// so the shared tile assets used by Levels 1-3 are never touched.
Shader "Level4/Terrain"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ColorTop ("Top Colour", Color) = (1,1,1,1)
        _ColorBottom ("Bottom Colour", Color) = (0.5,0.5,0.5,1)
        _YTop ("Gradient Top Y", Float) = 3
        _YBottom ("Gradient Bottom Y", Float) = -8.25
        _NoiseScale ("Noise Scale", Float) = 1.5
        _NoiseAmp ("Noise Amount", Range(0, 0.5)) = 0.08
        _StrataAmp ("Strata Amount", Range(0, 0.3)) = 0
        _SpeckColor ("Pebble Colour", Color) = (1,1,1,1)
        _SpeckAmp ("Pebble Amount", Range(0, 1)) = 0
        _BevelAmp ("Bevel (sprite UV.y)", Range(0, 0.6)) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
        half4 _ColorTop;
        half4 _ColorBottom;
        float _YTop;
        float _YBottom;
        float _NoiseScale;
        half _NoiseAmp;
        half _StrataAmp;
        half4 _SpeckColor;
        half _SpeckAmp;
        half _BevelAmp;
    CBUFFER_END

    struct Attributes { float3 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 positionWS : TEXCOORD1; };

    float Hash(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    float ValueNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        float2 u = f * f * (3.0 - 2.0 * f);
        float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
        return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
    }

    Varyings Vert(Attributes v)
    {
        Varyings o;
        UNITY_SETUP_INSTANCE_ID(v);
        SetUpSpriteInstanceProperties();
        v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
        float3 ws = TransformObjectToWorld(v.positionOS);
        o.positionCS = TransformWorldToHClip(ws);
        o.positionWS = ws.xy;
        o.color = v.color * unity_SpriteColor;
        o.uv = v.uv;
        return o;
    }

    half4 Frag(Varyings i) : SV_Target
    {
        half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
        float2 p = i.positionWS;

        float t = saturate((p.y - _YBottom) / (_YTop - _YBottom));
        half3 col = lerp(_ColorBottom.rgb, _ColorTop.rgb, t);

        float n = ValueNoise(p * _NoiseScale) * 0.65 + ValueNoise(p * _NoiseScale * 2.7 + 17.0) * 0.35;
        col *= 1.0 + (n - 0.5) * 2.0 * _NoiseAmp;

        float band = sin((p.y + ValueNoise(p * 0.35) * 1.4) * 4.5);
        col *= 1.0 + band * _StrataAmp;

        float2 g = p * 3.0;
        float2 cell = floor(g);
        float2 jitter = float2(Hash(cell + 3.1), Hash(cell + 7.7)) - 0.5;
        float pebble = step(0.82, Hash(cell)) * smoothstep(0.17, 0.09, length(frac(g) - 0.5 - jitter * 0.45));
        col = lerp(col, _SpeckColor.rgb, pebble * _SpeckAmp);

        // Slabs only: lighter top face, darker underside.
        col *= 1.0 + _BevelAmp * (smoothstep(0.55, 1.0, i.uv.y) * 0.8 - smoothstep(0.45, 0.0, i.uv.y) * 0.6);

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
