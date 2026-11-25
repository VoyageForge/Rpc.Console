using VoyageForge.Rpc.Executor;

namespace VoyageForge.Terminal.Commands
{
    internal sealed class ConsolePanelController : ControllerBase
    {
        public void Clear() => Console.Instance?.Clear();
        public void Hide() => Console.Instance?.Hide();

        internal static LocalExecutor CreateExecutor()
        {
            var executor = new LocalExecutor();
            executor.Register(() => new ConsolePanelController());
            return executor;
        }
    }
}
