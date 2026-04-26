// Original author ÉXÉå6 >>565 , 2009/09/25 , ver 1.0.0.0
// Attached by riorio , 2010/03/16 , ver 1.0.0.1
// Attached by riorio , 2010/03/21 , ver 1.0.0.3 , ver 1.0.0.4


#include "stdafx.h"
#include "DirectXWrapper.h"

namespace wrapper = freetrain::DirectXWrapper;
namespace drawing = System::Drawing;
 
using System::Runtime::InteropServices::COMException;
using System::Runtime::InteropServices::Marshal;
using System::IntPtr;
using System::String;
using System::Diagnostics::Debug;
using System::Windows::Forms::Control;
using System::Exception;
using System::Drawing::Bitmap;
using System::Drawing::Color;
using System::Drawing::Graphics;
using System::Drawing::Point;
using System::Drawing::Size;
using System::Drawing::Image;
using System::Convert;
using System::Math;
using msclr::auto_handle;
using wrapper::GDIGraphics;


// Ç±Ç±Ç©ÇÁUtil.cs

namespace Util
{
	static RECT toRECT( drawing::Rectangle srcRect ) 
	{
		RECT r = {};
		r.left	= srcRect.Left;
		r.top	= srcRect.Top;
		r.right	= srcRect.Right;
		r.bottom= srcRect.Bottom;
		return r;
	}
	static RECT toRECT( int x1, int y1, int x2, int y2 ) {
		RECT r = {};
		r.left	= x1;
		r.top	= y1;
		r.right	= x2;
		r.bottom= y2;
		return r;
	}
	static RECT toRECT( Point pt, Size sz ) {
		RECT r = {};
		r.left	= pt.X;
		r.top	= pt.Y;
		r.right	= pt.X+sz.Width;
		r.bottom= pt.Y+sz.Height;
		return r;
	}
	static drawing::Rectangle toRectangle( RECT r ) {
		return drawing::Rectangle( r.left, r.top, r.right-r.left, r.bottom-r.top );
	}

	/// <summary>
	/// Compute the intersection of two RECTs.
	/// </summary>
	static RECT intersect( RECT a, RECT b ) {
		RECT r = {};
		r.left   = Math::Max( a.left,   b.left   );
		r.top    = Math::Max( a.top,    b.top    );
		r.right	 = Math::Min( a.right,  b.right  );
		r.bottom = Math::Min( a.bottom, b.bottom );
		return r;
	}

	/// <summary>
	/// Clip two rectangles by the clipping region.
	/// 
	/// </summary>
	static void clip( RECT% dst, RECT% src, RECT clip ) {
		int t;

		// compute new dst.left
		t = Math::Max( dst.left,   clip.left );
		src.left   += (t-dst.left);
		dst.left   =  t;		// dst.left += (t-dst.left)

		t = Math::Max( dst.top,    clip.top );
		src.top    += (t-dst.top);
		dst.top    =  t;

		t = Math::Min( dst.right,  clip.right );
		src.right  += (t-dst.right);
		dst.right  =  t;

		t = Math::Min( dst.bottom, clip.bottom );
		src.bottom += (t-dst.bottom);
		dst.bottom =  t;
	}

	/// <summary>
	/// Clipping in a vflip mode.
	/// 
	/// In this mode, clipping is done as:
	/// 
	/// ###---     ------
	/// ###---  => ###---
	/// ------     ###---
	/// </summary>
	/// <param name="dst"></param>
	/// <param name="src"></param>
	/// <param name="clip"></param>
	static void clipVflip( RECT% dst, RECT% src, RECT clip ) {
		int t;

		// compute new dst.left
		t = Math::Max( dst.left,   clip.left );
		src.left   += (t-dst.left);
		dst.left   =  t;		// dst.left += (t-dst.left)

		t = Math::Max( dst.top,    clip.top );
		src.bottom -= (t-dst.top);		// different than the clip method
		dst.top    =  t;

		t = Math::Min( dst.right,  clip.right );
		src.right  += (t-dst.right);
		dst.right  =  t;

		t = Math::Min( dst.bottom, clip.bottom );
		src.top    -= (t-dst.bottom);	// different than the clip method
		dst.bottom =  t;
	}
}

//Ç±Ç±Ç‹Ç≈Util.cs
//Ç±Ç±Ç©ÇÁSurface.cs

/// <summary>
/// Wraps a Surface object and provides GDI+ functionality
/// via the graphics property.
/// </summary>
public ref class wrapper::GDIGraphics sealed {
public:
	initonly Graphics^ graphics;
private:
	initonly Surface^ surface;
	initonly HDC hdc;
public:
	GDIGraphics( Surface^ _surface );

	~GDIGraphics()
	{
		this->!GDIGraphics();
	}

	!GDIGraphics();
};

/// <summary>
/// Wraps DirectDraw surface object.
/// 
/// This is the core object of DirectDraw.
/// The code is a wrapper around Visual BASIC binding of DirectDraw.
/// 
/// Since I couldn't figure out how to create a CLR binding for
/// clipper, this class implements a clipping support by itself.
/// </summary>
public ref class wrapper::Surface
{
private:
	IDirectDrawSurface7* surface;

	/// <summary> Bit-width. </summary>
	initonly BYTE widthR,widthB,widthG;

	/// <summary>
	/// Clipping rect. Even if the client doesn't set any clipping,
	/// this is initialized to (0,0)-(size)
	/// </summary>
	RECT& clip;

	/// <summary>
	/// Gets the size of this surface.
	/// </summary>
public:
	Size size;

private:
	bool hasSourceColorKey;
	

	/// <summary>
	/// Obtain the wrapped DirectDraw interface.
	/// For advanced use only.
	/// </summary>
public:
	property IDirectDrawSurface7* handle { IDirectDrawSurface7* get() { return surface; } }
internal:
	Surface(IDirectDrawSurface7* _handle) : clip(*new RECT()) {
		hasSourceColorKey = false;
		this->surface = _handle;

		// compute the size of this surface
		DDSURFACEDESC2 desc = {sizeof desc};
		surface->GetSurfaceDesc( &desc );
		this->size = Size(Convert::ToInt32(static_cast<UINT>(desc.dwWidth)), Convert::ToInt32(static_cast<UINT>(desc.dwHeight)));
		resetClipRect();

		// compute the bit shift width for color fill
		DDPIXELFORMAT pixelFormat = {sizeof pixelFormat};
		ThrowIfFailed(surface->GetPixelFormat(&pixelFormat));
		widthR = countBitWidth(pixelFormat.dwRBitMask);
		widthG = countBitWidth(pixelFormat.dwGBitMask);
		widthB = countBitWidth(pixelFormat.dwBBitMask);
	}
public:
	property String^ displayModeName {
		 String^ get() {
			return "mode"+widthR.ToString()+widthG.ToString()+widthB.ToString();
		}
	}

	/// <summary>
	/// Counts the width of bits.
	/// </summary>
private:
	BYTE countBitWidth( int _i ) {
		UINT i = static_cast<UINT>(_i);
		if (i == 0) {
			return 0;
		}
		while((i&1)==0)	i>>=1;

		BYTE w=0;
		while(i!=0) {
			i >>= 1;
			w++;
		}
		return w;
	}

	/// <summary>
	/// Converts a Color into a fill value.
	/// </summary>
private:
	UINT colorToFill( Color c ) {
		UINT x = 0;
		x |= ((UINT)c.R)>>(8-widthR);
		x <<= widthG;
		x |= ((UINT)c.G)>>(8-widthG);
		x <<= widthB;
		x |= ((UINT)c.B)>>(8-widthB);
		return x;
	}

public:
	property drawing::Rectangle clipRect {
		drawing::Rectangle get() {
			return Util::toRectangle(clip);
		}
		void set(drawing::Rectangle value) {
			// clipping rectangle must also clip things to fit inside the surface.
			// otherwise blitting won't work.
			value.Intersect( drawing::Rectangle( 0,0, size.Width, size.Height ) );
			clip = Util::toRECT(value);
		}
	}
	/// <summary>
	/// Removes the clipping rect by re-initializing it
	/// to the default size.
	/// </summary>
public:
	void resetClipRect() {
		clip = Util::toRECT( Point(0,0), size );
	}


	~Surface() {
		// explicitly release a reference
		if(surface!=nullptr)
			surface->Release();
		surface=nullptr;
	}

	/// <summary>
	/// Performs fast bit blitting.
	/// 
	/// This can't be used with a surface with a clipper.
	/// </summary>
	/// 
public:
	void bltFast( int destX, int destY, Surface^ source, drawing::Rectangle srcRect ) {
		RECT srect = Util::toRECT(srcRect);
		// TODO: clip

		surface->BltFast( destX, destY, source->handle, &srect,
			DDBLTFAST_WAIT | DDBLTFAST_SRCCOLORKEY );
	}

	/// <summary>
	/// Copies an image from another surface.
	/// </summary>
	void blt( int dstX1, int dstY1, int dstX2, int dstY2, Surface^ source,
					 int srcX1, int srcY1, int srcX2, int srcY2 ) {
		
		RECT drect = Util::toRECT(dstX1,dstY1,dstX2,dstY2);
		RECT srect = Util::toRECT(srcX1,srcY1,srcX2,srcY2);

		blt( drect, source, srect );
	}

	void blt( Point dst, Surface^ source, drawing::Rectangle src ) {
		blt( Util::toRECT( dst, src.Size ), source, Util::toRECT(src) );
	}
private:
	void blt( RECT dst, Surface^ source, RECT src ) {
		DWORD flag;
		flag = DDBLT_WAIT;
		if( source->hasSourceColorKey )
			flag |= DDBLT_KEYSRC;

		Util::clip( dst, src, clip );

		surface->Blt(&dst, source->handle, &src, flag, nullptr);
	}

public:
	void bltAlpha( Point dstPos, Surface^ source, Point srcPos, Size sz ) {
		RECT dst = Util::toRECT( dstPos, sz );
		RECT src = Util::toRECT( srcPos, sz );
		Util::clip( dst, src, clip );
		bltAlphaFast( surface, source->surface,
			dst.left, dst.top,
			src.left, src.top, src.right, src.bottom,
			static_cast<DWORD>(source->colorKey) );
	}

	void bltAlpha( Point dstPos, Surface^ source ) {
		bltAlpha( dstPos, source, Point(), source->size );
	}

	void bltShape( Point dstPos, Surface^ source, Point srcPos, Size sz, Color fill ) {
		RECT dst = Util::toRECT( dstPos, sz );
		RECT src = Util::toRECT( srcPos, sz );
		Util::clip( dst, src, clip );

		::bltShape( surface, source->surface,
			dst.left, dst.top,
			src.left, src.top, src.right, src.bottom,
			static_cast<int>(colorToFill(fill)),
			static_cast<DWORD>(source->colorKey) );
	}

	void bltShape( Point dstPos, Surface^ source, Color fill ) {
		bltShape( dstPos, source, Point(), source->size, fill );
	}

	void blt( Point dstPos, Surface^ source, Point srcPos, Size sz ) {
		RECT drect = Util::toRECT(dstPos,sz);
		RECT srect = Util::toRECT(srcPos,sz);
		blt( drect, source, srect );
	}

	void blt( Point dstPos, Surface^ source ) {
		RECT drect = Util::toRECT(dstPos, source->size );
		RECT srect = Util::toRECT(Point(),source->size);	// use the mpety rect
		blt( drect, source, srect );
	}

	void bltColorTransform( Point dstPos, Surface^ source,
		Point srcPos, Size sz,
		array<Color>^ _srcColors, array<Color>^ _dstColors, bool vflip ) {

		RECT dst = Util::toRECT( dstPos, sz );
		RECT src = Util::toRECT( srcPos, sz );

      if( _srcColors->Length == 0 ){
        blt( dst, source, src );
      }
      else {

        if( vflip )
			// in VFLIP mode, clipping works in a different way.
			Util::clipVflip( dst, src, clip );
		else
			Util::clip( dst, src, clip );

		std::vector<int> srcColors(_srcColors->Length);
		std::vector<int> dstColors(_srcColors->Length);

        for( int i=_srcColors->Length-1; i>=0; i-- ) {
          srcColors[i] = static_cast<int>(colorToFill(_srcColors[i]));
          dstColors[i] = static_cast<int>(colorToFill(_dstColors[i]));
		}

		::bltColorTransform(
			surface, source->surface,
			dst.left, dst.top,
			src.left, src.top, src.right, src.bottom,
			&srcColors[0],
			&dstColors[0],
			srcColors.size(),
			source->colorKey,
			vflip?-1:0 );
      }
	}

	void bltHueTransform( Point dstPos, Surface^ source, Point srcPos, Size sz,
		Color R_dest, Color G_dest, Color B_dest ) {
		RECT dst = Util::toRECT( dstPos, sz );
		RECT src = Util::toRECT( srcPos, sz );
		Util::clip( dst, src, clip );

		//Debug.WriteLine(""+R_dest.ToArgb()+","+G_dest.ToArgb()+","+B_dest.ToArgb());
		::bltHueTransform( surface, source->surface,
			dst.left, dst.top,
			src.left, src.top, src.right, src.bottom,				
			R_dest.ToArgb(), G_dest.ToArgb(), B_dest.ToArgb(),
			source->colorKey );
	}



	/// <summary>
	/// Fills the surface.
	/// </summary>
	void fill( Color c ) {
		BltColorFill(clip, colorToFill(c) );
	}

	void fill( drawing::Rectangle rect, Color c ) {
		RECT r = Util::intersect( Util::toRECT(rect), clip );
		BltColorFill( r, colorToFill(c) );
	}
private:
	HRESULT BltColorFill(RECT r, DWORD color)
	{
		DDBLTFX ddbltfx = {};
		ddbltfx.dwSize = sizeof ddbltfx;
		ddbltfx.dwFillColor = color;
		return surface->Blt(&r, nullptr, nullptr, DDBLT_COLORFILL | DDBLT_WAIT, &ddbltfx);
	}

private:
	int colorKey;
public:
	/// <summary>
	/// Source color key. A mask color that will not be copied to other plains.
	/// </summary>
	property Color sourceColorKey {
		void set(Color value) {
			DDCOLORKEY key = {};
			// TODO: how shall I convert Color to this structure?
			key.dwColorSpaceHighValue = key.dwColorSpaceLowValue = colorKey = (int)colorToFill(value);
			surface->SetColorKey( DDCKEY_SRCBLT, &key );
			hasSourceColorKey = true;

			// TODO: how to remove color key?
		}
	}

	// retruns true if the color at the specified pixel is valid (opaque).
	bool HitTest( Point p )
	{
		return HitTest(p.X, p.Y);
	}

	// retruns true if the color at the specified pixel is valid (opaque).
	bool HitTest( int x, int y )
	{
		if(x<0 || x>size.Width || y<0 || y>size.Height )
			return false;
		return ((getColorAt(x,y)&0xffffff) == colorKey);
	}
private:
	// returns color at specified point.
	// the return value suited for current pixel format.
	// outrange point will raise an error.
	int getColorAt( int x, int y )
	{
		RECT r = {};
		r.left = x;
		r.top = y;
		r.bottom = r.top+1;
		r.right = r.left+1;
		DDSURFACEDESC2 desc = {sizeof desc};
		surface->GetSurfaceDesc( &desc );
		surface->Lock(&r,&desc,DDLOCK_WAIT|DDLOCK_READONLY,0);
		int c = *reinterpret_cast<int*>(static_cast<BYTE*>(desc.lpSurface) + (desc.lPitch * y + desc.dwDepth * x));
		surface->Unlock(&r);
		return c;
	}
public:
	void drawPolygon( Point p1, Point p2, Point p3, Point p4 ) {
		HDC hdc;
		handle->GetDC(&hdc);
		const POINT p[] =
		{
			{p1.X, p1.Y},
			{p2.X, p2.Y}, // {p2.X, p3.Y} -> {p2.X, p2.Y} 2010.03.16 by riorio
			{p3.X, p3.Y},
			{p4.X, p4.Y},
		};
		Polygon( hdc, p, 4 );
		handle->ReleaseDC(hdc);
	}

	void drawBox( drawing::Rectangle r ) {
		HDC hdc;
		handle->GetDC(&hdc);
		::Rectangle(hdc, r.Left, r.Top, r.Right, r.Bottom);
		handle->ReleaseDC(hdc);
	}

	/// <summary>
	/// Tries to recover a lost surface.
	/// </summary>
	void restore() {
		handle->Restore();
	}

//		public void lockTest() {
//			DDSURFACEDESC2 ddsd = new DDSURFACEDESC2();
//			DxVBLib.RECT r = new RECT();
//			r.left = r.top = 0;
//			r.right = size.Width;
//			r.bottom = size.Height;
//			handle.Lock( ref r, ref ddsd, CONST_DDLOCKFLAGS.DDLOCK_WAIT, 0 );  
//			handle.Unlock( ref r );
//		}

public:
	/// <summary>
	/// Makes the bitmap of this surface.
	/// The caller needs to dispose the bitmap.
	/// </summary>
	Bitmap^ createBitmap() {
		Bitmap^ bmp = gcnew Bitmap( size.Width, size.Height );
		GDIGraphics src(this);
		auto_handle<Graphics> dst = Graphics::FromImage(bmp);
		HDC dstHDC = static_cast<HDC>(dst->GetHdc().ToPointer());
		HDC srcHDC = static_cast<HDC>(src.graphics->GetHdc().ToPointer());
		BitBlt( dstHDC, 0, 0, size.Width, size.Height, srcHDC, 0, 0, 0x00CC0020 );
		dst->ReleaseHdc(static_cast<IntPtr>(dstHDC));
		src.graphics->ReleaseHdc(static_cast<IntPtr>(srcHDC));
		return bmp;
	}

	void GDICopyBits(Graphics^ g, drawing::Rectangle dst, drawing::Rectangle src){
		GDIGraphics gg(this);
		HDC dstHDC = static_cast<HDC>(g->GetHdc().ToPointer());
		HDC srcHDC = static_cast<HDC>(gg.graphics->GetHdc().ToPointer());
		StretchBlt( dstHDC, dst.X, dst.Y, dst.Width, dst.Height, 
			srcHDC, src.X, src.Y, src.Width, src.Height, 0x00CC0020 );
		g->ReleaseHdc(static_cast<IntPtr>(dstHDC));
		gg.graphics->ReleaseHdc(static_cast<IntPtr>(srcHDC));
	}

	void GDICopyBits(Graphics^ g, drawing::Rectangle dst, Point src){
		GDIGraphics gg(this);
		HDC dstHDC = static_cast<HDC>(g->GetHdc().ToPointer());
		HDC srcHDC = static_cast<HDC>(gg.graphics->GetHdc().ToPointer());
		BitBlt( dstHDC, dst.X, dst.Y, dst.Width, dst.Height, srcHDC, src.X, src.Y, 0x00CC0020 );
		g->ReleaseHdc(static_cast<IntPtr>(dstHDC));
		gg.graphics->ReleaseHdc(static_cast<IntPtr>(srcHDC));
	}
public:
	property bool IsLost
	{
		bool get()
		{
			return handle->IsLost() != S_OK;
		}
	}
};

GDIGraphics::GDIGraphics( wrapper::Surface^ _surface ) {
	this->surface = _surface;
	IDirectDrawSurface7* p = surface->handle;
	HDC hdcTmp;
	p->GetDC(&hdcTmp);
	hdc = hdcTmp;
	graphics = Graphics::FromHdc(static_cast<IntPtr>(hdc));
}


GDIGraphics::!GDIGraphics() {
	delete graphics;
	IDirectDrawSurface7* p = surface->handle;
	p->ReleaseDC(hdc);
}
//Ç±Ç±Ç‹Ç≈Surface.cs
//Ç±Ç±Ç©ÇÁDirectDraw.cs

/// <summary>
/// DirectDraw root class.
/// </summary>
public ref class wrapper::DirectDraw : System::IDisposable
{
private:
	static DWORD memoryPlace = DDSCAPS_SYSTEMMEMORY;
public:
	static property DDSurfaceAllocation SurfeceAllocation
	{
		DDSurfaceAllocation get() {
			if((memoryPlace&DDSCAPS_VIDEOMEMORY)!=0)
				return DDSurfaceAllocation::ForceVideoMem;
			else if((memoryPlace&DDSCAPS_SYSTEMMEMORY)!=0)
				return DDSurfaceAllocation::ForceSystemMem;
			else
				return DDSurfaceAllocation::Auto;
		}
		void set(DDSurfaceAllocation value) {
			switch(value)
			{
			case DDSurfaceAllocation::ForceSystemMem:
				memoryPlace = DDSCAPS_SYSTEMMEMORY;
				break;
			case DDSurfaceAllocation::ForceVideoMem:
				memoryPlace = DDSCAPS_VIDEOMEMORY;
				break;
			default:
				memoryPlace &= DDSCAPS_OFFSCREENPLAIN;
				break;
			}
		}
	}

protected:
	IDirectDraw7* handle;

public:
	DirectDraw() {
		// initialize DirectDraw
		void* p;
		DirectDrawCreateEx(nullptr, &p, IID_IDirectDraw7, nullptr);
		handle = static_cast<IDirectDraw7*>(p);
		handle->SetCooperativeLevel( 0, DDSCL_NORMAL );	// window mode
	}

	property int totalVideoMemory {
		int get() {
			DDSCAPS2 ddcaps = {};
			ddcaps.dwCaps = DDSCAPS_VIDEOMEMORY;
			DWORD total, free;
			ThrowIfFailed(handle->GetAvailableVidMem(&ddcaps, &total, &free));
			return total;
		}
	}

	property int availableVideoMemory {
		int get() {
			DDSCAPS2 ddcaps = {};
			ddcaps.dwCaps = DDSCAPS_VIDEOMEMORY;
			DWORD total, free;
			ThrowIfFailed(handle->GetAvailableVidMem(&ddcaps, &total, &free));
			return free;
		}
	}

	property String^ displayModeName {
		String^ get() {
			return GetDisplayModeName();
		}
	}

public:
	~DirectDraw() {
		this->!DirectDraw();
	}

	!DirectDraw() {
		if( handle != nullptr )
                  handle->Release();
		handle = nullptr;
	}


	/// <summary>
	/// Creates a blank off-screen surface with the specified size.
	/// </summary>
	Surface^ createOffscreenSurface( int width, int height ) {
		DDSURFACEDESC2 sd = {};
		sd.dwSize = sizeof sd;
		sd.dwFlags =	DDSD_CAPS |
					DDSD_WIDTH |
					DDSD_HEIGHT;

		sd.ddsCaps.dwCaps = DDSCAPS_OFFSCREENPLAIN|memoryPlace;
		sd.dwHeight	= Convert::ToUInt32(height);
		sd.dwWidth	= Convert::ToUInt32(width);
		try
		{
			return CreateSurfaceImpl(handle, sd);
		}
		catch(Exception^ e)
		{
			//for safe
			Debug::WriteLine(String::Format("{0}:({1}x{2})",e->Message,width,height));
			sd.ddsCaps.dwCaps |= DDSCAPS_SYSTEMMEMORY;
			return CreateSurfaceImpl(handle, sd);
		}
	}
private:
	Surface^ CreateSurfaceImpl(IDirectDraw7* dd, DDSURFACEDESC2& sd)
	{
		IDirectDrawSurface7* ps;
		HRESULT hr = dd->CreateSurface(&sd, &ps, nullptr);
		if (FAILED(hr))
		{
			Marshal::ThrowExceptionForHR(hr);
		}
		try
		{
			return gcnew Surface(ps);
		}
		catch(...)
		{
			ps->Release();
			throw;
		}
	}
public:

	Surface^ createOffscreenSurface( Size sz ) {
		return createOffscreenSurface( sz.Width, sz.Height );
	}

	/// <summary>
	/// Creates an off-screen surface from an image.
	/// </summary>
	Surface^ createFromImage( Image^ img ) {
		Surface^ s = createOffscreenSurface( img->Size );
		GDIGraphics g(s);
			// without the size parameter, it doesn't work well with non-standard DPIs.
		g.graphics->DrawImage( img, drawing::Rectangle( Point(), img->Size ) );
		return s;
	}

	/// <summary>
	/// Creates an off-screen surface from an image
	/// and set the source key color to the color of the
	/// top-left corner of the image.
	/// </summary>
	Surface^ createSprite( Bitmap^ img ) {
		Surface^ surface = createFromImage(img);
		surface->sourceColorKey = img->GetPixel(0,0);
		return surface;
	}



	/// <summary>
	/// Returns true if the given exception is thrown because of
	/// a lost surface.
	/// </summary>
	static bool isSurfaceLostException( COMException^ e ) {
		return static_cast<DWORD>(e->ErrorCode) == 0x887601C2;
	}
};

/// <summary>
/// DirectDraw with the primary surface to the window of
/// the specified control.
/// </summary>
public ref class wrapper::WindowedDirectDraw : DirectDraw {
private:
	Surface^ primary;
public:
	property Surface^ primarySurface { Surface^ get() { return primary; } }

	WindowedDirectDraw( Control^ control ) {

		primary = createPrimarySurface();
		// attach window clipper
		IDirectDrawClipper* cp;
		handle->CreateClipper(0, &cp, nullptr);
		cp->SetHWnd(0, static_cast<HWND>(control->Handle.ToPointer()));
		primary->handle->SetClipper(cp);
		primary->clipRect = drawing::Rectangle( int::MinValue/2, int::MinValue/2, int::MaxValue, int::MaxValue );
	}

	/// <summary>
	/// Creates the primary surface.
	/// </summary>
private:
	Surface^ createPrimarySurface() {
		DDSURFACEDESC2 sd= {sizeof sd};


		sd.dwFlags = DDSD_CAPS;
		sd.ddsCaps.dwCaps = DDSCAPS_PRIMARYSURFACE;

		IDirectDrawSurface7* p;
		HRESULT hr = handle->CreateSurface(&sd, &p, nullptr);
		ThrowIfFailed(hr);
		try
		{
			return gcnew Surface(p);
		}
		catch(...)
		{
			p->Release();
			throw;
		}
	}

	~WindowedDirectDraw()
	{
		//delete primary;
		primary = nullptr;
	}
};

//--------

public ref struct freetrain::DirectXWrapper::NightImageBuilder static_class
{
	static int BuildNightImage(Surface^ surface)
	{
		return buildNightImage(surface->handle);
	}
};
