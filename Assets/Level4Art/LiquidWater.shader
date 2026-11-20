// Liquid look for the merged water render (replaces the look of MetaballWater.shader, same setup).
// Use it on the material in MetaballWater's "Water Material" field.
//   - Translucent body, so the background shows through the water
//   - Lighter near the surface, deeper blue further down
//   - Bright line along the top surface, light (not dark) rim
//   - Bubbles drifting up inside deeper water (procedural: no texture needed)
// Works in URP (it is a UI shader drawn on MetaballWater's full-screen image).
Shader "UI/LiquidWater"
{
    Properties
    {
        [PerRendererData] _MainTex ("Water Render", 2D) = "black" {}

        [Header(Shape)]
        _Cutoff ("Cutoff (blob size)", Range(0.05, 1.5)) = 0.6
        _Softness ("Edge Softness", Range(0.001, 0.2)) = 0.04

        [Header(Body)]
        _WaterColor ("Shallow Color (alpha = see-through)", Color) = (0.24, 0.66, 0.97, 0.72)
        _DeepColor ("Deep Color (alpha = see-through)", Color) = (0.07, 0.36, 0.82, 0.88)
        _DepthPixels ("Depth To Full Deep (px)", Range(4, 80)) = 28
        _DropColorAmount ("Use Drop Colors (foam)", Range(0, 1)) = 0.25

        [Header(Edges)]
        _RimColor ("Rim Color", Color) = (0.62, 0.88, 1, 1)
        _RimWidth ("Rim Width", Range(0.0, 0.4)) = 0.06
        _SurfaceColor ("Top Surface Color", Color) = (0.82, 0.96, 1, 1)
        _SurfaceThickness ("Top Surface Thickness (px)", Range(0, 12)) = 3

        [Header(Bubbles)]
        _BubbleColor ("Bubble Color (alpha = strength)", Color) = (0.85, 0.97, 1, 0.75)
        _BubbleDensity ("Bubble Density", Range(0, 1)) = 0.28
        _BubbleScale ("Bubble Grid (cells per screen height)", Range(4, 60)) = 22
        _BubbleSize ("Bubble Size", Range(0.1, 0.9)) = 0.45
        _BubbleSpeed ("Bubble Rise Speed", Range(0, 2)) = 0.35
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Cutoff, _Softness, _DepthPixels, _DropColorAmount, _RimWidth, _SurfaceThickness;
            float _BubbleDensity, _BubbleScale, _BubbleSize, _BubbleSpeed;
            float4 _WaterColor, _DeepColor, _RimColor, _SurfaceColor, _BubbleColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            bool IsWater(float2 uv) { return tex2D(_MainTex, uv).a >= _Cutoff; }

            float4 frag (v2f i) : SV_Target
            {
                float4 s = tex2D(_MainTex, i.uv);
                float a = s.a;                                    // summed blob strength

                float inside = smoothstep(_Cutoff, _Cutoff + _Softness, a);
                if (inside <= 0.0) discard;

                float2 texel = _MainTex_TexelSize.xy;

                // --- Depth: how much water is above this pixel (0 = at the surface, 1 = deep) ---
                float depth = 0.0;
                [unroll] for (int k = 1; k <= 6; k++)
                    depth += IsWater(i.uv + float2(0, texel.y * _DepthPixels * k / 6.0)) ? 1.0 : 0.0;
                depth /= 6.0;

                float4 body = lerp(_WaterColor, _DeepColor, depth);
                float3 dropCol = saturate(s.rgb / max(a, 0.001));          // keeps the foam from WaterDropVisual
                float3 col = lerp(body.rgb, body.rgb * dropCol * 1.6, _DropColorAmount);
                float alpha = body.a;

                // --- Light rim around the edge (Where's My Water style, not a dark outline) ---
                float core = smoothstep(_Cutoff, _Cutoff + _RimWidth, a);
                col = lerp(_RimColor.rgb, col, core);
                alpha = lerp(_RimColor.a, alpha, core);

                // --- Bright line on the top surface: no water a few pixels above ---
                float above = tex2D(_MainTex, i.uv + float2(0, texel.y * _SurfaceThickness)).a;
                float surface = 1.0 - smoothstep(_Cutoff, _Cutoff + _Softness, above);
                col = lerp(col, _SurfaceColor.rgb, surface * _SurfaceColor.a);
                alpha = max(alpha, surface * _SurfaceColor.a);

                // --- Bubbles rising inside deeper water ---
                float aspect = _MainTex_TexelSize.z / max(_MainTex_TexelSize.w, 1.0);
                float2 bp = float2(i.uv.x * aspect, i.uv.y) * _BubbleScale;
                bp.y -= _Time.y * _BubbleSpeed;
                float2 cell = floor(bp);
                float2 f = frac(bp);
                // (no branch here: fwidth must run for every pixel)
                float hasBubble = step(Hash(cell), _BubbleDensity);
                float2 c = 0.3 + 0.4 * float2(Hash(cell + 1.7), Hash(cell + 3.1));
                float r = _BubbleSize * 0.5 * (0.5 + 0.5 * Hash(cell + 5.3));
                float d = length(f - c);
                float w = max(fwidth(d) * 1.5, 0.01);
                float ring = (1.0 - smoothstep(r - w, r + w, d))                // inside the outer edge
                           * smoothstep(r * 0.7 - w, r * 0.7 + w, d);           // outside the inner edge
                float bubble = hasBubble * ring * smoothstep(0.35, 0.7, depth) * core;   // only in deeper water
                col = lerp(col, _BubbleColor.rgb, bubble * _BubbleColor.a);
                alpha = max(alpha, bubble * _BubbleColor.a);

                return float4(col, alpha * inside * i.color.a);
            }
            ENDCG
        }
    }
}
