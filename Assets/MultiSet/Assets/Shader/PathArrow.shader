﻿Shader "MultiSet/PathArrow"
{
    Properties
    {
        _MainTex ("Arrow Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _ArrowSize ("Arrow Size", Range(0.1, 10.0)) = 1.0
        _ArrowSpacing ("Arrow Spacing", Range(0.1, 10.0)) = 2.0
        _Intensity ("Intensity", Range(0.1, 5.0)) = 1.0
        _ScrollSpeed ("Scroll Speed", Range(-2.0, 2.0)) = 0.0
    }
    
    SubShader
    {
        Tags { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }
        
        LOD 100
        Lighting Off
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _ArrowSize;
            float _ArrowSpacing;
            float _Intensity;
            float _ScrollSpeed;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // Apply texture tiling and offset based on arrow size and spacing
                float2 tiling = float2(_MainTex_ST.x / _ArrowSize, _MainTex_ST.y);
                float2 offset = float2(_MainTex_ST.z + _Time.y * _ScrollSpeed, _MainTex_ST.w);
                
                // Calculate UV coordinates with proper tiling
                // The _ArrowSpacing controls how far apart each arrow is
                o.uv = v.uv * float2(_ArrowSpacing, 1.0) * tiling + offset;
                
                o.color = v.color * _Color;
                
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample texture with repeating pattern
                fixed4 col = tex2D(_MainTex, frac(i.uv)) * i.color * _Intensity;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
    }
    Fallback "Transparent/VertexLit"
}