// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Xml;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Registry;
using Celeste.Formats.Graphics.Sprites;
using Celeste.Graphics.Content;

namespace Celeste.Mods.PandorasBox.Content.Switches
{
    internal sealed class FlagToggleSwitchResources
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, FlagToggleSwitchResources> owners = new();
        public SpriteBank SpriteBank;

        public static FlagToggleSwitchResources For(ISourceEngineContext engine) =>
            owners.GetValue(engine, _ => new FlagToggleSwitchResources());

        public static void LoadContent(ISourceEngineContext engine)
        {
            var xml = new XmlDocument();
            using (var input = ((IEverestContentRegistryEnvironment)engine).SourceContentRegistry
                   .Get("Graphics/PandorasBox/flagToggleSwitch").Stream) xml.Load(input);
            For(engine).SpriteBank =
                new SpriteBank(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, xml);
        }
    }
}
