using Celeste.Mods.PandorasBox.Runtime.Transport.Pipes;

namespace Celeste.Mods.PandorasBox.Modules
{
    public sealed partial class PandorasBoxModule
    {
        private ClearPipeHooks clearPipeHooks;
        private void LoadClearPipes() => clearPipeHooks = new ClearPipeHooks(SourceEngine);

        private void UnloadClearPipes()
        {
            clearPipeHooks?.Dispose();
            clearPipeHooks = null;
        }
    }
}
