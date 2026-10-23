// Copyright (c) Cruor. Licensed under the MIT License.

using System.Collections.Generic;
using Celeste.Core.Scheduling;
using Celeste.Graphics.Content.Tiles.Autotiling;

namespace Celeste.Mods.PandorasBox.Runtime.Terrain.Glitches
{
    internal sealed class TileGlitcherCache
    {
        private readonly ISourceEngineContext engine;
        public List<char> Foreground { get; private set; }
        public List<char> Background { get; private set; }
        public TileGlitcherCache(ISourceEngineContext engine) => this.engine = engine;

        public void Ensure()
        {
            if (Foreground != null && Background != null) return;
            var resources = (ISourceAutotilingResources)engine;
            Foreground =
                new List<char>(resources.ForegroundAutotiler.TerrainIds);
            Background =
                new List<char>(resources.BackgroundAutotiler.TerrainIds);
        }

        public void Clear()
        {
            Foreground = null;
            Background = null;
        }
    }
}
