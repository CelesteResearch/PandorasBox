using Celeste.Mods.PandorasBox.Runtime.Players.Clones;

namespace Celeste.Mods.PandorasBox.Modules;

public sealed partial class PandorasBoxModule
{
    private PlayerCloneRuntime playerClones;
    private void LoadPlayerClones() => playerClones = new(SourceEngine);
    private void UnloadPlayerClones() => playerClones?.Dispose();
}
