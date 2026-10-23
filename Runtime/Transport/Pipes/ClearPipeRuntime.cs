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
using System.Runtime.CompilerServices;
using Celeste.Scene.Scheduling;
using Celeste.Mods.PandorasBox.Gameplay.Transport.Pipes;

namespace Celeste.Mods.PandorasBox.Runtime.Transport.Pipes
{
    public sealed class ClearPipeRuntime
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, ClearPipeRuntime> Owners = new();
        public static ClearPipeRuntime For(ISourceEngineContext engine) => Owners.GetValue(engine, static e => new(e));
        public ISourceEngineContext Engine { get; }
        public HashSet<SceneEntityState> CurrentlyTransportedEntities { get; } = [];
        private ConditionalWeakTable<SceneEntityState, InteractionCache> interactions = new();
        private ConditionalWeakTable<SceneUpdateScheduler, object> allowedLists = new();

        private sealed class InteractionCache
        {
            public ClearPipeInteraction Value;
        }

        private ClearPipeRuntime(ISourceEngineContext engine) => Engine = engine;

        public Vector2 GetPipeExitDirectionVector(Vector2 exit, Vector2 previous) =>
            ((ISourceVectorEnvironment)Engine).VectorArithmetic.SafeNormalize(exit - previous);

        public T CollideFirst<T>(SceneEntityState entity) where T : SceneEntityState
        {
            foreach (var candidate in ((ISourceScene)entity.Scene).SourceScene.Entities.GetTrackedEntities(typeof(T)))
                if (SourceEntityCollision.Check(entity, (ISourceEntityCollider)candidate))
                    return (T)candidate;
            return null;
        }

        public bool CanExitPipe(SceneEntityState entity, Vector2 movementDirection, float transportSpeed,
            int checks = 16)
        {
            if (entity == null) return false;
            Vector2 originalPosition = entity.Position;
            for (int i = 0; i < checks; i++)
            {
                entity.Position += movementDirection * transportSpeed * (1f / 60f);
                if (entity.Scene != null && CollideFirst<ClearPipeSolid>(entity) == null)
                {
                    bool result = CollideFirst<SourceSolidEntityState>(entity) == null;
                    entity.Position = originalPosition;
                    return result;
                }
            }

            entity.Position = originalPosition;
            return false;
        }

        public bool CanTransportEntity(SceneEntityState entity, ClearPipeDirection direction) =>
            entity != null && !CurrentlyTransportedEntities.Contains(entity)
                           && GetClearPipeInteraction(entity)?.CanEnterPipe?.Invoke(entity, direction) == true;

        public Vector2 Lerp(Vector2 first, Vector2 second, float amount) => Vector2Math.Lerp(first, second, amount,
            ((ISourceScalarEnvironment)Engine).ScalarArithmetic);

        public float Length(Vector2 v) => ((ISourceVectorEnvironment)Engine).VectorArithmetic.Length(v);
        public float LengthSquared(Vector2 v) => ((ISourceVectorEnvironment)Engine).VectorArithmetic.LengthSquared(v);
        public void RememberInteraction(SceneEntityState entity) => UpdateInteractionCache(entity);

        private ClearPipeInteraction UpdateInteractionCache(SceneEntityState entity)
        {
            var cache = interactions.GetValue(entity, static _ => new InteractionCache());
            return cache.Value = entity.Get<ClearPipeInteraction>();
        }

        public ClearPipeInteraction GetClearPipeInteraction(SceneEntityState entity)
        {
            if (entity == null || entity.Scene == null) return null;
            return interactions.TryGetValue(entity, out var cached) ? cached.Value : UpdateInteractionCache(entity);
        }

        public bool HasClearPipeInteraction(SceneEntityState entity) => GetClearPipeInteraction(entity) != null;

        public bool AddClearPipeInteraction(SceneEntityState entity)
        {
            if (HasClearPipeInteraction(entity)) return false;
            if (ClearPipeHoldableInteraction.Add(entity, this) || ClearPipePufferInteraction.Add(entity, this))
            {
                UpdateInteractionCache(entity);
                return true;
            }

            return false;
        }

        public void AllowComponentsForList(SceneUpdateScheduler list) =>
            allowedLists.GetValue(list, static _ => new object());

        public bool ShouldAddComponentsForList(SceneUpdateScheduler list) => allowedLists.TryGetValue(list, out _);

        public void Clear()
        {
            CurrentlyTransportedEntities.Clear();
            interactions = new();
            allowedLists = new();
        }
    }
}
