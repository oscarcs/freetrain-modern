#include "stdafx.h"
#include <dshow.h>
#include <comdef.h>
#include <vcclr.h>
#include "DirectXWrapper.h"


namespace wrapper = freetrain::DirectXWrapper;
 
using System::Convert;
using System::Exception;
using System::String;
using System::Math;
using System::Diagnostics::Debug;
using System::Windows::Forms::Control;
using System::Windows::Forms::Message;
using System::Windows::Forms::IWin32Window;
using System::Runtime::InteropServices::COMException;
namespace
{
template<typename T>
inline void CreateInstance(const CLSID& clsid, T*% dst)
{
	T* tmp;
	ThrowIfFailed(CoCreateInstance(clsid, nullptr, CLSCTX_ALL, IID_PPV_ARGS(&tmp)));
	dst = tmp;
}

template<typename T>
inline void CreateInstance(const CLSID& clsid, const IID& iid, T*% dst)
{
	T* tmp;
	ThrowIfFailed(CoCreateInstance(clsid, nullptr, CLSCTX_ALL, iid, reinterpret_cast<void**>(&tmp)));
	dst = tmp;
}

template<typename T, typename U>
inline void QueryInterface(T* src, U*% dst)
{
	U* tmp;
	ThrowIfFailed(src->QueryInterface(&tmp));
	dst = tmp;
}

template<typename T, typename U>
inline void QueryInterface(T* src, const IID& iid, U*% dst)
{
	U* tmp;
	ThrowIfFailed(src->QueryInterface(iid, reinterpret_cast<void**>(&tmp)));
	dst = tmp;
}

inline MUSIC_TIME GetMusicTime(IDirectMusicPerformance* p)
{
	REFERENCE_TIME rt;
	MUSIC_TIME mt;
	ThrowIfFailed(p->GetTime(&rt, &mt));
	return mt;
}

inline REFERENCE_TIME GetClockTime(IDirectMusicPerformance* p)
{
	REFERENCE_TIME rt;
	MUSIC_TIME mt;
	ThrowIfFailed(p->GetTime(&rt, &mt));
	return rt;
}

inline MUSIC_TIME GetLength(IDirectMusicSegment* s)
{
	MUSIC_TIME length;
	ThrowIfFailed(s->GetLength(&length));
	return length;
}
}
_COM_SMARTPTR_TYPEDEF(IBasicAudio, __uuidof(IBasicAudio));

public ref class wrapper::AudioPath
{
internal:
	// Handle for DirectMusicAudioPath8
	IDirectMusicAudioPath* handle;

	// AudioPath object constructor
	AudioPath(IDirectMusicAudioPath* handle) : handle(handle) {
	}
public:
	/// <summary>
	/// AudioPath object disposed
	/// </summary>
	~AudioPath() {
		this->!AudioPath();
	}

	!AudioPath()
	{
		if (handle != nullptr) {
			handle->Release();
		}
		handle = nullptr;
	}
};

FILE *fp;


/// <summary>
/// Use DirectShow to manage BGM.
/// </summary>
public ref class wrapper::BGM
{
	literal int DS_NOTIFY_CODE = 123;
	literal int WM_DS_NOTIFY = WM_APP + 1;
public:
    /// <summary>BGM object constructor</summary>
	BGM();
private:
    // Create inner objects
    void createObjects() {
		CreateInstance(CLSID_FilterGraph, filter);
		QueryInterface(filter, mediaPos);
		QueryInterface(filter, mediaEvent);

		mediaEvent->SetNotifyWindow(reinterpret_cast<OAHWND>(wnd->Handle.ToPointer()), WM_DS_NOTIFY, DS_NOTIFY_CODE);
	}

public:
    /// <summary>BGM object disposed</summary>
    ~BGM() {
		delete wnd;
		this->!BGM();
	}

    // Release inner objects
	!BGM() {
		releaseObjects();
	}
private:
	void releaseObjects() {
		if (filter != nullptr) {
			filter->Release();
			mediaPos->Release();
			mediaEvent->Release();
		}
		filter = nullptr;
		mediaPos = nullptr;
		mediaEvent = nullptr;
	}

	IMediaControl* filter;
	IMediaPosition* mediaPos;
	IMediaEventEx* mediaEvent;

    /// <summary> Window that receives notification. </summary>
	Control^ wnd;
public:
    /// <summary>BGM filename</summary>
	property String^ fileName {
		void set(String^ value) {

			releaseObjects();
			createObjects();

			// not use marshal_as : riorio 2010.03.16 Fixed
			// original is -  _bstr_t file = msclr::interop::marshal_as<bstr_t>(value);

       		pin_ptr<const wchar_t> wch = PtrToStringChars( value );
			_bstr_t bstrt( wch );

			filter->RenderFile( bstrt );
		}
	}

    /// <summary>play BGM</summary>
	void run() {
		filter->Run();
	}

    /// <summary>pause BGM play</summary>
	void pause() {
		filter->Pause();
	}
	
    /// <summary>stop BGM play</summary>
	void stop() {
		filter->Stop();
	}

	/// <summary>BGM volume</summary>
    /// <value>0 to 100</value>
	property int volume {
		int get() {
			IBasicAudioPtr ba(filter);
			long volume;
			ThrowIfFailed(ba->get_Volume(&volume));
			double db = volume / 100.0;
			// 10^(db/20))*100
			return static_cast<int>(Math::Pow(10, db / 20) * 100);
		}
		void set(int value) {
			// log10(V/100)*20 = (log10(V)-2)*20
			int v = value;
			if (v <= 0) {
                v=1;
            }
			IBasicAudioPtr ba(filter);
			ThrowIfFailed(ba->put_Volume(static_cast<int>((Math::Log10(v) - 2) * 20 * 100)));
		}
	}

	/// <summary>BGM loop play</summary>
    /// <value>true is loop play</value>
    /// <value>false is not loop play</value>
    bool loop;

internal:
	void notify() {
		long code;
		LONG_PTR param1, param2;

                while( SUCCEEDED( mediaEvent->GetEvent(&code, &param1, &param2, 0))){
			
		     mediaEvent->FreeEventParams(code, param1, param2);

			if (code == EC_COMPLETE) {
				// Debug::WriteLine("BGM: rewinded");
				// rewind to the start
				mediaPos->put_CurrentPosition(0);
			}
		}
	}
private:
	/// <summary>Window that receives notification from DirectShow.</summary>
	ref class NotificationWindow;
};

ref class wrapper::BGM::NotificationWindow : Control {
	initonly BGM^ parent;

internal:
	NotificationWindow(BGM^ parent) : parent(parent) {}

protected:
	virtual void WndProc(Message% m) override {
		if (m.Msg == WM_DS_NOTIFY) {
			parent->notify();
		}
		__super::WndProc(m);
	}
};

wrapper::BGM::BGM() {
	loop = true;
	// launch an invisible window to receive notification
	wnd = gcnew NotificationWindow(this);
	createObjects();
}

namespace
{
	ref struct DirectAudio static_class
	{
		//internal static initonly DirectX8Class dx = new DirectX8Class();
		
	internal:
		static IDirectMusicLoader8* loader;
	private:
		static DirectAudio()
		{
			CreateInstance(CLSID_DirectMusicLoader, IID_IDirectMusicLoader8, loader);
		}
	};
}

public ref class wrapper::Segment
{
internal:
	IDirectMusicSegment8* handle;

private:
	Segment( IDirectMusicSegment8* handle ) : handle(handle) {}
	
public:
	static Segment^ fromFile( String^ fileName ) {
		IDirectMusicSegment8* pSegment = nullptr;
		try {
			pin_ptr<const WCHAR> pfilename = PtrToStringChars(fileName);
			ThrowIfFailed(DirectAudio::loader->LoadObjectFromFile(
				CLSID_DirectMusicSegment, IID_IDirectMusicSegment8,
				const_cast<WCHAR*>(pfilename), reinterpret_cast<void**>(&pSegment)));
			return gcnew Segment(pSegment);
		} catch( Exception^ e ) {
			if (pSegment != nullptr) {
				pSegment->Release();
			}
			throw gcnew Exception("unable to load music file: " + fileName, e);
		}
	}

	static Segment^ fromMidiFile( String^ fileName ) {
		Segment^ seg = fromFile(fileName);
		seg->handle->SetParam(GUID_StandardMIDIFile, 0xFFFFFFFF, 0, 0, nullptr);
		return seg;
	}

	~Segment() {
		this->!Segment();
	}

	!Segment() {
		if(handle!=nullptr) {
			handle->Release();
		}
		handle = nullptr;
	}



	/// <summary>
	/// Prepares this sound object for the play by the performance object.
	/// </summary>
	void downloadTo( Performance^ p );

	/// <summary>
	/// Reverses the effect of the downloadTo method.
	/// </summary>
	void unloadFrom( Performance^ p );

	Segment^ clone() {
		IDirectMusicSegment *p;
		ThrowIfFailed(handle->Clone(0, 0, &p));
		IDirectMusicSegment8 *p8;
		QueryInterface(p, IID_IDirectMusicSegment8, p8);
		return gcnew Segment(p8);
	}

	property int repeats {
		int get() {
			DWORD ret;
			ThrowIfFailed(handle->GetRepeats(&ret));
			return Convert::ToInt32(static_cast<UINT>(ret));
		}
		void set(int value) {
			ThrowIfFailed(handle->SetRepeats(Convert::ToUInt32(value)));
		}
	}
};

public ref class wrapper::Performance
{
internal:
	IDirectMusicPerformance8* handle;

public:
	Performance( IWin32Window^ owner ) {
		CreateInstance(CLSID_DirectMusicPerformance, IID_IDirectMusicPerformance8, handle);

		DMUS_AUDIOPARAMS param = {sizeof param};
		IDirectSound* nullDs = nullptr;
		IDirectMusic* nullDm = nullptr;

		// TODO: learn more about this initialization process
		handle->InitAudio(
			&nullDm,
			&nullDs,
			static_cast<HWND>(owner->Handle.ToPointer()),
			DMUS_APATH_DYNAMIC_STEREO, 16,
			DMUS_AUDIOF_ALL,
			&param);
	}

	~Performance() {
		this->!Performance();
	}

	!Performance() {
		if (handle != nullptr) {
			handle->CloseDown();
			handle->Release();
		}
		handle = nullptr;
	}

	/// <summary>
	/// Plays the given segment exclusively.
	/// </summary>
	SegmentState^ playExclusive( Segment^ seg );

	/// <summary>
	/// Plays the given segment immediately.
	/// </summary>
	SegmentState^ play( Segment^ seg ) {
		return play(seg, 0);
	}

	/// <summary>
	/// Plays the given segment after the specified lead time (in milliseconds)
	/// </summary>
	SegmentState^ play( Segment^ seg, int leadTime );

	/// <summary>
	/// Creates an audio path from the properties of the given segment.
	/// </summary>
	AudioPath^ createAudioPath( Segment^ seg ) {
		IUnknown* config;
		seg->handle->GetAudioPathConfig(&config);
		IDirectMusicAudioPath *path;
		ThrowIfFailed(handle->CreateAudioPath(config, TRUE, &path));
		try
		{
			return gcnew AudioPath(path);
		}
		catch(...)
		{
			path->Release();
			throw;
		}
	}
};

void wrapper::Segment::downloadTo( Performance^ p ) {
	IDirectMusicAudioPath* path;
	p->handle->GetDefaultAudioPath(&path);
	handle->Download(path);
	path->Release();
}

void wrapper::Segment::unloadFrom( Performance^ p ) {
	IDirectMusicAudioPath* path;
	p->handle->GetDefaultAudioPath(&path);
	handle->Unload(path);
	path->Release();
}

public ref class wrapper::SegmentState
{
internal:
	SegmentState( Performance^ perf, IDirectMusicSegmentState* state, int endTime ) {
		this->performance = perf;
		this->state = state;
		this->estimatedEndTime = endTime;
	}
private:
	initonly Performance^ performance;
	initonly IDirectMusicSegmentState* state;
	initonly int estimatedEndTime;

	/// <summary>
	/// Returns true if this segment is still being played.
	/// </summary>
public:
	property bool isPlaying {
		bool get() {
//Debug::Print(String::Format("SegmentState.isPlaying1:estimatedEndTime={0}", estimatedEndTime));
//Debug::Print(String::Format("SegmentState.isPlaying2:GetMusicTime={0}", GetMusicTime(performance->handle)));

            if( performance->handle->IsPlaying(nullptr, state)== S_OK )
				return true;

			if( GetMusicTime(performance->handle) < estimatedEndTime )
				return true;

//				// because of the latency, sometimes this method false even if it's not being played yet.
//				// thus make sure that it has the reasonable start time.
//				int currentTime = performance.handle.GetMusicTime();
//				if( currentTime <= state.GetStartTime() )
//					return true;	// this will be played in a future
			
			return false;
		}
	}
};

wrapper::SegmentState^ wrapper::Performance::playExclusive( Segment^ seg ) {
	IDirectMusicSegmentState* state;
	handle->PlaySegment(seg->handle, 0, 0, &state);
	return gcnew SegmentState( this, 
		state, //handle->PlaySegmentEx( seg->handle, 0, 0, nullptr, nullptr ),
		GetMusicTime(handle) + GetLength(seg->handle) );
}

wrapper::SegmentState^ wrapper::Performance::play( Segment^ seg, int leadTime ){
	//Debug::Print(String::Format("Performance.play1:leadTime={0}", leadTime));

	if( leadTime!=0 ) {
		MUSIC_TIME t;
        //readtime(msec) => referencetime([100ns]) 
		handle->ReferenceToMusicTime(leadTime*10*1000 + GetClockTime(handle), &t);
		leadTime = t;
	}
	if( leadTime<0 )
		leadTime = 0;

	//Debug::Print(String::Format("Performance.play2:leadTime={0}", leadTime));
	//Debug::Print(String::Format("Performance.play3:GetMusicTime={0}", GetMusicTime(handle)));
	//Debug::Print(String::Format("Performance.play4:GetLength={0}", GetLength(seg->handle)));


	IDirectMusicSegmentState* ss;
	ThrowIfFailed(handle->PlaySegment(seg->handle, DMUS_SEGF_SECONDARY, leadTime, &ss));
	return gcnew SegmentState( this, ss,
		GetLength(seg->handle) + leadTime );
}
