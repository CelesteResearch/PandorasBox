// SPDX-License-Identifier: MIT

using Celeste.Mods.PandorasBox.Content.Switches;

namespace Celeste.Mods.PandorasBox.Modules
{
    public sealed partial class PandorasBoxModule
    {
        public override void LoadContent(bool firstLoad)
        {
            base.LoadContent(firstLoad);
            FlagToggleSwitchResources.LoadContent(SourceEngine);
        }
    }
}
