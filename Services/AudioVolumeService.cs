using System;
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

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    public class AudioVolumeService
    {
        private const int eRender = 0;
        private const int eMultimedia = 1;
        private const int CLSCTX_ALL = 23;

        private IAudioEndpointVolume? GetVolumeEndpoint()
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

        public float GetMasterVolume()
        {
            try
            {
                var endpoint = GetVolumeEndpoint();
                if (endpoint == null) return 0.5f;

                int hr = endpoint.GetMasterVolumeLevelScalar(out float level);
                if (hr == 0) return Math.Clamp(level, 0.0f, 1.0f);
            }
            catch
            {
                // Fallback to default
            }

            return 0.5f;
        }

        public void SetMasterVolume(float level)
        {
            try
            {
                var endpoint = GetVolumeEndpoint();
                if (endpoint == null) return;

                float clamped = Math.Clamp(level, 0.0f, 1.0f);
                Guid emptyGuid = Guid.Empty;
                endpoint.SetMasterVolumeLevelScalar(clamped, ref emptyGuid);
            }
            catch
            {
                // Ignore volume update errors
            }
        }

        public bool IsMuted()
        {
            try
            {
                var endpoint = GetVolumeEndpoint();
                if (endpoint == null) return false;

                int hr = endpoint.GetMute(out bool isMuted);
                if (hr == 0) return isMuted;
            }
            catch
            {
                // Ignore
            }

            return false;
        }

        public void ToggleMute()
        {
            try
            {
                var endpoint = GetVolumeEndpoint();
                if (endpoint == null) return;

                int hr = endpoint.GetMute(out bool isMuted);
                if (hr == 0)
                {
                    Guid emptyGuid = Guid.Empty;
                    endpoint.SetMute(!isMuted, ref emptyGuid);
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}
