Shader "VRSYS/Recording Samples/Display"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (0.06,0.8,0.86,1)
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 position : SV_POSITION; float3 normal : TEXCOORD0; float3 world : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float4 _BaseColor;
            float4 _EmissionColor;
            Varyings vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                UNITY_INITIALIZE_OUTPUT(Varyings,o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.position = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.world = mul(unity_ObjectToWorld,v.vertex).xyz;
                return o;
            }
            fixed4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 normal = normalize(i.normal);
                float3 view = normalize(_WorldSpaceCameraPos.xyz-i.world);
                float3 key = normalize(float3(-0.4,0.8,-0.6));
                float diffuse = 0.35 + 0.65 * saturate(dot(normal,key));
                float highlight = 0.18 * pow(saturate(dot(normal,normalize(key+view))),32);
                float rim = 0.06 * pow(1-saturate(dot(normal,view)),3);
                return fixed4(_BaseColor.rgb*diffuse + highlight + rim + _EmissionColor.rgb,1);
            }
            ENDCG
        }
    }
}
