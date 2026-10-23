using Celeste.Core.Components;
using System;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Runtime;
using Celeste.Mods.PandorasBox.Runtime.Terrain.Glitches;

namespace Celeste.Mods.PandorasBox.Modules
{
    public sealed partial class PandorasBoxModule
    {
        private TileGlitcherCache tileGlitcherCache;

        internal static TileGlitcherCache GetTileGlitcherCache(ISourceEngineContext engine)
        {
            foreach (var module in ((ISourceEverestModulesEnvironment)engine).Modules)
                if (module is PandorasBoxModule owner)
                    return owner.tileGlitcherCache ?? (owner.tileGlitcherCache = new TileGlitcherCache(engine));
            throw new InvalidOperationException("The owning PandorasBox module is not loaded.");
        }

        private void UnloadTileGlitchers()
        {
            tileGlitcherCache?.Clear();
            tileGlitcherCache = null;
        }
    }
}
