HEADER
{
	Description = "Projected Shadow Texture for standalone entity";
}

FEATURES
{
	#include "vr_common_features.fxc"

	Feature(F_TEXTURE_FILTERING, 0..4 (0="Anisotropic", 1="Bilinear", 2="Trilinear", 3="Point Sample", 4="Nearest Neighbour"), "Texture Filtering");
}

MODES
{
	VrForward();
	ToolsVis(S_MODE_TOOLS_VIS);
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs(VertexInput v)
	{
		PixelInput i = ProcessVertex(v);
		return FinalizeVertex(i);
	}
}

PS
{
	#define BLEND_MODE_ALREADY_SET
	RenderState(BlendEnable, true);
	RenderState(BlendOp, ADD);
	RenderState(SrcBlend, ZERO);
	RenderState(DstBlend, SRC_COLOR);
	RenderState(BlendOpAlpha, ADD);
	RenderState(SrcBlendAlpha, ZERO);
	RenderState(DstBlendAlpha, SRC_ALPHA);

	#define DEPTH_STATE_ALREADY_SET
    RenderState(DepthWriteEnable, false);
    RenderState(DepthEnable, false);
    RenderState(DepthFunc, ALWAYS);

	RenderState(StencilEnable, true);
	RenderState(StencilReadMask, 0xff);
	RenderState(StencilWriteMask, 0x00);
	RenderState(StencilFailOp, ZERO);
	RenderState(StencilDepthFailOp, ZERO);
	RenderState(StencilPassOp, KEEP);
	RenderState(StencilFunc, NOT_EQUAL);
	RenderState(BackStencilFailOp, KEEP);
	RenderState(BackStencilDepthFailOp, KEEP);
	RenderState(BackStencilPassOp, KEEP);
	RenderState(BackStencilFunc, NEVER);
	RenderState(StencilRef, 0x02);

	CreateInputTexture2D(TextureColor, Srgb, 8, "", "_color", "Material,10/10", Default3(1.0, 1.0, 1.0));
    CreateInputTexture2D(TextureTranslucency, Linear, 8, "", "_trans", "Material,10/70", Default3(1.0, 1.0, 1.0));
	CreateTexture2DWithoutSampler(g_tColor) <
		Channel(RGB, AlphaWeighted(TextureColor, TextureTranslucency), Srgb);
		Channel(A, Box(TextureTranslucency), Linear);
		OutputFormat(BC7);
		SrgbRead(true);
	>;
	SamplerState TextureFiltering <
		Filter((
			F_TEXTURE_FILTERING == 0 ? ANISOTROPIC : (
				F_TEXTURE_FILTERING == 1 ? BILINEAR : (
					F_TEXTURE_FILTERING == 2 ? TRILINEAR : (
						F_TEXTURE_FILTERING == 3 ? POINT : NEAREST
					)
				)
			)
		));
		MaxAniso(8);
	>;

	float MinDepthAlpha <Default(1.0); Range(0.0, 2.0); UiGroup("Material,10/71"); >;
	float MinDepthCutoff <Default(1.0); Range(0.0, 2.0); UiGroup("Material,10/72"); >;
	float MaxDepthAlpha <Default(0.0); Range(0.0, 2.0); UiGroup("Material,10/73"); >;
	float MaxDepthCutoff <Default(1.0); Range(0.0, 2.0); UiGroup("Material,10/74"); >;
	float4x4 matInvertTransform <Attribute("InvertTransformMatrix"); >;
	CreateTexture2DMS(g_tSceneDepth) <
		Attribute("DepthBuffer");
		SrgbRead(false);
		Filter(POINT);
		AddressU(CLAMP);
		AddressV(CLAMP);
	>;

	float3 InvertProjectPosition(float3 coordinates, float4x4 transformMatrix)
	{
        float4 projectedPosition = mul(
			transformMatrix,
			float4(2.0 * coordinates.x - 1.0, 1.0 - 2.0 * coordinates.y, coordinates.z, 1.0)
		);
        return projectedPosition.xyz / projectedPosition.w;
    }

	float3 FetchViewPosition(float2 screenSpacePosition)
	{
		float sceneDepth = RemapValClamped(
			Tex2DMS(g_tSceneDepth, screenSpacePosition, 0).r,
			g_flViewportMinZ,
			g_flViewportMaxZ,
			0.0,
			1.0
		);
		return float3((screenSpacePosition + g_vViewportOffset.xy) / (g_vRenderTargetSize - 1.0), sceneDepth);
	}

	float3 FetchScenePosition(float2 screenSpacePosition)
	{
        return InvertProjectPosition(FetchViewPosition(screenSpacePosition), g_matProjectionToWorld)
			+ g_vCameraPositionWs;
	}

	float ColorCutoff(float color, float cutoff)
	{
		return 1.0 - saturate(cutoff - color) / saturate(cutoff);
	}

	bool IsInsideProjection(float3 position)
	{
		float3 centerPosition = position - 0.5;
		return max(max(abs(centerPosition.x), abs(centerPosition.y)), abs(centerPosition.z)) < 0.5;
	}

	float4 MainPs(PixelInput i) : SV_Target0
	{
		float3 scenePosition = FetchScenePosition(i.vPositionSs.xy);
		float3 projectedPosition = mul(matInvertTransform, float4(scenePosition, 1.0)).xyz;
		if (!IsInsideProjection(projectedPosition + float3(0.5, 0.5, 0.0))) {
			discard;
		}
		float4 colorSample = Tex2DS(g_tColor, TextureFiltering, projectedPosition.xy - 0.5);
		float alphaFactor = saturate(lerp(MinDepthAlpha, MaxDepthAlpha, projectedPosition.z))
			* ColorCutoff(colorSample.a, lerp(MinDepthCutoff, MaxDepthCutoff, projectedPosition.z));
		return float4(lerp(1.0, colorSample.rgb, alphaFactor), 1.0);
	}
}
