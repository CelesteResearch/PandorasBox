// SPDX-License-Identifier: MIT

using Celeste.Mods.PandorasBox.Runtime.Water;

namespace Celeste.Mods.PandorasBox.Modules;

public sealed partial class PandorasBoxModule
{
    private WaterDrowningRuntime waterDrowning;
    private void LoadWaterDrowning() => waterDrowning = new(SourceEngine);
    private void UnloadWaterDrowning() => waterDrowning?.Dispose();
}
