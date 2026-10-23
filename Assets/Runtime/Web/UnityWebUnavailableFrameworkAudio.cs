using System;
using Celeste.Audio.IO;
using Celeste.Audio.Interop;
using Celeste.Core.Diagnostics;

namespace Celeste.Unity.Web
{
    // Original uses FMOD. These independent framework facilities remain unavailable;
    // construction does not probe a native SDK, and any actual use fails explicitly.
    internal sealed unsafe class UnityWebUnavailableFrameworkAudio : ISourceFAudioIOFactory,
        ISourceFAudioIO, ISourceMicrophoneIO, ISourceTheoraIO
    {
        public ISourceFAudioIO Create(ISourceFnaLoggingEnvironment actualLogging)
        {
            if (actualLogging == null) throw new ArgumentNullException(nameof(actualLogging));
            return this;
        }

        private static PlatformNotSupportedException Missing() => new PlatformNotSupportedException(
            "FAudio, framework media and microphone IO are not configured on this Web host; original game audio requires the separate real FMOD device.");

        public void F3DAudioCalculate(byte[] Instance, ref SourceFAudioLayout.F3DAUDIO_LISTENER pListener,
            ref SourceFAudioLayout.F3DAUDIO_EMITTER pEmitter, uint Flags,
            ref SourceFAudioLayout.F3DAUDIO_DSP_SETTINGS pDSPSettings) => throw Missing();

        public void F3DAudioInitialize(uint SpeakerChannelMask, float SpeedOfSound, byte[] Instance) => throw Missing();
        public uint FAPOBase_Release(IntPtr fapo) => throw Missing();
        public uint FAudioCreate(out IntPtr ppFAudio, uint Flags, uint XAudio2Processor) => throw Missing();
        public uint FAudioCreateReverb(out IntPtr ppApo, uint Flags) => throw Missing();
        public uint FAudioSourceVoice_ExitLoop(IntPtr voice, uint OperationSet) => throw Missing();
        public uint FAudioSourceVoice_FlushSourceBuffers(IntPtr voice) => throw Missing();

        public void FAudioSourceVoice_GetState(IntPtr voice, out SourceFAudioLayout.FAudioVoiceState pVoiceState,
            uint Flags) => throw Missing();

        public uint FAudioSourceVoice_SetFrequencyRatio(IntPtr voice, float Ratio, uint OperationSet) =>
            throw Missing();

        public uint FAudioSourceVoice_Start(IntPtr voice, uint Flags, uint OperationSet) => throw Missing();
        public uint FAudioSourceVoice_Stop(IntPtr voice, uint Flags, uint OperationSet) => throw Missing();

        public uint FAudioSourceVoice_SubmitSourceBuffer(IntPtr voice, ref SourceFAudioLayout.FAudioBuffer pBuffer,
            IntPtr pBufferWMA) => throw Missing();

        public uint FAudioSourceVoice_SubmitSourceBuffer(IntPtr voice, ref SourceFAudioLayout.FAudioBuffer pBuffer,
            ref SourceFAudioLayout.FAudioBufferWMA pBufferWMA) => throw Missing();

        public void FAudioVoice_DestroyVoice(IntPtr voice) => throw Missing();
        public void FAudioVoice_GetVolume(IntPtr voice, out float pVolume) => throw Missing();

        public uint FAudioVoice_SetEffectParameters(IntPtr voice, uint EffectIndex, IntPtr pParameters,
            uint ParametersByteSize, uint OperationSet) => throw Missing();

        public uint FAudioVoice_SetFilterParameters(IntPtr voice,
            ref SourceFAudioLayout.FAudioFilterParameters pParameters, uint OperationSet) => throw Missing();

        public uint FAudioVoice_SetOutputMatrix(IntPtr voice, IntPtr pDestinationVoice, uint SourceChannels,
            uint DestinationChannels, IntPtr pLevelMatrix, uint OperationSet) => throw Missing();

        public uint FAudioVoice_SetOutputVoices(IntPtr voice, ref SourceFAudioLayout.FAudioVoiceSends pSendList) =>
            throw Missing();

        public uint FAudioVoice_SetVolume(IntPtr voice, float Volume, uint OperationSet) => throw Missing();

        public uint FAudio_CreateMasteringVoice(IntPtr audio, out IntPtr ppMasteringVoice, uint InputChannels,
            uint InputSampleRate, uint Flags, uint DeviceIndex, IntPtr pEffectChain) => throw Missing();

        public uint FAudio_CreateSourceVoice(IntPtr audio, out IntPtr ppSourceVoice,
            ref SourceFAudioLayout.FAudioWaveFormatEx pSourceFormat, uint Flags, float MaxFrequencyRatio,
            IntPtr pCallback, IntPtr pSendList, IntPtr pEffectChain) => throw Missing();

        public uint FAudio_CreateSourceVoice(IntPtr audio, out IntPtr ppSourceVoice, IntPtr pSourceFormat, uint Flags,
            float MaxFrequencyRatio, IntPtr pCallback, IntPtr pSendList, IntPtr pEffectChain) => throw Missing();

        public uint FAudio_CreateSubmixVoice(IntPtr audio, out IntPtr ppSubmixVoice, uint InputChannels,
            uint InputSampleRate, uint Flags, uint ProcessingStage, IntPtr pSendList, IntPtr pEffectChain) =>
            throw Missing();

        public uint FAudio_GetDeviceCount(IntPtr audio, out uint pCount) => throw Missing();

        public uint FAudio_GetDeviceDetails(IntPtr audio, uint Index,
            out SourceFAudioLayout.FAudioDeviceDetails pDeviceDetails) => throw Missing();

        public uint FAudio_Release(IntPtr audio) => throw Missing();
        public IntPtr Malloc(int bytes) => throw Missing();
        public void Free(IntPtr memory) => throw Missing();
        public void LogError(string message) => throw Missing();
        public void LogWarn(string message) => throw Missing();
        public void XNA_SetSongVolume(float volume) => throw Missing();
        public uint XNA_VisualizationEnabled() => throw Missing();
        public void XNA_EnableVisualization(uint enabled) => throw Missing();
        public void XNA_PauseSong() => throw Missing();
        public void XNA_ResumeSong() => throw Missing();
        public void XNA_StopSong() => throw Missing();
        public void XNA_GetSongVisualizationData(float[] frequencies, float[] samples, uint count) => throw Missing();
        public uint XNA_GetSongEnded() => throw Missing();
        public void XNA_SongQuit() => throw Missing();
        public void XNA_SongInit() => throw Missing();
        public float XNA_PlaySong(string handle) => throw Missing();
        public int InitializeCaptureAudio() => throw Missing();
        public int GetNumAudioDevices(int isCapture) => throw Missing();
        public string GetAudioDeviceName(int index, int isCapture) => throw Missing();

        public uint OpenAudioDevice(string device, int isCapture, ref SourceCaptureAudioSpec desired,
            out SourceCaptureAudioSpec obtained, int allowedChanges) => throw Missing();

        public int GetMicrophoneSamples(uint handle, byte[] buffer, int offset, int count) => throw Missing();
        public void StartMicrophone(uint handle) => throw Missing();
        public void StopMicrophone(uint handle) => throw Missing();
        public int GetMicrophoneQueuedBytes(uint handle) => throw Missing();
        public int tf_fopen(string fileName, out IntPtr decoder) => throw Missing();

        public void tf_videoinfo(IntPtr decoder, out int width, out int height, out double fps,
            out SourceTheoraPixelFormat format) => throw Missing();

        public int tf_setvideotrack(IntPtr decoder, int track) => throw Missing();
        public int tf_setaudiotrack(IntPtr decoder, int track) => throw Missing();
        public int tf_close(ref IntPtr decoder) => throw Missing();
    }
}
