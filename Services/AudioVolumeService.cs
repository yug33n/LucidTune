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

        private List<ISimpleAudioVolume> GetTargetAudioVolumes(out string? matchedDisplayName)
        {
            matchedDisplayName = null;
            var list = new List<ISimpleAudioVolume>();
            if (string.IsNullOrWhiteSpace(_targetAppId))
            {
                TargetAppDisplayName = null;
                return list;
            }

            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                int hr = enumerator.GetDefaultAudioEndpoint(eRender, eMultimedia, out var device);
                if (hr != 0 || device == null) return list;

                Guid sessionManagerGuid = typeof(IAudioSessionManager2).GUID;
                hr = device.Activate(ref sessionManagerGuid, CLSCTX_ALL, IntPtr.Zero, out var sessionManagerObj);
                if (hr != 0 || sessionManagerObj == null) return list;

                var sessionManager = sessionManagerObj as IAudioSessionManager2;
                if (sessionManager == null) return list;

                hr = sessionManager.GetSessionEnumerator(out var sessionEnum);
                if (hr != 0 || sessionEnum == null) return list;

                hr = sessionEnum.GetCount(out int count);
                if (hr != 0 || count == 0) return list;

                for (int i = 0; i < count; i++)
                {
                    hr = sessionEnum.GetSession(i, out var control);
                    if (hr != 0 || control == null) continue;

                    var control2 = control as IAudioSessionControl2;
                    var simpleVol = control as ISimpleAudioVolume;

                    if (simpleVol != null)
                    {
                        uint pid = 0;
                        string sessionId = "";
                        int isSystem = 0;

                        if (control2 != null)
                        {
                            control2.GetProcessId(out pid);
                            control2.GetSessionIdentifier(out sessionId);
                            isSystem = control2.IsSystemSoundsSession();
                        }

                        // S_OK (0) means this IS a Windows system sounds session. S_FALSE (1) means regular app session.
                        if (isSystem == 0 || pid == 0) continue;

                        string procName = GetProcessName(pid);

                        if (IsMatch(_targetAppId, pid, procName, sessionId))
                        {
                            list.Add(simpleVol);
                            if (string.IsNullOrEmpty(matchedDisplayName))
                            {
                                matchedDisplayName = !string.IsNullOrEmpty(procName) ? procName : _targetAppId;
                            }
                        }
                    }
                }

                TargetAppDisplayName = matchedDisplayName;
                return list;
            }
            catch
            {
                TargetAppDisplayName = null;
                return list;
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
            string sId = (sessionId ?? "").ToLowerInvariant();

            string normTarget = Normalize(target);
            string normProc = Normalize(pName);
            string normSession = Normalize(sId);

            // 1. Yandex Music & Yandex Browser matchers
            bool targetIsYandex = normTarget.Contains("yandex") || normTarget.Contains("яндекс") || normTarget.Contains("ruyandex");
            if (targetIsYandex)
            {
                if (normProc.Contains("yandex") || normProc.Contains("яндекс") || normProc.Contains("music") || normProc.Contains("музыка") || normProc.Contains("browser"))
                    return true;
                if (normSession.Contains("yandex") || normSession.Contains("яндекс") || normSession.Contains("music") || normSession.Contains("ruyandex"))
                    return true;
            }

            // 2. Spotify matchers
            bool targetIsSpotify = normTarget.Contains("spotify") || normTarget.Contains("спотифай");
            if (targetIsSpotify)
            {
                if (normProc.Contains("spotify") || normProc.Contains("спотифай") || normSession.Contains("spotify"))
                    return true;
            }

            // 3. VK Music matchers
            bool targetIsVk = normTarget.Contains("vk") || normTarget.Contains("вк");
            if (targetIsVk)
            {
                if (normProc.Contains("vk") || normProc.Contains("вк") || normSession.Contains("vk"))
                    return true;
            }

            // 4. Telegram matchers
            bool targetIsTelegram = normTarget.Contains("telegram") || normTarget.Contains("телеграм");
            if (targetIsTelegram)
            {
                if (normProc.Contains("telegram") || normSession.Contains("telegram"))
                    return true;
            }

            // 5. Popular browser matchers
            if (normTarget.Contains("chrome") && (normProc.Contains("chrome") || normSession.Contains("chrome"))) return true;
            if ((normTarget.Contains("edge") || normTarget.Contains("msedge")) && (normProc.Contains("edge") || normProc.Contains("msedge") || normSession.Contains("edge"))) return true;
            if (normTarget.Contains("firefox") && (normProc.Contains("firefox") || normSession.Contains("firefox"))) return true;
            if (normTarget.Contains("opera") && (normProc.Contains("opera") || normSession.Contains("opera"))) return true;
            if (normTarget.Contains("brave") && (normProc.Contains("brave") || normSession.Contains("brave"))) return true;
            if (normTarget.Contains("vivaldi") && (normProc.Contains("vivaldi") || normSession.Contains("vivaldi"))) return true;
            if ((normTarget.Contains("browser") || normTarget.Contains("yandex")) && normProc.Equals("browser")) return true;

            // 6. Direct containment
            if (!string.IsNullOrEmpty(normProc) && normProc.Length >= 3)
            {
                if (normTarget.Contains(normProc) || normProc.Contains(normTarget))
                    return true;
            }

            // 7. Session ID containment
            if (!string.IsNullOrEmpty(normSession) && normSession.Length >= 4)
            {
                if (normTarget.Contains(normSession) || normSession.Contains(normTarget))
                    return true;
            }

            // 8. Executable file path on disk
            if (pid != 0)
            {
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    string? exePath = proc.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        string normPath = Normalize(exePath.ToLowerInvariant());
                        if (targetIsYandex && (normPath.Contains("yandex") || normPath.Contains("яндекс") || normPath.Contains("music"))) return true;
                        if (targetIsSpotify && normPath.Contains("spotify")) return true;
                        if (!string.IsNullOrEmpty(normTarget) && normTarget.Length >= 4 && normPath.Contains(normTarget)) return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private static string Normalize(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            var sb = new System.Text.StringBuilder(str.Length);
            foreach (char c in str)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }

        public float GetVolume()
        {
            try
            {
                var volumes = GetTargetAudioVolumes(out var matchedName);
                if (volumes.Count > 0)
                {
                    TargetAppDisplayName = matchedName;
                    foreach (var v in volumes)
                    {
                        int hr = v.GetMasterVolume(out float level);
                        if (hr == 0 && level > 0)
                        {
                            return Math.Clamp(level, 0.0f, 1.0f);
                        }
                    }

                    if (volumes[0].GetMasterVolume(out float firstLevel) == 0)
                    {
                        return Math.Clamp(firstLevel, 0.0f, 1.0f);
                    }
                }

                TargetAppDisplayName = null;
                return GetMasterVolume();
            }
            catch
            {
                TargetAppDisplayName = null;
                return GetMasterVolume();
            }
        }

        public void SetVolume(float level)
        {
            float clamped = Math.Clamp(level, 0.0f, 1.0f);
            try
            {
                var volumes = GetTargetAudioVolumes(out var matchedName);
                if (volumes.Count > 0)
                {
                    TargetAppDisplayName = matchedName;
                    Guid emptyGuid = Guid.Empty;
                    foreach (var v in volumes)
                    {
                        try
                        {
                            v.SetMasterVolume(clamped, ref emptyGuid);
                        }
                        catch { }
                    }
                    return;
                }

                TargetAppDisplayName = null;
                SetMasterVolume(clamped);
            }
            catch
            {
                TargetAppDisplayName = null;
                SetMasterVolume(clamped);
            }
        }

        public bool IsMuted()
        {
            try
            {
                var volumes = GetTargetAudioVolumes(out var matchedName);
                if (volumes.Count > 0)
                {
                    TargetAppDisplayName = matchedName;
                    foreach (var v in volumes)
                    {
                        int hr = v.GetMute(out bool isMuted);
                        if (hr == 0 && isMuted) return true;
                    }
                    return false;
                }

                TargetAppDisplayName = null;
                return IsMasterMuted();
            }
            catch
            {
                TargetAppDisplayName = null;
                return IsMasterMuted();
            }
        }

        public void ToggleMute()
        {
            try
            {
                var volumes = GetTargetAudioVolumes(out var matchedName);
                if (volumes.Count > 0)
                {
                    TargetAppDisplayName = matchedName;
                    bool currentMute = IsMuted();
                    bool newMute = !currentMute;
                    Guid emptyGuid = Guid.Empty;
                    foreach (var v in volumes)
                    {
                        try
                        {
                            v.SetMute(newMute, ref emptyGuid);
                        }
                        catch { }
                    }
                    return;
                }

                TargetAppDisplayName = null;
                ToggleMasterMute();
            }
            catch
            {
                TargetAppDisplayName = null;
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
