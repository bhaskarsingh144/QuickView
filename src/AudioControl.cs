using System;
using System.Runtime.InteropServices;

internal sealed class AudioControl : IDisposable
{
    private IMMDeviceEnumerator _enumerator;
    private IMMDevice _device;
    private IAudioEndpointVolume _volume;
    private bool _ready;

    public bool Available
    {
        get { return _ready && _volume != null; }
    }

    public string Status { get; private set; }

    public AudioControl()
    {
        Status = "";
        Bind();
    }

    public void Refresh()
    {
        Bind();
    }

    private void Bind()
    {
        Release();
        try
        {
            Type t = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            if (t == null)
            {
                Status = "Audio unavailable";
                return;
            }
            object raw = Activator.CreateInstance(t);
            _enumerator = raw as IMMDeviceEnumerator;
            if (_enumerator == null)
            {
                Status = "Audio unavailable";
                return;
            }
            int hr = _enumerator.GetDefaultAudioEndpoint(0, 1, out _device);
            if (hr != 0 || _device == null)
            {
                Status = "No playback device";
                return;
            }
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            object o;
            hr = _device.Activate(ref iid, 1, IntPtr.Zero, out o);
            _volume = o as IAudioEndpointVolume;
            if (hr != 0 || _volume == null)
            {
                Status = "Volume control unavailable";
                return;
            }
            _ready = true;
            Status = "";
        }
        catch (Exception ex)
        {
            Status = "Audio unavailable";
            Program.LogCrash(ex);
            Release();
        }
    }

    public bool TryGetVolume(out float level01, out bool muted)
    {
        level01 = 0f;
        muted = false;
        if (!Available)
            return false;
        try
        {
            _volume.GetMasterVolumeLevelScalar(out level01);
            _volume.GetMute(out muted);
            return true;
        }
        catch (Exception ex)
        {
            Status = "Audio read failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    public bool TrySetVolume(float level01)
    {
        if (!Available)
            return false;
        if (level01 < 0f) level01 = 0f;
        if (level01 > 1f) level01 = 1f;
        try
        {
            _volume.SetMasterVolumeLevelScalar(level01, Guid.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Status = "Audio set failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    public bool TrySetMute(bool muted)
    {
        if (!Available)
            return false;
        try
        {
            _volume.SetMute(muted, Guid.Empty);
            return true;
        }
        catch (Exception ex)
        {
            Status = "Mute failed";
            Program.LogCrash(ex);
            return false;
        }
    }

    public void Dispose()
    {
        Release();
    }

    private void Release()
    {
        _ready = false;
        if (_volume != null)
        {
            try { Marshal.ReleaseComObject(_volume); } catch { }
            _volume = null;
        }
        if (_device != null)
        {
            try { Marshal.ReleaseComObject(_device); } catch { }
            _device = null;
        }
        if (_enumerator != null)
        {
            try { Marshal.ReleaseComObject(_enumerator); } catch { }
            _enumerator = null;
        }
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int NotImpl1();
        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr pNotify);
        int UnregisterControlChangeNotify(IntPtr pNotify);
        int GetChannelCount(out uint pnChannelCount);
        int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        [PreserveSig]
        int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig]
        int GetMasterVolumeLevelScalar(out float pfLevel);
        int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
        int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
        int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);
        [PreserveSig]
        int GetMute(out bool pbMute);
    }
}
