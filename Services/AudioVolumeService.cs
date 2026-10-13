using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace LucidTune.Services
{
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr pNotify);
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
        [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
        [PreserveSig] int VolumeStepUp(ref Guid pguidEventContext);
        [PreserveSig] int VolumeStepDown(ref Guid pguidEventContext);
        [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
        [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig] int GetState(out uint pdwState);
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint dwStateMask, out IntPtr ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(ref Guid AudioSessionGuid, uint StreamFlags, out IntPtr SessionControl);
        [PreserveSig] int GetSimpleAudioVolume(ref Guid AudioSessionGuid, uint StreamFlags, out IntPtr AudioVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
        [PreserveSig] int RegisterSessionNotification(IntPtr NewSessionNotifications);
        [PreserveSig] int UnregisterSessionNotification(IntPtr NewSessionNotifications);
        [PreserveSig] int RegisterDuckNotification(string sessionID, IntPtr duckNotification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr duckNotification);
    }

    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int SessionCount);
        [PreserveSig] int GetSession(int SessionIndex, out IAudioSessionControl Session);
    }

    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl
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
    }

    [Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioSessionControl2 : IAudioSessionControl
    {
        [PreserveSig] new int GetState(out int pRetVal);
        [PreserveSig] new int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] new int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] new int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] new int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] new int GetGroupingParam(out Guid pRetVal);
        [PreserveSig] new int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        [PreserveSig] new int RegisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] new int UnregisterAudioSessionNotification(IntPtr NewNotifications);

        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int GetProcessId(out uint pRetVal);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float fLevel, ref Guid EventContext);
        [PreserveSig] int GetMasterVolume(out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid EventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    public class AudioVolumeService
    {
        private const int eRender = 0;
        private const int eMultimedia = 1;
        private const int CLSCTX_ALL = 23;

        private string? _targetAppId;

        public string? TargetAppDisplayName { get; private set; }
        public bool IsTargetingApplication => !string.IsNullOrEmpty(TargetAppDisplayName);

        public void SetTargetApp(string? appId)
        {
            _targetAppId = appId;
        }

        private IAudioEndpointVolume? GetMasterVolumeEndpoint()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                int hr = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out var device);
                if (hr != 0 || device == null) return null;

                Guid iid = typeof(IAudioEndpointVolume).GUID;
                hr = device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out var endpointObj);
                if (hr != 0 || endpointObj == null) return null;

                return endpointObj as IAudioEndpointVolume;
            }
            catch
            {
                return null;
            }
        }

        private ISimpleAudioVolume? GetTargetAudioVolume()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                int hr = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out var device);
                if (hr != 0 || device == null) return null;

                Guid sessionManagerGuid = typeof(IAudioSessionManager2).GUID;
                hr = device.Activate(ref sessionManagerGuid, CLSCTX_ALL, IntPtr.Zero, out var sessionManagerObj);
                if (hr != 0 || sessionManagerObj == null) return null;

                var sessionManager = sessionManagerObj as IAudioSessionManager2;
                if (sessionManager == null) return null;

                hr = sessionManager.GetSessionEnumerator(out var sessionEnum);
                if (hr != 0 || sessionEnum == null) return null;

                hr = sessionEnum.GetCount(out int count);
                if (hr != 0 || count == 0) return null;

                ISimpleAudioVolume? matchedVolume = null;
                ISimpleAudioVolume? activeFallbackVolume = null;
                string? matchedName = null;
                string? fallbackName = null;

                for (int i = 0; i < count; i++)
                {
                    hr = sessionEnum.GetSession(i, out var control);
                    if (hr != 0 || control == null) continue;

                    var control2 = control as IAudioSessionControl2;
                    var simpleVol = control as ISimpleAudioVolume;

                    if (control2 != null && simpleVol != null)
                    {
                        control2.GetProcessId(out uint pid);
                        if (pid == 0) continue; // Skip idle / system sound endpoints

                        control2.GetSessionIdentifier(out string sessionId);
                        control.GetState(out int state);

                        string procName = GetProcessName(pid);

                        // If session is actively outputting sound (state == 1)
                        if (state == 1 && activeFallbackVolume == null)
                        {
                            activeFallbackVolume = simpleVol;
                            fallbackName = procName;
                        }

                        if (!string.IsNullOrWhiteSpace(_targetAppId))
                        {
                            if (IsMatch(_targetAppId, pid, procName, sessionId))
                            {
                                matchedName = !string.IsNullOrEmpty(procName) ? procName : _targetAppId;
                                if (state == 1)
                                {
                                    TargetAppDisplayName = matchedName;
                                    return simpleVol;
                                }
                                matchedVolume ??= simpleVol;
                            }
                        }
                    }
                }

                if (matchedVolume != null)
                {
                    TargetAppDisplayName = matchedName;
                    return matchedVolume;
                }

                if (activeFallbackVolume != null)
                {
                    TargetAppDisplayName = fallbackName;
                    return activeFallbackVolume;
                }

                TargetAppDisplayName = null;
                return null;
            }
            catch
            {
                TargetAppDisplayName = null;
                return null;
            }
        }

        private static string GetProcessName(uint pid)
        {
            if (pid == 0) return "";
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                return proc.ProcessName;
            }
            catch
            {
                return "";
            }
        }

        private static bool IsMatch(string targetAppId, uint pid, string procName, string? sessionId)
        {
            if (string.IsNullOrWhiteSpace(targetAppId)) return false;

            string target = targetAppId.ToLowerInvariant();
            string pName = procName.ToLowerInvariant();

            // 1. Direct process name containment or localized synonyms
            if (!string.IsNullOrEmpty(pName))
            {
                if (target.Contains(pName) || pName.Contains(target))
                    return true;

                // Yandex Music synonyms
                if ((target.Contains("yandex") || target.Contains("яндекс")) &&
                    (pName.Contains("yandex") || pName.Contains("яндекс") || pName.Contains("музыка")))
                    return true;

                // Spotify synonyms
                if ((target.Contains("spotify") || target.Contains("спотифай")) &&
                    (pName.Contains("spotify") || pName.Contains("спотифай")))
                    return true;

                // VK Music synonyms
                if ((target.Contains("vk") || target.Contains("вк")) &&
                    (pName.Contains("vk") || pName.Contains("вк")))
                    return true;
            }

            // 2. Executable file name or installation folder path
            if (pid != 0)
            {
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    string? path = proc.MainModule?.FileName?.ToLowerInvariant();
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (target.Contains("yandex") && path.Contains("yandex")) return true;
                        if (target.Contains("spotify") && path.Contains("spotify")) return true;
                        if (target.Contains("chrome") && path.Contains("chrome")) return true;
                        if (target.Contains("firefox") && path.Contains("firefox")) return true;
                        if (target.Contains("edge") && (path.Contains("edge") || path.Contains("msedge"))) return true;
                        if (target.Contains("telegram") && path.Contains("telegram")) return true;

                        string fileName = Path.GetFileNameWithoutExtension(path);
                        if (target.Contains(fileName) || fileName.Contains(target)) return true;
                    }
                }
                catch { }
            }

            // 3. Audio session identifier string
            if (!string.IsNullOrEmpty(sessionId))
            {
                string sId = sessionId.ToLowerInvariant();
                if (target.Contains(sId) || sId.Contains(target))
                    return true;
            }

            return false;
        }

        public float GetVolume()
        {
            try
            {
                var appVolume = GetTargetAudioVolume();
                if (appVolume != null)
                {
                    int hr = appVolume.GetMasterVolume(out float level);
                    if (hr == 0) return Math.Clamp(level, 0.0f, 1.0f);
                }

                return GetMasterVolume();
            }
            catch
            {
                return GetMasterVolume();
            }
        }

        public void SetVolume(float level)
        {
            float clamped = Math.Clamp(level, 0.0f, 1.0f);
            try
            {
                var appVolume = GetTargetAudioVolume();
                if (appVolume != null)
                {
                    Guid emptyGuid = Guid.Empty;
                    appVolume.SetMasterVolume(clamped, ref emptyGuid);
                    return;
                }

                SetMasterVolume(clamped);
            }
            catch
            {
                SetMasterVolume(clamped);
            }
        }

        public bool IsMuted()
        {
            try
            {
                var appVolume = GetTargetAudioVolume();
                if (appVolume != null)
                {
                    int hr = appVolume.GetMute(out bool isMuted);
                    if (hr == 0) return isMuted;
                }

                return IsMasterMuted();
            }
            catch
            {
                return IsMasterMuted();
            }
        }

        public void ToggleMute()
        {
            try
            {
                var appVolume = GetTargetAudioVolume();
                if (appVolume != null)
                {
                    int hr = appVolume.GetMute(out bool isMuted);
                    if (hr == 0)
                    {
                        Guid emptyGuid = Guid.Empty;
                        appVolume.SetMute(!isMuted, ref emptyGuid);
                        return;
                    }
                }

                ToggleMasterMute();
            }
            catch
            {
                ToggleMasterMute();
            }
        }

        public float GetMasterVolume()
        {
            try
            {
                var endpoint = GetMasterVolumeEndpoint();
                if (endpoint == null) return 0.5f;

                int hr = endpoint.GetMasterVolumeLevelScalar(out float level);
                if (hr == 0) return Math.Clamp(level, 0.0f, 1.0f);
            }
            catch { }
            return 0.5f;
        }

        public void SetMasterVolume(float level)
        {
            try
            {
                var endpoint = GetMasterVolumeEndpoint();
                if (endpoint == null) return;

                float clamped = Math.Clamp(level, 0.0f, 1.0f);
                Guid emptyGuid = Guid.Empty;
                endpoint.SetMasterVolumeLevelScalar(clamped, ref emptyGuid);
            }
            catch { }
        }

        public bool IsMasterMuted()
        {
            try
            {
                var endpoint = GetMasterVolumeEndpoint();
                if (endpoint == null) return false;

                int hr = endpoint.GetMute(out bool isMuted);
                if (hr == 0) return isMuted;
            }
            catch { }
            return false;
        }

        public void ToggleMasterMute()
        {
            try
            {
                var endpoint = GetMasterVolumeEndpoint();
                if (endpoint == null) return;

                int hr = endpoint.GetMute(out bool isMuted);
                if (hr == 0)
                {
                    Guid emptyGuid = Guid.Empty;
                    endpoint.SetMute(!isMuted, ref emptyGuid);
                }
            }
            catch { }
        }
    }
}
