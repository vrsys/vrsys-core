// VRSYS plugin of Virtual Reality and Visualization Research Group (Bauhaus University Weimar)
//  _    ______  _______  _______
// | |  / / __ \/ ___/\ \/ / ___/
// | | / / /_/ /\__ \  \  /\__ \ 
// | |/ / _, _/___/ /  / /___/ / 
// |___/_/ |_|/____/  /_//____/  
//
//  __                            __                       __   __   __    ___ .  . ___
// |__)  /\  |  | |__|  /\  |  | /__`    |  | |\ | | \  / |__  |__) /__` |  |   /\   |  
// |__) /~~\ \__/ |  | /~~\ \__/ .__/    \__/ | \| |  \/  |___ |  \ .__/ |  |  /~~\  |  
//
//       ___               __                                                           
// |  | |__  |  |\/|  /\  |__)                                                          
// |/\| |___ |  |  | /~~\ |  \                                                                                                                                                                                     
//
// Copyright (c) 2022 Virtual Reality and Visualization Research Group
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------
//   Authors:        Jacob Lammert
//   Date:           2023
//-----------------------------------------------------------------

Shader "VRSYS/StereoPortalShader"
{
    Properties{
      _MainTex("Mono Texture (unused)", 2D) = "white" {}
      _LeftEyeTexture("Left Eye Texture", 2D) = "white" {}
      _RightEyeTexture("Right Eye Texture", 2D) = "white" {}
      _relativeResolution("_relativeResolution",Float) = 1
      _screenResolution("_screenResolution",Float) = 1024
      _owner("_owner",Float) = 0
    }
    SubShader{
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM

        #pragma surface surf NoLighting noambient

        fixed4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten) {
			return fixed4(s.Albedo, 1);
		}

        struct Input {
            float2 uv_MainTex;
        };

        sampler2D _MainTex;
        sampler2D _LeftEyeTexture;
        sampler2D _RightEyeTexture;
        
        float _relativeResolution;
        float _screenResolution;
        float _drawFrame;

        void surf(Input IN, inout SurfaceOutput o) {
            
            float factor = pow(_relativeResolution,3) * _screenResolution;
            float2 sample_uv = floor(IN.uv_MainTex * factor) / factor;
            o.Albedo = unity_StereoEyeIndex == 0 ? tex2D(_LeftEyeTexture, sample_uv).rgb : tex2D(_RightEyeTexture, sample_uv).rgb;
            
        }

        ENDCG
    }
    Fallback "Diffuse"
}
