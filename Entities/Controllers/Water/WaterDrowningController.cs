// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;

namespace Celeste.Mods.PandorasBox.Entities.Controllers.Water;

[CustomEntity("pandorasBox/waterDrowningController")]
[RuntimeTypeAlias("Celeste.Mod.PandorasBox.WaterDrowningController")]
[SceneTracked(true)]
public sealed class WaterDrowningController : SceneEntityState
{
    private readonly ISourceEngineContext engine;
    public float WaterDuration;
    public float WaterDrownDuration;
    public string Mode;
    public bool Flashing;

    public static WaterDrowningController Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
        Vector2 offset, OriginalLevelEntityData data) => new(data, offset, level.EngineContext);

    public WaterDrowningController(EntityData data, Vector2 offset, ISourceEngineContext engine)
        : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
    {
        this.engine = engine;
        WaterDuration = 0f;
        WaterDrownDuration = data.Float("maxDuration", 10f);
        Mode = data.Attr("mode", "Swimming");
    }

    public override void Update()
    {
        var scene = ((ISourceScene)Scene).SourceScene;
        var players = scene.Entities.GetTrackedEntities(typeof(RoomPlayerSession));
        var player = players.Count > 0 ? (RoomPlayerSession)players[0] : null;
        if (player != null)
        {
            bool inWater = Mode == "Swimming"
                ? player.World.SwimCheck()
                : Mode == "Diving" && player.World.SwimUnderwaterCheck();
            if (inWater) WaterDuration += engine.DeltaTime;
            else WaterDuration = 0f;
            if (inWater && WaterDuration >= WaterDrownDuration && !player.Dead)
                player.Die(Vector2.Zero, false, true);
        }

        float interval = 0f;
        if ((double)WaterDuration > (double)WaterDrownDuration * 0.7) interval = 0.6f;
        else if ((double)WaterDuration > (double)WaterDrownDuration * 0.5) interval = 1f;
        else if ((double)WaterDuration > (double)WaterDrownDuration * 0.3) interval = 1.4f;
        if (interval > 0f && scene.Clock.OnInterval(interval, engine) && player != null && !player.Dead)
        {
            ((IOriginalGameInputProcessEnvironment)engine).InputSource.Rumble(RumbleStrength.Climb, RumbleLength.Short);
            Flashing = !Flashing;
        }

        base.Update();
    }
}
