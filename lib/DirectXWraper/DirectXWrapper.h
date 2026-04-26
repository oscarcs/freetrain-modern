// DirectXWraper.h

#pragma once

#define static_class abstract sealed

using System::Runtime::InteropServices::Marshal;

namespace freetrain
{
namespace DirectXWrapper
{
	// DirectDraw

	/// <summary>
	/// Color mask.
	/// </summary>
	public enum class ColorMask { R,G,B };
	ref class Surface;
	ref class GDIGraphics;

	ref class DirectDraw;
	ref class WindowedDirectDraw;
	public enum class DDSurfaceAllocation { Auto, ForceVideoMem, ForceSystemMem };

	ref struct NightImageBuilder;

	// DirectAudio
	ref class AudioPath;
	ref class BGM;
	ref class Performance;
	ref class Segment;
	ref class SegmentState;
}
}

HRESULT bltAlphaFast(
	IDirectDrawSurface7* lpDDSDest,
	IDirectDrawSurface7* lpDDSSource,
	int iDestX,
	int iDestY,
	int sourceX1, int sourceY1, int sourceX2, int sourceY2,
	DWORD colorKey );


HRESULT bltShape(
	IDirectDrawSurface7* lpDDSDest,
	IDirectDrawSurface7* lpDDSSource,
	int iDestX,
	int iDestY,
	int sourceX1, int sourceY1, int sourceX2, int sourceY2,
	int fillColor,
	DWORD colorKey );

HRESULT bltColorTransform(
	IDirectDrawSurface7* lpDDSDest,
	IDirectDrawSurface7* lpDDSSource,
	int iDestX,
	int iDestY,
	int sourceX1, int sourceY1, int sourceX2, int sourceY2,
	int* srcColors,
	int* dstColors,
	int colorsLen,
	//IntArray* srcColors,
	//IntArray* dstColors,
	DWORD colorKey,
	BOOL vflip );

HRESULT bltHueTransform(
	IDirectDrawSurface7* lpDDSDest,
	IDirectDrawSurface7* lpDDSSource,
	int iDestX,
	int iDestY,
	int sourceX1, int sourceY1, int sourceX2, int sourceY2,
	DWORD R_dst, DWORD G_dst, DWORD B_dst, DWORD colorKey );

HRESULT buildNightImage( IDirectDrawSurface7* lpSurface );

System::String^ GetDisplayModeName();

inline void ThrowIfFailed(HRESULT hr)
{
	if (FAILED(hr))
	{
		Marshal::ThrowExceptionForHR(hr);
	}
}
