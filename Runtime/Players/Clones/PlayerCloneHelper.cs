using System.Collections.Generic;
using System.Numerics;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Presentation.Sprites;

namespace Celeste.Mods.PandorasBox.Runtime.Players.Clones;

public static class PlayerCloneHelper
{
    public static readonly Dictionary<string, PlayerSpriteMode> Modes = new()
    {
        { "Backpack", PlayerSpriteMode.Madeline },
        { "NoBackpack", PlayerSpriteMode.MadelineNoBackpack },
        { "MadelineAsBadeline", PlayerSpriteMode.MadelineAsBadeline }
    };

    public static RoomPlayerSession CreatePlayer(OriginalLevelSceneRoot level, EntityData data, Vector2 offset) =>
        CreatePlayer(level, data.Position + offset, data.Attr("mode", ""));

    public static RoomPlayerSession CreatePlayer(OriginalLevelSceneRoot level, Vector2 position,
        string mode = "Inventory")
    {
        int count = 0;
        int left = 0;
        foreach (var entity in level.SourceScene.Entities.Entities)
            if (entity is RoomPlayerSession player)
            {
                count++;
                if (player.Facing == -1) left++;
            }

        var spriteMode = Modes.TryGetValue(mode, out var selected)
            ? selected
            : level.Session.Inventory.Backpack
                ? PlayerSpriteMode.Madeline
                : PlayerSpriteMode.MadelineNoBackpack;
        var clone = level.CreatePlayer(position, spriteMode);
        if (count > 0) clone.Data.Facing = count - left >= left ? 1 : -1;
        return clone;
    }
}
