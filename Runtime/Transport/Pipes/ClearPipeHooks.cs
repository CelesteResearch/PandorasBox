// SPDX-License-Identifier: MIT
// Copyright (C) 2018-2020 Cruor

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Mods.PandorasBox.Runtime.Transport.Pipes;
using Celeste.Mods.PandorasBox.Components.Transport.Pipes;
using Celeste.Mods.PandorasBox.Entities.Transport.Pipes;
using static Celeste.Mods.PandorasBox.Runtime.Transport.Pipes.ClearPipeGeometry;
using Celeste.Scene.Scheduling;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Player.Collision.Responses;
using Celeste.Mods.PandorasBox.Gameplay.Transport.Pipes;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.PandorasBox.Runtime.Transport.Pipes
{
    public sealed class ClearPipeHooks : IDisposable
    {
        private readonly ClearPipeRuntime runtime;
        private readonly List<IDisposable> hooks = [];

        public ClearPipeHooks(ISourceEngineContext engine)
        {
            runtime = ClearPipeRuntime.For(engine);
            try
            {
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Added)),
                    (Action<Action<RoomPlayerSession, object>, RoomPlayerSession, object>)PlayerAdded));
                hooks.Add(new Hook(
                    typeof(PlayerHoldingController).GetMethod(nameof(PlayerHoldingController.UpdateCarry)),
                    (Action<Action<PlayerHoldingController>, PlayerHoldingController>)UpdateCarry));
                hooks.Add(new Hook(
                    typeof(PlayerCollisionResponse).GetMethod(nameof(PlayerCollisionResponse.Horizontal)),
                    (Action<Action<PlayerCollisionResponse, PixelCollision, SourcePlatformEntityState>,
                        PlayerCollisionResponse, PixelCollision, SourcePlatformEntityState>)Horizontal));
                hooks.Add(new Hook(typeof(PlayerCollisionResponse).GetMethod(nameof(PlayerCollisionResponse.Vertical)),
                    (Action<Action<PlayerCollisionResponse, PixelCollision, SourcePlatformEntityState>,
                        PlayerCollisionResponse, PixelCollision, SourcePlatformEntityState>)Vertical));
                hooks.Add(new Hook(typeof(SceneUpdateScheduler).GetMethod(nameof(SceneUpdateScheduler.AddEntity)),
                    (Action<Action<SceneUpdateScheduler, object>, SceneUpdateScheduler, object>)AddEntity));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void PlayerAdded(Action<RoomPlayerSession, object> original, RoomPlayerSession player, object scene)
        {
            if (ReferenceEquals(player.EngineContext, runtime.Engine)) ClearPipePlayerInteraction.Add(player, runtime);
            original(player, scene);
        }

        private void UpdateCarry(Action<PlayerHoldingController> original, PlayerHoldingController holding)
        {
            original(holding);
            if (holding.Body.SourceColliderOwner is RoomPlayerSession player
                && ReferenceEquals(player.EngineContext, runtime.Engine)
                && player.Get<ClearPipeInteraction>() is { CurrentClearPipe: not null } interaction)
                player.Speed = interaction.DirectionVector * interaction.TravelSpeed;
        }

        private bool Transporting(PlayerCollisionResponse response) =>
            ReferenceEquals(response.Player.EngineContext, runtime.Engine)
            && response.Player.Get<ClearPipeInteraction>()?.CurrentClearPipe != null;

        private void Horizontal(Action<PlayerCollisionResponse, PixelCollision, SourcePlatformEntityState> original,
            PlayerCollisionResponse response, PixelCollision collision, SourcePlatformEntityState hit)
        {
            if (!Transporting(response)) original(response, collision, hit);
        }

        private void Vertical(Action<PlayerCollisionResponse, PixelCollision, SourcePlatformEntityState> original,
            PlayerCollisionResponse response, PixelCollision collision, SourcePlatformEntityState hit)
        {
            if (!Transporting(response)) original(response, collision, hit);
        }

        private void AddEntity(Action<SceneUpdateScheduler, object> original, SceneUpdateScheduler list, object entity)
        {
            if (runtime.ShouldAddComponentsForList(list) && entity is SceneEntityState sourceEntity)
                runtime.AddClearPipeInteraction(sourceEntity);
            original(list, entity);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            runtime.Clear();
        }
    }
}
