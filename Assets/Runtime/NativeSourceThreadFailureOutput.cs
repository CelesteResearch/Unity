using System;
using System.Diagnostics;
using System.IO;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Engine.Update;
using Celeste.Game.Execution.Threading;

namespace Celeste.Unity
{
    public sealed class NativeSourceThreadFailureOutput : IOriginalThreadFailureOutput
    {
        private readonly IOriginalGameErrorIdentity info;
        private readonly IOriginalEngineFrameDevice frameDevice;
        private readonly ISourceEngineMainThreadDispatcher dispatcher;

        public NativeSourceThreadFailureOutput(IOriginalGameErrorIdentity actualEngineInfo,
            IOriginalEngineFrameDevice actualFrameDevice, ISourceEngineMainThreadDispatcher dispatcher)
        {
            info = actualEngineInfo ?? throw new ArgumentNullException(nameof(actualEngineInfo));
            frameDevice = actualFrameDevice ?? throw new ArgumentNullException(nameof(actualFrameDevice));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void WriteErrorLog(Exception exception) => OriginalGameErrorLog.Write(exception, info);

        public void OpenErrorLog()
        {
            if (!File.Exists(OriginalGameErrorLog.Filename))
                return;
            dispatcher.Invoke(() => Process.Start(new ProcessStartInfo(OriginalGameErrorLog.Filename)
            {
                UseShellExecute = true
            }));
        }

        public void ExitGame() => dispatcher.Invoke(frameDevice.Exit);
    }
}
