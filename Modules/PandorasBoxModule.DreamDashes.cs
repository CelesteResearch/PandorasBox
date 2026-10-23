// Copyright (C) 2018-2020 Cruor
// SPDX-License-Identifier: MIT

using Celeste.Mods.PandorasBox.Runtime.DreamDashes;

namespace Celeste.Mods.PandorasBox.Modules;

public sealed partial class PandorasBoxModule
{
    private void LoadDreamDashes() => _ = DreamDashControllerRuntime.For(SourceEngine);
    private void UnloadDreamDashes() => DreamDashControllerRuntime.Release(SourceEngine);
}
