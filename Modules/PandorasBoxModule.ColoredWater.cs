using Celeste.Mods.PandorasBox.Runtime.Water;

namespace Celeste.Mods.PandorasBox.Modules
{
    public sealed partial class PandorasBoxModule
    {
        private ColoredWaterJumpHooks coloredWaterJumpHooks;

        private void LoadColoredWater() => coloredWaterJumpHooks = new ColoredWaterJumpHooks(SourceEngine);

        private void UnloadColoredWater()
        {
            coloredWaterJumpHooks?.Dispose();
            coloredWaterJumpHooks = null;
        }
    }
}
