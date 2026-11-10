using System.Runtime.InteropServices;

namespace IvoryIsland.Services;

/// <summary>
/// Event-driven Windows Core Audio (WASAPI) service using IAudioEndpointVolume and IMMNotificationClient.
/// Controls the default Windows system output endpoint volume and mute state, synchronizes with hardware
/// and keyboard volume keys in real time, and recovers automatically when audio output devices change.
/// Also provides optional process-level audio volume lookup via ISimpleAudioVolume where available.
/// </summary>
public sealed class AudioVolumeService : IDisposable
{
    private readonly object _comLock = new();
    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMDevice? _defaultEndpoint;
    private IAudioEndpointVolume? _endpointVolume;
    private EndpointVolumeCallback? _volumeCallback;
    private DeviceNotificationClient? _deviceCallback;

    private double _currentVolumePercent = 70.0;
    private bool _isMuted;
    private bool _isAvailable;
    private bool _isDisposed;
    private readonly Guid _eventContextGuid = Guid.NewGuid();

    public double CurrentVolumePercent => _currentVolumePercent;
    public bool IsMuted => _isMuted;
    public bool IsAvailable => _isAvailable;

    public event EventHandler<AudioVolumeChangedEventArgs>? VolumeChanged;

    public void Initialize()
    {
        if (_isDisposed)
            return;

        lock (_comLock)
        {
            try
            {
                var enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                if (enumeratorType is null)
                    return;

                _deviceEnumerator = (IMMDeviceEnumerator?)Activator.CreateInstance(enumeratorType);
                if (_deviceEnumerator is null)
                    return;

                _deviceCallback = new DeviceNotificationClient(this);
                _deviceEnumerator.RegisterEndpointNotificationCallback(_deviceCallback);

                AttachToDefaultOutputEndpointUnsafe();
            }
            catch
            {
                _isAvailable = false;
            }
        }
    }

    private void AttachToDefaultOutputEndpointUnsafe()
    {
        DetachEndpointUnsafe();

        if (_deviceEnumerator is null)
            return;

        try
        {
            // eRender = 0, eMultimedia = 1
            int hr = _deviceEnumerator.GetDefaultAudioEndpoint(0, 1, out _defaultEndpoint);
            if (hr != 0 || _defaultEndpoint is null)
            {
                _isAvailable = false;
                return;
            }

            Guid iidAudioEndpointVolume = typeof(IAudioEndpointVolume).GUID;
            hr = _defaultEndpoint.Activate(ref iidAudioEndpointVolume, 23 /* CLSCTX_ALL */, IntPtr.Zero, out object? endpointObj);
            if (hr != 0 || endpointObj is not IAudioEndpointVolume endpointVol)
            {
                _isAvailable = false;
                return;
            }

            _endpointVolume = endpointVol;
            _volumeCallback = new EndpointVolumeCallback(this);
            _endpointVolume.RegisterControlChangeNotify(_volumeCallback);

            if (_endpointVolume.GetMasterVolumeLevelScalar(out float levelScalar) == 0)
            {
                _currentVolumePercent = Math.Clamp(Math.Round(levelScalar * 100.0), 0.0, 100.0);
            }

            if (_endpointVolume.GetMute(out bool muted) == 0)
            {
                _isMuted = muted;
            }

            _isAvailable = true;
        }
        catch
        {
            _isAvailable = false;
        }
    }

    private void DetachEndpointUnsafe()
    {
        if (_endpointVolume is not null)
        {
            try
            {
                if (_volumeCallback is not null)
                    _endpointVolume.UnregisterControlChangeNotify(_volumeCallback);
            }
            catch { }

            try { Marshal.ReleaseComObject(_endpointVolume); } catch { }
            _endpointVolume = null;
        }

        _volumeCallback = null;

        if (_defaultEndpoint is not null)
        {
            try { Marshal.ReleaseComObject(_defaultEndpoint); } catch { }
            _defaultEndpoint = null;
        }
    }

    public void SetVolumePercent(double percent)
    {
        if (_isDisposed)
            return;

        double clamped = Math.Clamp(percent, 0.0, 100.0);
        float scalar = (float)(clamped / 100.0);

        lock (_comLock)
        {
            if (_endpointVolume is null)
                return;

            try
            {
                var ctx = _eventContextGuid;
                _endpointVolume.SetMasterVolumeLevelScalar(scalar, ref ctx);
                _currentVolumePercent = Math.Round(clamped);

                if (_currentVolumePercent > 0 && _isMuted)
                {
                    _endpointVolume.SetMute(false, ref ctx);
                    _isMuted = false;
                }
            }
            catch
            {
                // Endpoint may have disconnected; trigger re-attach
                AttachToDefaultOutputEndpointUnsafe();
            }
        }

        VolumeChanged?.Invoke(this, new AudioVolumeChangedEventArgs(_currentVolumePercent, _isMuted, isExternalChange: false));
    }

    public void StepVolume(double deltaPercent)
    {
        SetVolumePercent(_currentVolumePercent + deltaPercent);
    }

    public void SetMute(bool mute)
    {
        if (_isDisposed)
            return;

        lock (_comLock)
        {
            if (_endpointVolume is null)
                return;

            try
            {
                var ctx = _eventContextGuid;
                _endpointVolume.SetMute(mute, ref ctx);
                _isMuted = mute;
            }
            catch
            {
                AttachToDefaultOutputEndpointUnsafe();
            }
        }

        VolumeChanged?.Invoke(this, new AudioVolumeChangedEventArgs(_currentVolumePercent, _isMuted, isExternalChange: false));
    }

    public void ToggleMute() => SetMute(!_isMuted);

    /// <summary>
    /// Optional per-application process-level volume control via ISimpleAudioVolume.
    /// Note: Browser sessions share process-level audio streams per browser audio process, not per tab.
    /// </summary>
    public bool TrySetProcessVolume(uint processId, float volumeScalar)
    {
        if (_isDisposed || processId == 0)
            return false;

        lock (_comLock)
        {
            if (_defaultEndpoint is null)
                return false;

            IAudioSessionManager2? sessionMgr = null;
            IAudioSessionEnumerator? sessionEnum = null;
            try
            {
                Guid iidMgr = typeof(IAudioSessionManager2).GUID;
                if (_defaultEndpoint.Activate(ref iidMgr, 23, IntPtr.Zero, out object? mgrObj) != 0 ||
                    mgrObj is not IAudioSessionManager2 mgr)
                {
                    return false;
                }

                sessionMgr = mgr;
                if (sessionMgr.GetSessionEnumerator(out sessionEnum) != 0 || sessionEnum is null)
                    return false;

                if (sessionEnum.GetCount(out int count) != 0)
                    return false;

                for (int i = 0; i < count; i++)
                {
                    if (sessionEnum.GetSession(i, out IAudioSessionControl? ctrl) == 0 && ctrl is not null)
                    {
                        try
                        {
                            if (ctrl is IAudioSessionControl2 ctrl2 &&
                                ctrl2.GetProcessId(out uint pid) == 0 &&
                                pid == processId &&
                                ctrl is ISimpleAudioVolume simpleVol)
                            {
                                var ctx = _eventContextGuid;
                                simpleVol.SetMasterVolume(Math.Clamp(volumeScalar, 0f, 1f), ref ctx);
                                return true;
                            }
                        }
                        finally
                        {
                            try { Marshal.ReleaseComObject(ctrl); } catch { }
                        }
                    }
                }
            }
            catch
            {
            }
            finally
            {
                if (sessionEnum is not null) try { Marshal.ReleaseComObject(sessionEnum); } catch { }
                if (sessionMgr is not null) try { Marshal.ReleaseComObject(sessionMgr); } catch { }
            }
        }

        return false;
    }

    internal void HandleExternalEndpointNotify(float masterVolumeScalar, bool isMuted, Guid eventContext)
    {
        if (_isDisposed)
            return;

        double newPercent = Math.Clamp(Math.Round(masterVolumeScalar * 100.0), 0.0, 100.0);
        bool isSelf = eventContext == _eventContextGuid;

        _currentVolumePercent = newPercent;
        _isMuted = isMuted;

        if (!isSelf)
        {
            VolumeChanged?.Invoke(this, new AudioVolumeChangedEventArgs(newPercent, isMuted, isExternalChange: true));
        }
    }

    internal void HandleDefaultDeviceChanged()
    {
        if (_isDisposed)
            return;

        lock (_comLock)
        {
            AttachToDefaultOutputEndpointUnsafe();
        }

        VolumeChanged?.Invoke(this, new AudioVolumeChangedEventArgs(_currentVolumePercent, _isMuted, isExternalChange: false));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;
        _isDisposed = true;

        lock (_comLock)
        {
            DetachEndpointUnsafe();

            if (_deviceEnumerator is not null)
            {
                try
                {
                    if (_deviceCallback is not null)
                        _deviceEnumerator.UnregisterEndpointNotificationCallback(_deviceCallback);
                }
                catch { }

                try { Marshal.ReleaseComObject(_deviceEnumerator); } catch { }
                _deviceEnumerator = null;
            }
        }
    }

    #region WASAPI COM Callback Implementations

    [StructLayout(LayoutKind.Sequential)]
    private struct AUDIO_VOLUME_NOTIFICATION_DATA
    {
        public Guid guidEventContext;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bMuted;
        public float fMasterVolume;
        public uint nChannels;
    }

    private sealed class EndpointVolumeCallback : IAudioEndpointVolumeCallback
    {
        private readonly AudioVolumeService _owner;
        public EndpointVolumeCallback(AudioVolumeService owner) => _owner = owner;

        public int OnNotify(IntPtr pNotify)
        {
            if (pNotify == IntPtr.Zero)
                return 0;

            try
            {
                var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(pNotify);
                _owner.HandleExternalEndpointNotify(data.fMasterVolume, data.bMuted, data.guidEventContext);
            }
            catch
            {
            }
            return 0;
        }
    }

    private sealed class DeviceNotificationClient : IMMNotificationClient
    {
        private readonly AudioVolumeService _owner;
        public DeviceNotificationClient(AudioVolumeService owner) => _owner = owner;

        public int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState) => 0;
        public int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId) => 0;
        public int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId) => 0;

        public int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId)
        {
            // eRender == 0, eMultimedia == 1 (or eConsole == 0)
            if (flow == 0 && (role == 0 || role == 1))
            {
                _owner.HandleDefaultDeviceChanged();
            }
            return 0;
        }

        public int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PROPERTYKEY key) => 0;
    }

    #endregion

    #region WASAPI COM Interfaces

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public int pid;
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? ppEndpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice? ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? ppInterface);
    }

    [ComImport]
    [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int newState);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string defaultDeviceId);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PROPERTYKEY key);
    }

    [ComImport]
    [Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr pNotify);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
        [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
        [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr AudioSessionGuid, uint StreamFlags, out IntPtr SessionControl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr AudioSessionGuid, uint StreamFlags, out ISimpleAudioVolume AudioVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator? SessionEnum);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int SessionCount);
        [PreserveSig] int GetSession(int SessionCount, out IAudioSessionControl? Session);
    }

    [ComImport]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
    }

    [ComImport]
    [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int pRetVal);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetGroupingParam(out Guid pRetVal);
        [PreserveSig] int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int GetProcessId(out uint pRetVal);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float fLevel, ref Guid EventContext);
        [PreserveSig] int GetMasterVolume(out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid EventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    #endregion
}

public sealed class AudioVolumeChangedEventArgs : EventArgs
{
    public double VolumePercent { get; }
    public bool IsMuted { get; }
    public bool IsExternalChange { get; }

    public AudioVolumeChangedEventArgs(double volumePercent, bool isMuted, bool isExternalChange)
    {
        VolumePercent = volumePercent;
        IsMuted = isMuted;
        IsExternalChange = isExternalChange;
    }
}
