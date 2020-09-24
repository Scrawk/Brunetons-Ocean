// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "BrunetonsAtmosphere/SkyMap" 
{
	SubShader
	{
		Pass
		{

		//This is the shader for the little map texture in the GUI at top left of screen.

		ZTest Always

		CGPROGRAM
		#include "UnityCG.cginc"
		#pragma target 3.0
		#pragma vertex vert
		#pragma fragment frag
		#include "Atmosphere.cginc"

			float _ApplyHDR;
		
			struct v2f 
			{
    			float4  pos : SV_POSITION;
    			float2  uv : TEXCOORD0;
			};

			v2f vert(appdata_base v)
			{
    			v2f OUT;
    			OUT.pos = UnityObjectToClipPos(v.vertex);
    			OUT.uv = (v.texcoord.xy-0.5)*2.2;
    			return OUT;
			}
			
			float4 frag(v2f IN) : COLOR
			{
			
			   	float2 u = IN.uv;

			   	float l = dot(u, u);
			    float3 result = float3(0,0,0);
			    
		    	if (l <= 1.02 && l > 1.0) 
				{
		            u = u / l;
		            l = 1.0 / l;
		        }
		
		        // inverse stereographic projection,
		        // from skymap coordinates to world space directions
		        float3 r = float3(2.0 * u, 1.0 - l) / (1.0 + l);
		        
		        float3 extinction;
		        float3 inscatter = SkyRadiance(_WorldSpaceCameraPos, r.xzy, extinction);
		        float3 sunColor = float3(0,0,0);
		       
			    if (l <= 1.02) 
				{
			        result.rgb = inscatter;
			        float sun = step(cos(M_PI / 90.0), dot(r.xzy, SUN_DIR));
			    	sunColor = float3(sun,sun,sun) * SUN_INTENSITY;
			   	}
			   	
			   	float3 col = sunColor * extinction + result;

				if(_ApplyHDR)
					return float4(hdr(col),1.0);
				else
					return float4(col, 1.0);
				
			}
			
			ENDCG

    	}
	}
}