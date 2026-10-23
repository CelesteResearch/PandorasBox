// Copyright (C) 2018-2020 Cruor
// SPDX-License-Identifier: MIT

using System;
using Celeste.Core.Rendering;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Presentation.Rendering;
using Celeste.Simulation;

namespace Celeste.Mods.PandorasBox.Runtime.DreamDashes;

public sealed partial class DreamDashControllerRuntime
{
    private void RenderPlayer(Action<RoomPlayerSession, ISourceRenderContext> original, RoomPlayerSession player,
        ISourceRenderContext context)
    {
        if (!ReferenceEquals(player.EngineContext, Engine) || player.Scene is not OriginalLevelSceneRoot level ||
            !walls.TryGetValue(player, out var wall) || !wall.HasRotation || wall.Rotation == 0f)
        {
            original(player, context);
            return;
        }

        var camera = level.Camera;
        float angle = camera.Angle;
        var position = camera.Position;
        var origin = camera.Origin;
        var spritePosition = player.Sprite.Position;
        GameplayRendererState.End(context);
        camera.Angle = wall.Rotation;
        camera.Origin = player.Position + wall.RenderOffset - camera.Position;
        camera.Position += camera.Origin;
        player.Sprite.Position += wall.RenderOffset;
        player.Hair.MoveHairBy(wall.RenderOffset);
        GameplayRendererState.Begin(context);
        try
        {
            original(player, context);
        }
        finally
        {
            GameplayRendererState.End(context);
            camera.Angle = angle;
            camera.Origin = origin;
            camera.Position = position;
            player.Sprite.Position = spritePosition;
            player.Hair.MoveHairBy(-wall.RenderOffset);
            GameplayRendererState.Begin(context);
        }
    }
}
