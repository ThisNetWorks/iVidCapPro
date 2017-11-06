
Shader "Custom/ividcappro" {

	Properties {
		_MainTex ("", 2D) = "white" {}
		_CornerX1 ("CornerX1", Range(0,1)) = 0.0
		_CornerX2 ("CornerX2", Range(0,1)) = 1.0
		_CornerY1 ("CornerY1", Range(0,1)) = 0.0
		_CornerY2 ("CornerY2", Range(0,1)) = 0.5
	}
 
	SubShader {
 
		ZTest Always Cull Off ZWrite Off Fog { Mode Off } //Rendering settings
 
		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc" 
			//we include "UnityCG.cginc" to use the appdata_img struct
    
			struct v2f {
				float4 pos : POSITION;
				half2 uv : TEXCOORD0;
			};
   
			v2f vert (appdata_img v){
   				v2f o;
   				o.pos = UnityObjectToClipPos (v.vertex);
   				o.uv = MultiplyUV (UNITY_MATRIX_TEXTURE0, v.texcoord.xy);
   				return o; 
  			}
    
  			sampler2D _MainTex; //Reference in Pass is necessary to let us use this variable in shaders
  			float _CornerX1;
  			float _CornerX2;
  			float _CornerY1;
  			float _CornerY2;
    
			fixed4 frag (v2f i) : COLOR {

				float tx = lerp(_CornerX1, _CornerX2, i.uv.x);
				float ty = lerp(1.0 - _CornerY1, 1.0 - _CornerY2, 1.0 - i.uv.y);
				fixed4 col = tex2D(_MainTex, fixed2(tx, ty));
				return col;
			}
  			ENDCG
		}
	} 
	FallBack "Diffuse"
}