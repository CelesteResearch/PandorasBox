using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;

namespace Celeste.Mods.PandorasBox.Modules
{
    public sealed partial class PandorasBoxModule(ISourceEngineContext engine) : SourceEverestModule(engine)
    {
        // Water and transport interactions own their lifecycle hooks.
        public override void Load()
        {
            LoadPlayerClones();
            LoadWaterDrowning();
            LoadClearPipes();
            LoadDreamDashes();
            LoadColoredWater();
        }

        public override void Unload()
        {
            UnloadPlayerClones();
            UnloadWaterDrowning();
            UnloadClearPipes();
            UnloadDreamDashes();
            UnloadColoredWater();
            UnloadTileGlitchers();
        }
    }
}
