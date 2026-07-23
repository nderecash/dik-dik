// Inverted hull outline.
//
// Draws the mesh a second time, pushed outward along its normals, with front faces
// culled so only the parts sticking out behind the real mesh survive. The result is a
// solid band around the silhouette.
//
// Chosen over post-processing edge detection because this project uses the built-in
// render pipeline and a post effect would mean pulling in a whole stack for one level.
// This is a forty line shader that works everywhere, including WebGL.
//
// Used by Level 3, where the night side removes the bright sky the rest of the game's
// art direction depends on. High contrast turns these on and gives edges back.

Shader "Dikdik/HazardOutline"
{
    Properties
    {
        _Color ("Outline Colour", Color) = (1, 1, 1, 1)
        _Width ("Outline Width", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        // Geometry+1 so the outline sorts after the object it belongs to.
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+1" }

        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 pushed = v.vertex.xyz + normalize(v.normal) * _Width;
                o.pos = UnityObjectToClipPos(float4(pushed, 1.0));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
