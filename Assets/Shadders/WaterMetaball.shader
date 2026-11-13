
Shader "Custom/WaterMetaball"
{
    Properties
    {
        _MainTex ("Water Texture", 2D) = "white" {}
        _WaterColor ("Water Tint", Color) = (1, 1, 1, 1)
        _Threshold ("Threshold", Range(0, 1)) = 0.25
        _Softness ("Edge Softness", Range(0.001, 0.3)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            fixed4 _WaterColor;
            float _Threshold;
            float _Softness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 pixel = _MainTex_TexelSize.xy;

                float density = 0.0;
                float totalWeight = 0.0;

                float3 colorSum = float3(0, 0, 0);
                float colorWeight = 0.0;

                // Blur both the water shape and its colors.
                for (int x = -3; x <= 3; x++)
                {
                    for (int y = -3; y <= 3; y++)
                    {
                        float2 offset = float2(x, y);

                        float weight = exp(
                            -dot(offset, offset) / 8.0
                        );

                        fixed4 sampleColor = tex2D(
                            _MainTex,
                            i.uv + offset * pixel
                        );

                        float contribution =
                            sampleColor.a * weight;

                        density += contribution;
                        totalWeight += weight;

                        // Preserve the original droplet colors.
                        colorSum +=
                            sampleColor.rgb * contribution;

                        colorWeight += contribution;
                    }
                }

                density /= max(totalWeight, 0.0001);

                // Merge nearby water droplets.
                float alpha = smoothstep(
                    _Threshold - _Softness,
                    _Threshold + _Softness,
                    density
                );

                // Blend blue, pink and intermediate colors.
                float3 finalColor =
                    colorSum / max(colorWeight, 0.0001);

                finalColor *= _WaterColor.rgb;

                return fixed4(
                    finalColor,
                    alpha * _WaterColor.a
                );
            }

            ENDCG
        }
    }

    Fallback Off
}
