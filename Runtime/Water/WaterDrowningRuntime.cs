// SPDX-License-Identifier: MIT

using System;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Mods.PandorasBox.Entities.Controllers.Water;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.PandorasBox.Runtime.Water;

internal sealed class WaterDrowningRuntime : IDisposable
{
    private readonly ISourceEngineContext engine;
    private readonly Hook render;

    public WaterDrowningRuntime(ISourceEngineContext engine)
    {
        this.engine = engine;
        render = new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Render)),
            (Action<Action<RoomPlayerSession, ISourceRenderContext>, RoomPlayerSession, ISourceRenderContext>)Render);
    }

    private void Render(Action<RoomPlayerSession, ISourceRenderContext> original, RoomPlayerSession player,
        ISourceRenderContext context)
    {
        if (!ReferenceEquals(player.EngineContext, engine))
        {
            original(player, context);
            return;
        }

        var registry = ((ISourceScene)player.Scene).SourceScene.Entities;
        WaterDrowningController controller = null;
        if (registry.TryGetTrackedEntities(typeof(WaterDrowningController), out var controllers))
        {
            if (controllers.Count > 0) controller = (WaterDrowningController)controllers[0];
        }

        if (controller != null && controller.WaterDuration > 0f)
        {
            float stamina = player.Data.Stamina;
            player.Data.Stamina = controller.WaterDuration + 2f > controller.WaterDrownDuration ? 0f : 110f;
            original(player, context);
            player.Data.Stamina = stamina;
        }
        else original(player, context);
    }

    public void Dispose() => render.Dispose();
}
