// A screen on an avatar that shows whatever the world's video player is
// playing.
//
// An avatar cannot reach a world's video player, but many players publish
// their picture as a global texture named _Udon_VideoTex for exactly this
// kind of use (ProTV, VideoTXL and USharpVideo among them). Where a world
// sets it, this draws it, fitted inside the screen without stretching.
// Where no world does, or nothing is playing, it shows a test card instead,
// drawn from distance functions, so the screen never just sits black.
Shader "Wirenook/Video Screen"
{
    Properties
    {
        _Brightness ("Brightness", Range(0, 2)) = 1
        _Sky ("Test card, top", Color) = (0.353, 0.659, 0.863, 1)
        _Ice ("Test card, bottom", Color) = (0.843, 0.984, 1, 1)
        _Ink ("Test card, mark", Color) = (0.153, 0.078, 0.322, 1)
        _Aspect ("Screen width over height", Float) = 1.7778
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            // If a viewer blocks this avatar's shaders, show nothing: the
            // screen's dark back panel is behind it.
            "VRCFallback" = "Hidden"
        }

        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            // Set by the world's video player, if it has one that shares its picture.
            sampler2D _Udon_VideoTex;
            float4 _Udon_VideoTex_TexelSize;

            float _Brightness;
            fixed4 _Sky;
            fixed4 _Ice;
            fixed4 _Ink;
            float _Aspect;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float inside(float distance)
            {
                float edge = max(fwidth(distance), 0.0001);
                return saturate(0.5 - distance / edge);
            }

            float ellipse(float2 p, float2 centre, float2 radii)
            {
                float2 q = (p - centre) / radii;
                return (length(q) - 1.0) * min(radii.x, radii.y);
            }

            // The wirenook rabbit on a sky, with a row of colour bars under it.
            fixed3 testCard(float2 uv)
            {
                fixed3 colour = lerp(_Ice.rgb, _Sky.rgb, smoothstep(0.1, 0.95, uv.y));

                // In square units, so the mark is round on a wide screen.
                float2 p = float2((uv.x - 0.5) * _Aspect, uv.y - 0.56);
                float rabbit = min(length(p) - 0.15,
                                   min(ellipse(p, float2(-0.09, 0.25), float2(0.055, 0.15)),
                                       ellipse(p, float2(0.09, 0.25), float2(0.055, 0.15))));
                colour = lerp(colour, _Ink.rgb, inside(rabbit));

                if (uv.y < 0.14)
                {
                    float bar = floor(uv.x * 6.0);
                    fixed3 bars = bar < 1.0 ? fixed3(1.0, 0.0, 0.9)
                        : bar < 2.0 ? fixed3(0.451, 0.957, 1.0)
                        : bar < 3.0 ? fixed3(1.0, 0.976, 0.694)
                        : bar < 4.0 ? fixed3(0.725, 0.659, 1.0)
                        : bar < 5.0 ? fixed3(1.0, 0.4, 0.0)
                        : fixed3(0.212, 0.373, 0.776);
                    colour = bars;
                }

                return colour;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // An unset global texture is a tiny white square.
                bool playing = _Udon_VideoTex_TexelSize.z > 16.0;
                if (!playing) return fixed4(testCard(i.uv) * _Brightness, 1);

                // Fit the picture inside the screen: bars at the sides of a
                // narrow picture, above and below a wide one.
                float picture = _Udon_VideoTex_TexelSize.z / max(_Udon_VideoTex_TexelSize.w, 1.0);
                float2 scale = picture > _Aspect ? float2(1.0, picture / _Aspect) : float2(_Aspect / picture, 1.0);
                float2 uv = (i.uv - 0.5) * scale + 0.5;
                if (any(uv < 0.0) || any(uv > 1.0)) return fixed4(0, 0, 0, 1);
                return fixed4(tex2D(_Udon_VideoTex, uv).rgb * _Brightness, 1);
            }
            ENDCG
        }
    }
}
