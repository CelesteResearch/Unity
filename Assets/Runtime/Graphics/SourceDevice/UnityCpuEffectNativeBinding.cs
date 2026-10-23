#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using Celeste.Graphics.Effects;

namespace Celeste.Unity.Graphics.SourceDevice
{
    public sealed class UnityCpuEffectNativeBinding : ICpuEffectNativeBinding
    {
        public static readonly UnityCpuEffectNativeBinding Instance = new();
        private const string Library = "__Internal";
        private UnityCpuEffectNativeBinding() { }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr CelesteEffectGetError();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectFrontendCreate([MarshalAs(UnmanagedType.LPUTF8Str)] string profile,
            out IntPtr frontend);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CelesteEffectFrontendDestroy(IntPtr frontend);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectCreate(IntPtr frontend, IntPtr code, uint length, out IntPtr effect,
            out IntPtr metadata);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectClone(IntPtr source, out IntPtr effect, out IntPtr metadata);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CelesteEffectDestroy(IntPtr effect);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectSetTechnique(IntPtr effect, IntPtr technique);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectApply(IntPtr effect, uint pass, IntPtr changes);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectGetShaderCount(IntPtr effect, out uint count);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectGetTechniqueCount(IntPtr effect, out uint count);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectGetTechnique(IntPtr effect, uint index,
            out IntPtr technique, out uint passCount);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectGetPassStageFlags(IntPtr effect, uint technique,
            uint pass, out uint flags);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectGetPassShaderCount(IntPtr effect, uint technique,
            uint pass, int vertex, out uint count);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectAcquirePassShader(IntPtr effect, uint technique,
            uint pass, int vertex, uint index, out IntPtr shader);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectAcquireShader(IntPtr effect, uint index, out IntPtr shader);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectAcquireBoundShader(IntPtr effect, int vertex, out IntPtr shader);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectGetShaderDescription(IntPtr shader,
            out CpuShaderDescription description);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CelesteEffectReleaseShader(IntPtr shader);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectBorrowRegisters(IntPtr effect, out CpuRegisterView view);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectCopyRegisters(IntPtr effect, in CpuRegisterView target);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        private static extern int CelesteEffectCopyHlslConstants(IntPtr effect, int vertex, IntPtr target,
            uint capacityBytes);

        IntPtr ICpuEffectNativeBinding.CelesteEffectGetError()
            => CelesteEffectGetError();

        int ICpuEffectNativeBinding.CelesteEffectFrontendCreate(string profile,
            out IntPtr frontend)
            => CelesteEffectFrontendCreate(profile, out frontend);

        void ICpuEffectNativeBinding.CelesteEffectFrontendDestroy(IntPtr frontend)
            => CelesteEffectFrontendDestroy(frontend);

        int ICpuEffectNativeBinding.CelesteEffectCreate(IntPtr frontend, IntPtr code, uint length, out IntPtr effect,
            out IntPtr metadata)
            => CelesteEffectCreate(frontend, code, length, out effect, out metadata);

        int ICpuEffectNativeBinding.CelesteEffectClone(IntPtr source, out IntPtr effect, out IntPtr metadata)
            => CelesteEffectClone(source, out effect, out metadata);

        void ICpuEffectNativeBinding.CelesteEffectDestroy(IntPtr effect)
            => CelesteEffectDestroy(effect);

        int ICpuEffectNativeBinding.CelesteEffectSetTechnique(IntPtr effect, IntPtr technique)
            => CelesteEffectSetTechnique(effect, technique);

        int ICpuEffectNativeBinding.CelesteEffectApply(IntPtr effect, uint pass, IntPtr changes)
            => CelesteEffectApply(effect, pass, changes);

        int ICpuEffectNativeBinding.CelesteEffectGetShaderCount(IntPtr effect, out uint count)
            => CelesteEffectGetShaderCount(effect, out count);

        int ICpuEffectNativeBinding.CelesteEffectGetTechniqueCount(IntPtr effect, out uint count)
            => CelesteEffectGetTechniqueCount(effect, out count);

        int ICpuEffectNativeBinding.CelesteEffectGetTechnique(IntPtr effect, uint index,
            out IntPtr technique, out uint passCount)
            => CelesteEffectGetTechnique(effect, index, out technique, out passCount);

        int ICpuEffectNativeBinding.CelesteEffectGetPassStageFlags(IntPtr effect, uint technique,
            uint pass, out uint flags)
            => CelesteEffectGetPassStageFlags(effect, technique, pass, out flags);

        int ICpuEffectNativeBinding.CelesteEffectGetPassShaderCount(IntPtr effect, uint technique,
            uint pass, int vertex, out uint count)
            => CelesteEffectGetPassShaderCount(effect, technique, pass, vertex, out count);

        int ICpuEffectNativeBinding.CelesteEffectAcquirePassShader(IntPtr effect, uint technique,
            uint pass, int vertex, uint index, out IntPtr shader)
            => CelesteEffectAcquirePassShader(effect, technique, pass, vertex, index, out shader);

        int ICpuEffectNativeBinding.CelesteEffectAcquireShader(IntPtr effect, uint index, out IntPtr shader)
            => CelesteEffectAcquireShader(effect, index, out shader);

        int ICpuEffectNativeBinding.CelesteEffectAcquireBoundShader(IntPtr effect, int vertex, out IntPtr shader)
            => CelesteEffectAcquireBoundShader(effect, vertex, out shader);

        int ICpuEffectNativeBinding.CelesteEffectGetShaderDescription(IntPtr shader,
            out CpuShaderDescription description)
            => CelesteEffectGetShaderDescription(shader, out description);

        void ICpuEffectNativeBinding.CelesteEffectReleaseShader(IntPtr shader)
            => CelesteEffectReleaseShader(shader);

        int ICpuEffectNativeBinding.CelesteEffectBorrowRegisters(IntPtr effect, out CpuRegisterView view)
            => CelesteEffectBorrowRegisters(effect, out view);

        int ICpuEffectNativeBinding.CelesteEffectCopyRegisters(IntPtr effect, in CpuRegisterView target)
            => CelesteEffectCopyRegisters(effect, in target);

        int ICpuEffectNativeBinding.CelesteEffectCopyHlslConstants(IntPtr effect, int vertex, IntPtr target,
            uint capacityBytes)
            => CelesteEffectCopyHlslConstants(effect, vertex, target, capacityBytes);

    }
}
#endif
