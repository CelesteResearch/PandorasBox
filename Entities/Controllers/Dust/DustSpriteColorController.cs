using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Areas;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.Effects.Dust;
using Celeste.Graphics.Display;
using Celeste.Mods.PandorasBox.Color;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Hazards.Spinners.Dust;

namespace Celeste.Mods.PandorasBox.Entities.Controllers.Dust
{
    [CustomEntity("pandorasBox/dustSpriteColorController")]
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.DustSpriteColorController")]
    public class DustSpriteColorController : SceneEntityState
    {
        private readonly ISourceEngineContext engine;
        private readonly DustStylePresentation style;

        public DustSpriteColorController(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            var eyeColor = ColorReader.GetColor(data.Attr("eyeColor", "Red"));
            var eyeTexture = data.Attr("eyeTexture", "danger/dustcreature/eyes");
            var colors = data.Attr("borderColor", "Green").Split(',');
            var borders = new Vector3[colors.Length];
            for (var i = 0; i < colors.Length; i++) borders[i] = ColorReader.GetColor(colors[i]).ToVector3();
            style = new DustStylePresentation
            {
                EdgeColors = borders,
                EyeColor = eyeColor,
                EyeTextures = eyeTexture
            };
        }

        public override void Added(object scene)
        {
            var level = scene as OriginalLevelSceneRoot;
            var id = level.Session.AreaID;
            var styles = ((OriginalDustStyleProvider)((ISourceDustStyleEnvironment)engine).DustStyles).Styles;
            if (!styles.ContainsKey(id) || !styles[id].Equals(style)) styles[id] = style;
            base.Added(scene);
        }

        private void RemoveStyle(object scene)
        {
            var level = scene as OriginalLevelSceneRoot;
            var areas = ((IOriginalAreaKeyEnvironment)engine).AreaKeys;
            var id = level.Session.AreaID;
            var styles = ((OriginalDustStyleProvider)((ISourceDustStyleEnvironment)engine).DustStyles).Styles;
            if (areas.GetLevelSet(areas.GetSessionArea(level.Session)) != "Celeste" && styles[id].Equals(style))
                styles.Remove(id);
        }

        public override void Removed(object scene)
        {
            RemoveStyle(scene);
            // The installed Mod calls Entity.Remove(empty components), not Entity.Removed.
            base.Remove(Array.Empty<SourceComponentState>());
        }

        public override void SceneEnd(object scene)
        {
            RemoveStyle(scene);
            base.SceneEnd(scene);
        }
    }
}
