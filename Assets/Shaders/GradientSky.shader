// A skybox that gives the game a real horizon instead of a flat clear colour.
//
// Dark space overhead fading to a dusty band at the horizon, a low sun, and a scatter
// of stars in the upper sky. It is the single biggest change from "prototype" to "place":
// the silhouette art depends on a bright horizon behind the black shapes, and this
// provides it while still reading as an airless sky.
//
// Pure procedural, no textures, so it costs nothing to ship and works in WebGL.

Shader "Dikdik/GradientSky"
{
    Properties
    {
        _Zenith ("Zenith Colour", Color) = (0.03, 0.04, 0.09, 1)
        _Horizon ("Horizon Colour", Color) = (0.55, 0.55, 0.6, 1)
        _Ground ("Below Horizon", Color) = (0.05, 0.05, 0.06, 1)
        _HorizonSharp ("Horizon Sharpness", Range(1, 12)) = 4
        _SunDir ("Sun Direction", Vector) = (0.3, 0.12, 1, 0)
        _SunColour ("Sun Colour", Color) = (1, 0.95, 0.85, 1)
        _SunSize ("Sun Size", Range(0.9, 0.9999)) = 0.995
        _StarAmount ("Star Amount", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Zenith, _Horizon, _Ground, _SunColour;
            float _HorizonSharp, _SunSize, _StarAmount;
            float4 _SunDir;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            // Cheap hash for the star field.
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float h = dir.y;

                // Sky gradient: horizon band up to the zenith, and a darker floor below.
                float up = saturate(pow(saturate(h), 1.0 / _HorizonSharp));
                fixed3 sky = lerp(_Horizon.rgb, _Zenith.rgb, up);
                sky = lerp(sky, _Ground.rgb, saturate(-h * 6.0));

                // Sun disk plus a soft glow.
                float3 sun = normalize(_SunDir.xyz);
                float d = dot(dir, sun);
                float disk = smoothstep(_SunSize, 1.0, d);
                float glow = pow(saturate(d), 200.0) * 0.5 + pow(saturate(d), 8.0) * 0.15;
                sky += _SunColour.rgb * (disk + glow);

                // Stars, only in the upper sky, thinning toward the horizon.
                if (h > 0.05)
                {
                    float3 cell = floor(dir * 300.0);
                    float s = hash(cell);
                    float star = step(1.0 - _StarAmount * 0.02, s) * saturate(h * 2.0);
                    sky += star;
                }

                return fixed4(sky, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
