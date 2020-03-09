// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'


Shader "Custom/Waves/MeshTile" {
	Properties {
		_ColorTex ("ColorTex (RGB)", 2D) = "white" {}
		_DepthTex ("DepthTex (R)", 2D) = "white" {}
	}

	SubShader {
		Tags { "RenderType"="Opaque" }

		Pass {
			Tags { "LightMode"="ForwardBase" }

			CGPROGRAM

			#pragma target 3.0
			#pragma fragmentoption ARB_precision_hint_fastest

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase multi_compile_fog nolightmap nodirlightmap nodynlightmap novertexlight

			#include "UnityCG.cginc"
			#include "AutoLight.cginc"

			inline float invLerp(float start, float end, float val) {
				return saturate((val - start) / (end - start));
			}

			/*
			 * Get a bilinearly interpolated sample from a texture (used in vert shader texture fetch, which doesn't support filtering)
			 *
			 * Could optimize by sampling from pre-calculated texture specificalliy for filtering step (http://http.developer.nvidia.com/GPUGems2/gpugems2_chapter18.html)
			 * or by evaluating procedural height function after morphing vertex
			 */
			// float4 tex2Dlod_bilinear(sampler2D tex, float4 uv) {
			// 	float2 pixelFrac = frac(uv.xy * _TileRes.x);

			// 	float4 baseUV = uv - float4(pixelFrac * _TileRes.y,0,0);
			// 	float4 heightBL = tex2Dlod(tex, baseUV);
			// 	float4 heightBR = tex2Dlod(tex, baseUV + float4(_TileRes.y,0,0,0));
			// 	float4 heightTL = tex2Dlod(tex, baseUV + float4(0,_TileRes.y,0,0));
			// 	float4 heightTR = tex2Dlod(tex, baseUV + float4(_TileRes.y,_TileRes.y,0,0));

			// 	float4 tA = lerp(heightBL, heightBR, pixelFrac.x);
			// 	float4 tB = lerp(heightTL, heightTR, pixelFrac.x);

			// 	return lerp(tA, tB, pixelFrac.y);
			// }

			// float UnpackHeight(float4 c) {
			// 	return (c.r * 256 + c.g) / 257.0;
			// }

			// half3 UnpackNormalCustom(half3 c) {
			// 	half3 n;
			// 	n.xy = c.xy * 2.0 - 1.0;
			// 	n.z = sqrt(1.0 - saturate(dot(n.xy, n.xy)));
			// 	return normalize(n);
			// }

			half3 UnpackNormalCustom(half3 c) {
				return normalize(c * 2.0 - 1.0);
			}

			float BiLerp3_sympy(float4x4 p, float2 uv) {
				float u = uv.x;
				float v = uv.y;

				/*----------------terms-------------------*/

				float a0 = -1.0 / 2.0 * p[0][1];
				float a1 = u * (a0 + (1.0 / 2.0) * p[2][1]);
				float a2 = pow(u, 2);
				float a3 = -5.0 / 2.0 * p[1][1];
				float a4 = (1.0 / 2.0) * p[3][1];
				float a5 = a2 * (a3 - a4 + p[0][1] + 2 * p[2][1]);
				float a6 = pow(u, 3);
				float a7 = (3.0 / 2.0) * p[1][1];
				float a8 = a6 * (a0 + a4 + a7 - 3.0 / 2.0 * p[2][1]);
				float a9 = -1.0 / 2.0 * p[0][2];
				float a10 = u * (a9 + (1.0 / 2.0) * p[2][2]);
				float a11 = (1.0 / 2.0) * p[3][2];
				float a12 = a2 * (-a11 + p[0][2] - 5.0 / 2.0 * p[1][2] + 2 * p[2][2]);
				float a13 = (3.0 / 2.0) * p[1][2];
				float a14 = a6 * (a11 + a13 + a9 - 3.0 / 2.0 * p[2][2]);
				float a15 = -1.0 / 2.0 * p[0][0];
				float a16 = u * (a15 + (1.0 / 2.0) * p[2][0]);
				float a17 = (1.0 / 2.0) * p[3][0];
				float a18 = a2 * (-a17 + p[0][0] - 5.0 / 2.0 * p[1][0] + 2 * p[2][0]);
				float a19 = a6 * (a15 + a17 + (3.0 / 2.0) * p[1][0] - 3.0 / 2.0 * p[2][0]);
				float a20 = -1.0 / 2.0 * a16 - 1.0 / 2.0 * a18 - 1.0 / 2.0 * a19 - 1.0 / 2.0 * p[1][0];
				float a21 = (1.0 / 2.0) * p[1][3];
				float a22 = -1.0 / 2.0 * p[0][3];
				float a23 = (1.0 / 2.0) * u * (a22 + (1.0 / 2.0) * p[2][3]);
				float a24 = (1.0 / 2.0) * p[3][3];
				float a25 = (1.0 / 2.0) * a2 * (-a24 + p[0][3] - 5.0 / 2.0 * p[1][3] + 2 * p[2][3]);
				float a26 = (1.0 / 2.0) * a6 * (a22 + a24 + (3.0 / 2.0) * p[1][3] - 3.0 / 2.0 * p[2][3]);

				/*--------------solutions------------------*/

				float output_0 = a1 + a5 + a8 + p[1][1] + pow(v, 3) * ((3.0 / 2.0) * a1 - 3.0 / 2.0 * a10 - 3.0 / 2.0 * a12 - a13 - 3.0 / 2.0 * a14 + a20 + a21 + a23 + a25
				+ a26 + (3.0 / 2.0) * a5 + a7 + (3.0 / 2.0) * a8) + pow(v, 2) * (-5.0 / 2.0 * a1 + 2 * a10 + 2 * a12 + 2 * a14 + a16 + a18 + a19 - a21 - a23 - a25 - a26 + a3 -
				5.0 / 2.0 * a5 - 5.0 / 2.0 * a8 + p[1][0] + 2 * p[1][2]) + v * ((1.0 / 2.0) * a10 + (1.0 / 2.0) * a12 + (1.0 / 2.0) * a14 + a20 + (1.0 / 2.0) * p[1][2]);

				return output_0;
			}

			float2 BiLerp3_Grad1_sympy(float4x4 p, float2 uv) {
				float u = uv.x;
				float v = uv.y;

				/*----------------terms-------------------*/

				float a0 = (1.0 / 2.0) * p[3][2];
				float a1 = -a0 + p[0][2] - 5.0 / 2.0 * p[1][2] + 2 * p[2][2];
				float a2 = a1 * u;
				float a3 = -1.0 / 2.0 * p[0][2];
				float a4 = (3.0 / 2.0) * p[1][2];
				float a5 = a0 + a3 + a4 - 3.0 / 2.0 * p[2][2];
				float a6 = pow(u, 2);
				float a7 = (3.0 / 2.0) * a6;
				float a8 = (1.0 / 2.0) * p[3][0];
				float a9 = -a8 + p[0][0] - 5.0 / 2.0 * p[1][0] + 2 * p[2][0];
				float a10 = a9 * u;
				float a11 = -1.0 / 2.0 * p[0][0];
				float a12 = a11 + a8 + (3.0 / 2.0) * p[1][0] - 3.0 / 2.0 * p[2][0];
				float a13 = -a10 - a12 * a7 + (1.0 / 4.0) * p[0][0] - 1.0 / 4.0 * p[2][0];
				float a14 = pow(v, 2);
				float a15 = (1.0 / 4.0) * p[2][3];
				float a16 = (1.0 / 4.0) * p[0][3];
				float a17 = (1.0 / 2.0) * p[3][3];
				float a18 = -a17 + p[0][3] - 5.0 / 2.0 * p[1][3] + 2 * p[2][3];
				float a19 = a18 * u;
				float a20 = -5.0 / 2.0 * p[1][1];
				float a21 = (1.0 / 2.0) * p[3][1];
				float a22 = a20 - a21 + p[0][1] + 2 * p[2][1];
				float a23 = a22 * u;
				float a24 = 3 * a6;
				float a25 = a5 * a6;
				float a26 = -1.0 / 2.0 * p[0][1];
				float a27 = (3.0 / 2.0) * p[1][1];
				float a28 = a21 + a26 + a27 - 3.0 / 2.0 * p[2][1];
				float a29 = a28 * a6;
				float a30 = -1.0 / 2.0 * p[0][3];
				float a31 = a17 + a30 + (3.0 / 2.0) * p[1][3] - 3.0 / 2.0 * p[2][3];
				float a32 = a31 * a7;
				float a33 = a11 + (1.0 / 2.0) * p[2][0];
				float a34 = a26 + (1.0 / 2.0) * p[2][1];
				float a35 = u * (a3 + (1.0 / 2.0) * p[2][2]);
				float a36 = a1 * a6;
				float a37 = pow(u, 3);
				float a38 = a37 * a5;
				float a39 = (1.0 / 2.0) * p[1][3];
				float a40 = a33 * u;
				float a41 = a34 * u;
				float a42 = (1.0 / 2.0) * u * (a30 + (1.0 / 2.0) * p[2][3]);
				float a43 = a6 * a9;
				float a44 = (1.0 / 2.0) * a18 * a6;
				float a45 = a12 * a37;
				float a46 = a28 * a37;
				float a47 = (1.0 / 2.0) * a31 * a37;
				float a48 = -1.0 / 2.0 * a40 - 1.0 / 2.0 * a43 - 1.0 / 2.0 * a45 - 1.0 / 2.0 * p[1][0];

				/*--------------solutions------------------*/

				float output_0 = a14 * (2 * a10 + a12 * a24 - a15 + a16 - a19 + 4 * a2 - 5 * a23 + 6 * a25 - 15.0 / 2.0 * a29 - a32 + a33 + (5.0 / 4.0) * p[0][1] - p[0][2] - 5.0 / 4.0 * p[2][1] + p[2][2]) + 2 * a23 + a24 * a28 + a34 + pow(v, 3) * (a13 + a15 - a16 + a19 - 3 * a2 + 3 * a23 - 9.0 / 2.0 * a25 + (9.0 / 2.0) * a29 + a32 - 3.0 / 4.0 * p[0][1] + (3.0 / 4.0) * p[0][2] + (3.0 / 4.0) * p[2][1] - 3.0 / 4.0 * p[2][2]) + v * (a13 + a2 + a5 * a7 - 1.0 / 4.0 * p[0][2] + (1.0 / 4.0) * p[2][2]);
				float output_1 = 3 * a14 * (-a1 * a7 + a22 * a7 + a27 - 3.0 / 2.0 * a35 - 3.0 / 2.0 * a38 + a39 - a4 + (3.0 / 2.0) * a41 + a42 + a44 + (3.0 / 2.0) * a46 + a47 + a48) + (1.0 / 2.0) * a35 + (1.0 / 2.0) * a36 + (1.0 / 2.0) * a38 + a48 + (1.0 / 2.0) * p[1][2] + 2 * v * (a20 - 5.0 / 2.0 * a22 * a6 + 2 * a35 + 2 * a36 + 2 * a38 - a39 + a40 - 5.0 / 2.0 * a41 - a42 + a43 - a44 + a45 - 5.0 / 2.0 * a46 - a47 + p[1][0] + 2 * p[1][2]);

				return float2(output_0, output_1);
			}

			float2 BiLerp3_Grad2_sympy(float4x4 p, float2 uv) {
				float u = uv.x;
				float v = uv.y;

				/*----------------terms-------------------*/

				float a0 = -p[3][2];
				float a1 = a0 + p[0][2] - 3*p[1][2] + 3*p[2][2];
				float a2 = a1*u;
				float a3 = -p[3][0];
				float a4 = a3 + p[0][0] - 3*p[1][0] + 3*p[2][0];
				float a5 = a4*u;
				float a6 = -p[3][1];
				float a7 = a6 + p[0][1] - 3*p[1][1] + 3*p[2][1];
				float a8 = a7*u;
				float a9 = 4*p[2][3];
				float a10 = 2*p[0][3];
				float a11 = 5*p[1][3];
				float a12 = -p[3][3];
				float a13 = a12 + p[0][3] - 3*p[1][3] + 3*p[2][3];
				float a14 = 3*u;
				float a15 = -a10 + a11 + a13*a14 - a9 + p[3][3];
				float a16 = 4*p[2][2];
				float a17 = 2*p[0][2];
				float a18 = 5*p[1][2];
				float a19 = a3 + 2*p[0][0] - 5*p[1][0] + 4*p[2][0];
				float a20 = a19 - 3*a5;
				float a21 = -5*p[1][1];
				float a22 = a21 + a6 + 2*p[0][1] + 4*p[2][1];
				float a23 = u*(p[0][3] - p[2][3]);
				float a24 = pow(u, 3);
				float a25 = a13*a24;
				float a26 = p[0][2] - p[2][2];
				float a27 = a1*a24;
				float a28 = pow(u, 2);
				float a29 = a28*(a0 + a16 + a17 - a18);
				float a30 = a22*a28;
				float a31 = a28*(a10 - a11 + a12 + a9);
				float a32 = p[0][1] - p[2][1];
				float a33 = a24*a7;
				float a34 = a19*a28 - a24*a4 + 2*p[1][0] - u*(p[0][0] - p[2][0]);

				/*--------------solutions------------------*/

				float output_0 = -a14*a7 + a22 - 1.0/2.0*pow(v, 3)*(a15 - 9*a2 + a20 + 9*a8 - 6*p[0][1] + 6*p[0][2] + 15*p[1][1] - 15*p[1][2] - 12*p[2][1] + 12*p[2][2] + 3*p[3][1] - 3*p[3][2]) + (1.0/2.0)*pow(v, 2)*(a15 - 12*a2 - 6*a5 + 15*a8 + 4*p[0][0] - 10*p[0][1] + 8*p[0][2] - 10*p[1][0] + 25*p[1][1] - 20*p[1][2] + 8*p[2][0] - 20*p[2][1] + 16*p[2][2] - 2*p[3][0] + 5*p[3][1] - 4*p[3][2]) - 1.0/2.0*v*(-a16 - a17 + a18 + 3*a2 + a20 + p[3][2]);
				float output_1 = a21 + (1.0/2.0)*a23 + (1.0/2.0)*a25 - 2*a26*u - 2*a27 + 2*a29 - 5.0/2.0*a30 - 1.0/2.0*a31 + (5.0/2.0)*a32*u + (5.0/2.0)*a33 + a34 + 4*p[1][2] - p[1][3] - 3.0/2.0*v*(-a14*a26 + a14*a32 + a23 + a25 - 3*a27 + 3*a29 - 3*a30 - a31 + 3*a33 + a34 - 6*p[1][1] + 6*p[1][2] - 2*p[1][3]);

				return float2(output_0, output_1);
			}

			float BiLerp3_Curvature_sympy(float4x4 p, float2 uv) {
				float u = uv.x;
				float v = uv.y;

								
				/*----------------terms-------------------*/

				float a0 = (1.0/2.0)*p[3][2];
				float a1 = -a0 + p[0][2] - 5.0/2.0*p[1][2] + 2*p[2][2];
				float a2 = a1*u;
				float a3 = -1.0/2.0*p[0][2];
				float a4 = (3.0/2.0)*p[1][2];
				float a5 = a0 + a3 + a4 - 3.0/2.0*p[2][2];
				float a6 = pow(u, 2);
				float a7 = (3.0/2.0)*a6;
				float a8 = (1.0/2.0)*p[3][0];
				float a9 = -a8 + p[0][0] - 5.0/2.0*p[1][0] + 2*p[2][0];
				float a10 = a9*u;
				float a11 = -1.0/2.0*p[0][0];
				float a12 = a11 + a8 + (3.0/2.0)*p[1][0] - 3.0/2.0*p[2][0];
				float a13 = -a10 - a12*a7 + (1.0/4.0)*p[0][0] - 1.0/4.0*p[2][0];
				float a14 = pow(v, 2);
				float a15 = (1.0/4.0)*p[2][3];
				float a16 = (1.0/4.0)*p[0][3];
				float a17 = (1.0/2.0)*p[3][3];
				float a18 = -a17 + p[0][3] - 5.0/2.0*p[1][3] + 2*p[2][3];
				float a19 = a18*u;
				float a20 = -5.0/2.0*p[1][1];
				float a21 = (1.0/2.0)*p[3][1];
				float a22 = a20 - a21 + p[0][1] + 2*p[2][1];
				float a23 = a22*u;
				float a24 = 3*a6;
				float a25 = a5*a6;
				float a26 = -1.0/2.0*p[0][1];
				float a27 = (3.0/2.0)*p[1][1];
				float a28 = a21 + a26 + a27 - 3.0/2.0*p[2][1];
				float a29 = a28*a6;
				float a30 = -1.0/2.0*p[0][3];
				float a31 = a17 + a30 + (3.0/2.0)*p[1][3] - 3.0/2.0*p[2][3];
				float a32 = a31*a7;
				float a33 = a11 + (1.0/2.0)*p[2][0];
				float a34 = pow(v, 3);
				float a35 = a26 + (1.0/2.0)*p[2][1];
				float a36 = a14*(2*a10 + a12*a24 - a15 + a16 - a19 + 4*a2 - 5*a23 + 6*a25 - 15.0/2.0*a29 - a32 + a33 + (5.0/4.0)*p[0][1] - p[0][2] - 5.0/4.0*p[2][1] + p[2][2]) + 2*a23 + a24*a28 + a34*(a13 + a15 - a16 + a19 - 3*a2 + 3*a23 - 9.0/2.0*a25 + (9.0/2.0)*a29 + a32 - 3.0/4.0*p[0][1] + (3.0/4.0)*p[0][2] + (3.0/4.0)*p[2][1] - 3.0/4.0*p[2][2]) + a35 + v*(a13 + a2 + a5*a7 - 1.0/4.0*p[0][2] + (1.0/4.0)*p[2][2]);
				float a37 = u*(a3 + (1.0/2.0)*p[2][2]);
				float a38 = a1*a6;
				float a39 = pow(u, 3);
				float a40 = a39*a5;
				float a41 = (1.0/2.0)*p[1][3];
				float a42 = a33*u;
				float a43 = a35*u;
				float a44 = (1.0/2.0)*u*(a30 + (1.0/2.0)*p[2][3]);
				float a45 = a6*a9;
				float a46 = (5.0/2.0)*a6;
				float a47 = (1.0/2.0)*a18*a6;
				float a48 = a12*a39;
				float a49 = a28*a39;
				float a50 = (1.0/2.0)*a31*a39;
				float a51 = -1.0/2.0*a42 - 1.0/2.0*a45 - 1.0/2.0*a48 - 1.0/2.0*p[1][0];
				float a52 = 3*a14*(-a1*a7 + a22*a7 + a27 - 3.0/2.0*a37 - a4 - 3.0/2.0*a40 + a41 + (3.0/2.0)*a43 + a44 + a47 + (3.0/2.0)*a49 + a50 + a51) + (1.0/2.0)*a37 + (1.0/2.0)*a38 + (1.0/2.0)*a40 + a51 + (1.0/2.0)*p[1][2] + 2*v*(a20 - a22*a46 + 2*a37 + 2*a38 + 2*a40 - a41 + a42 - 5.0/2.0*a43 - a44 + a45 - a47 + a48 - 5.0/2.0*a49 - a50 + p[1][0] + 2*p[1][2]);
				float a53 = -p[3][2];
				float a54 = a53 + p[0][2] - 3*p[1][2] + 3*p[2][2];
				float a55 = a54*u;
				float a56 = -p[3][0];
				float a57 = a56 + p[0][0] - 3*p[1][0] + 3*p[2][0];
				float a58 = a57*u;
				float a59 = -p[3][1];
				float a60 = a59 + p[0][1] - 3*p[1][1] + 3*p[2][1];
				float a61 = a60*u;
				float a62 = 4*p[2][3];
				float a63 = 2*p[0][3];
				float a64 = 5*p[1][3];
				float a65 = -p[3][3];
				float a66 = a65 + p[0][3] - 3*p[1][3] + 3*p[2][3];
				float a67 = 3*u;
				float a68 = -a62 - a63 + a64 + a66*a67 + p[3][3];
				float a69 = 4*p[2][2];
				float a70 = 2*p[0][2];
				float a71 = 5*p[1][2];
				float a72 = a56 + 2*p[0][0] - 5*p[1][0] + 4*p[2][0];
				float a73 = -3*a58 + a72;
				float a74 = -5*p[1][1];
				float a75 = a59 + a74 + 2*p[0][1] + 4*p[2][1];
				float a76 = u*(p[0][3] - p[2][3]);
				float a77 = a39*a66;
				float a78 = u*(p[0][2] - p[2][2]);
				float a79 = a39*a54;
				float a80 = a53 + a69 + a70 - a71;
				float a81 = a6*(a62 + a63 - a64 + a65);
				float a82 = u*(p[0][1] - p[2][1]);
				float a83 = a39*a60;
				float a84 = -a39*a57 + a6*a72 + 2*p[1][0] - u*(p[0][0] - p[2][0]);

				/*--------------solutions------------------*/

				float output_0 = (a36*(-a46*a75 + 2*a6*a80 + a74 + (1.0/2.0)*a76 + (1.0/2.0)*a77 - 2*a78 - 2*a79 - 1.0/2.0*a81 + (5.0/2.0)*a82 + (5.0/2.0)*a83 + a84 + 4*p[1][2] - p[1][3] - 3.0/2.0*v*(-a24*a75 + a24*a80 + a76 + a77 - 3*a78 - 3*a79 - a81 + 3*a82 + 3*a83 + a84 - 6*p[1][1] + 6*p[1][2] - 2*p[1][3])) - a52*((1.0/2.0)*a14*(-12*a55 - 6*a58 + 15*a61 + a68 + 4*p[0][0] - 10*p[0][1] + 8*p[0][2] - 10*p[1][0] + 25*p[1][1] - 20*p[1][2] + 8*p[2][0] - 20*p[2][1] + 16*p[2][2] - 2*p[3][0] + 
				5*p[3][1] - 4*p[3][2]) - 1.0/2.0*a34*(-9*a55 + 9*a61 + a68 + a73 - 6*p[0][1] + 6*p[0][2] + 15*p[1][1] - 15*p[1][2] - 12*p[2][1] + 12*p[2][2] + 3*p[3][1] - 3*p[3][2]) - a60*a67 + a75 - 1.0/2.0*v*(3*a55 - a69 - a70 + a71 + a73 + p[3][2])))/pow(pow(a36, 2) + pow(a52, 2), 3.0/2.0);

				return output_0;
			}

			sampler2D _ColorTex;
			sampler2D _DepthTex;

			float2 _MainColor_ST;

			float4 _LightColor0;

			struct appdata {
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f {
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0; // diffuse
			};

			v2f vert (appdata_base v) {
				v2f o;

				// To clip space
				// v.vertex.z += tex2Dlod(_DepthTex, float4(v.vertex.x / 16, v.vertex.y / 9, 0,0)).r;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord;

				return o;
			}

			half4 frag(v2f i) : COLOR {
				fixed4 finalColor = tex2D(_ColorTex, i.uv);
				// fixed4 finalColor = tex2D(_DepthTex, i.uv) / 10.0;

                UNITY_OPAQUE_ALPHA(finalColor.a);

				return finalColor;
			}

			ENDCG
		}
	}
	FallBack "Diffuse"
}
