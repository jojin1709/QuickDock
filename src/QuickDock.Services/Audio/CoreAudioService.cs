using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;

namespace QuickDock.Services.Audio;

public sealed class CoreAudioService : IAudioService
{
    private readonly ILogger<CoreAudioService> _logger;
    private readonly AudioEndpointVolumeCallback _callback;

    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMDevice? _defaultDevice;
    private IAudioEndpointVolume? _endpointVolume;
    private bool _isCallbackRegistered;
    private bool _isDisposed;

    public event EventHandler<AudioVolumeChangedEventArgs>? VolumeChanged;

    public CoreAudioService(ILogger<CoreAudioService> logger)
    {
        _logger = logger;
        _callback = new AudioEndpointVolumeCallback(this);
        InitializeEndpoint();
    }

    private void InitializeEndpoint()
    {
        try
        {
            var enumeratorType = Type.GetTypeFromCLSID(ComGuids.CLSID_MMDeviceEnumerator);
            if (enumeratorType == null)
            {
                _logger.LogWarning("MMDeviceEnumerator COM type could not be loaded.");
                return;
            }

            _deviceEnumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;
            int hr = _deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out _defaultDevice);
            if (hr != 0 || _defaultDevice == null)
            {
                _logger.LogWarning("No default audio render endpoint available (hr=0x{Hr:X8}).", hr);
                return;
            }

            Guid iid = ComGuids.IID_IAudioEndpointVolume;
            hr = _defaultDevice.Activate(ref iid, CLSCTX.CLSCTX_ALL, IntPtr.Zero, out object volumeObj);
            if (hr != 0 || volumeObj == null)
            {
                _logger.LogWarning("Failed to activate IAudioEndpointVolume (hr=0x{Hr:X8}).", hr);
                return;
            }

            _endpointVolume = (IAudioEndpointVolume)volumeObj;

            hr = _endpointVolume.RegisterControlChangeNotify(_callback);
            _isCallbackRegistered = (hr == 0);
            _logger.LogInformation("CoreAudioService initialized successfully. Callback registered: {Registered}", _isCallbackRegistered);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize CoreAudioService.");
        }
    }

    public float GetMasterVolume()
    {
        if (_endpointVolume == null) return 0f;

        try
        {
            int hr = _endpointVolume.GetMasterVolumeLevelScalar(out float level);
            if (hr == 0) return Math.Clamp(level, 0f, 1f);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "GetMasterVolume failed.");
        }

        return 0f;
    }

    public void SetMasterVolume(float level)
    {
        level = Math.Clamp(level, 0f, 1f);
        if (_endpointVolume == null) return;

        try
        {
            _endpointVolume.SetMasterVolumeLevelScalar(level, Guid.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetMasterVolume failed for level {Level}.", level);
        }
    }

    public void IncreaseVolume(float step = 0.05f)
    {
        float current = GetMasterVolume();
        SetMasterVolume(current + step);
    }

    public void DecreaseVolume(float step = 0.05f)
    {
        float current = GetMasterVolume();
        SetMasterVolume(current - step);
    }

    public bool GetMuteState()
    {
        if (_endpointVolume == null) return false;

        try
        {
            int hr = _endpointVolume.GetMute(out bool isMuted);
            if (hr == 0) return isMuted;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "GetMuteState failed.");
        }

        return false;
    }

    public void SetMuteState(bool mute)
    {
        if (_endpointVolume == null) return;

        try
        {
            _endpointVolume.SetMute(mute, Guid.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetMuteState failed for mute={Mute}.", mute);
        }
    }

    public void ToggleMute()
    {
        SetMuteState(!GetMuteState());
    }

    public string GetDefaultDeviceName()
    {
        if (_defaultDevice == null) return "No audio device";

        try
        {
            int hr = _defaultDevice.OpenPropertyStore(STGM.STGM_READ, out IPropertyStore store);
            if (hr == 0 && store != null)
            {
                var key = ComGuids.PKEY_Device_FriendlyName;
                store.GetValue(ref key, out PROPVARIANT pv);
                string name = pv.GetValue();
                pv.Clear();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to query audio device name.");
        }

        return "Speakers";
    }

    internal void OnVolumeNotification(float volume, bool isMuted)
    {
        VolumeChanged?.Invoke(this, new AudioVolumeChangedEventArgs(volume, isMuted));
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_endpointVolume != null && _isCallbackRegistered)
        {
            try
            {
                _endpointVolume.UnregisterControlChangeNotify(_callback);
            }
            catch
            {
                // Best effort unregister
            }
        }

        if (_endpointVolume != null) Marshal.ReleaseComObject(_endpointVolume);
        if (_defaultDevice != null) Marshal.ReleaseComObject(_defaultDevice);
        if (_deviceEnumerator != null) Marshal.ReleaseComObject(_deviceEnumerator);

        _endpointVolume = null;
        _defaultDevice = null;
        _deviceEnumerator = null;
    }

    #region COM Interop Definitions

    private static class ComGuids
    {
        public static readonly Guid CLSID_MMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
        public static readonly Guid IID_IMMDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");
        public static readonly Guid IID_IAudioEndpointVolume = new("5BC64874-38B4-4B78-9B04-273F65E10777");
        public static readonly PropertyKey PKEY_Device_FriendlyName = new(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;

        public PropertyKey(Guid guid, uint id)
        {
            fmtid = guid;
            pid = id;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPVARIANT
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr unionmember;

        public string GetValue()
        {
            // VT_LPWSTR = 31
            if (vt == 31 && unionmember != IntPtr.Zero)
            {
                return Marshal.PtrToStringUni(unionmember) ?? string.Empty;
            }
            return string.Empty;
        }

        public void Clear()
        {
            PropVariantClear(ref this);
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PROPVARIANT pvar);
    }

    private enum EDataFlow
    {
        eRender,
        eCapture,
        eAll
    }

    private enum ERole
    {
        eConsole,
        eMultimedia,
        eCommunications
    }

    private enum CLSCTX : uint
    {
        CLSCTX_INPROC_SERVER = 0x1,
        CLSCTX_INPROC_HANDLER = 0x2,
        CLSCTX_LOCAL_SERVER = 0x4,
        CLSCTX_ALL = 0x17
    }

    private enum STGM : uint
    {
        STGM_READ = 0x00000000
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IntPtr ppDevices);
        [PreserveSig]
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        int RegisterEndpointNotificationCallback(IntPtr pClient);
        int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig]
        int OpenPropertyStore(STGM stgmAccess, out IPropertyStore ppProperties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        int GetState(out uint pdwState);
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        int GetCount(out uint cProps);
        int GetAt(uint iProp, out PropertyKey pkey);
        [PreserveSig]
        int GetValue(ref PropertyKey key, out PROPVARIANT pv);
        int SetValue(ref PropertyKey key, ref PROPVARIANT propvar);
        int Commit();
    }

    [ComImport]
    [Guid("5BC64874-38B4-4B78-9B04-273F65E10777")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig]
        int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig]
        int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
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
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        int VolumeStepUp(Guid pguidEventContext);
        int VolumeStepDown(Guid pguidEventContext);
        int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    [ComImport]
    [Guid("657804FA-D6AD-4496-8560-E5D526D0C169")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig]
        int OnNotify(IntPtr pNotify);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIO_VOLUME_NOTIFICATION_DATA
    {
        public Guid guidEventContext;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bMuted;
        public float fMasterVolume;
        public uint nChannels;
    }

    private sealed class AudioEndpointVolumeCallback : IAudioEndpointVolumeCallback
    {
        private readonly CoreAudioService _parent;

        public AudioEndpointVolumeCallback(CoreAudioService parent)
        {
            _parent = parent;
        }

        public int OnNotify(IntPtr pNotify)
        {
            if (pNotify != IntPtr.Zero)
            {
                var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(pNotify);
                _parent.OnVolumeNotification(data.fMasterVolume, data.bMuted);
            }
            return 0;
        }
    }

    #endregion
}

