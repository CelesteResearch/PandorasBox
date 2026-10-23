using System.Numerics;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.PandorasBox.Runtime.Players.Clones;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.PandorasBox.Entities.Players.Clones;

[CustomEntity("pandorasBox/playerClone")]
[RuntimeTypeAlias("Celeste.Mod.PandorasBox.CloneSpawner")]
[SceneTracked(false)]
public sealed class CloneSpawner : SourceActorEntityState
{
    public RoomPlayerSession Clone;
    private readonly string flag;
    private readonly int id;
    private bool active;
    private readonly string visualMode;

    public static CloneSpawner Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
        Vector2 offset, OriginalLevelEntityData data) => new(data, offset, level.EngineContext);

    public CloneSpawner(EntityData data, Vector2 offset, ISourceEngineContext engine)
        : base(new PixelActor(null, data.Position + offset), engine,
            ((ISourcePlatformTargetMotionEnvironment)engine).TargetMotionPolicy)
    {
        flag = data.Attr("flag", "");
        id = data.ID;
        visualMode = data.Attr("mode", "Inventory");
    }

    private bool GetFlag() => Scene is OriginalLevelSceneRoot level &&
                              level.Session.GetFlag(string.IsNullOrEmpty(flag) ? "pb_clone_spawner_" + id : flag);

    private void HandleClone()
    {
        var level = Scene as OriginalLevelSceneRoot;
        if (active)
        {
            if (Clone == null)
            {
                Clone = PlayerCloneHelper.CreatePlayer(level, Position, visualMode);
                level.SourceScene.QueueSourceEntity(Clone);
            }
        }
        else if (Clone != null)
        {
            Clone.RemoveSelf();
            Clone = null;
        }
    }

    public override void Added(object scene)
    {
        active = GetFlag();
        base.Added(scene);
    }

    public override void Removed(object scene)
    {
        active = false;
        HandleClone();
        base.Removed(scene);
    }

    public override void Update()
    {
        active = GetFlag();
        HandleClone();
        base.Update();
    }
}
