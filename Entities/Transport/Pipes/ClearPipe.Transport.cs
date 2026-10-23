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

namespace Celeste.Mods.PandorasBox.Entities.Transport.Pipes
{
    public partial class ClearPipe
    {
        private IEnumerator moveBetweenNodes(SceneEntityState entity, ClearPipeInteraction interaction, Vector2 from,
            Vector2 to, float? travelSpeed = null, bool firstEntry = false, bool exiting = false)
        {
            // Directions should not be affected by offset
            interaction.DirectionVector = runtime.GetPipeExitDirectionVector(to, from);
            interaction.Direction = GetPipeExitDirection(to, from);

            interaction.From = from;
            interaction.To = to;

            if (!exiting)
            {
                interaction.To += interaction.PipeRenderOffset;
            }

            if (!firstEntry)
            {
                interaction.From += interaction.PipeRenderOffset;
            }

            // Travel distance should be the actual distance the entity has to travel
            interaction.Distance = runtime.Length(from - to);
            interaction.TravelSpeed = travelSpeed != null ? travelSpeed.Value : TransportSpeed;

            while (entity != null && interaction.Moved <= interaction.Distance && interaction.Distance != 0f &&
                   !interaction.ExitEarly)
            {
                interaction?.OnPipeUpdate(entity, interaction);

                float lerpValue = interaction.Moved / interaction.Distance;

                entity.Position = runtime.Lerp(interaction.From, interaction.To, lerpValue);
                interaction.Moved += ((ISourceScalarEnvironment)Engine).ScalarArithmetic.Multiply(
                    interaction.TravelSpeed, Engine.DeltaTime, 1f);

                // Only yield if we are still in the same segment
                if (interaction.Moved <= interaction.Distance || legacyPipeMovementYields)
                {
                    yield return null;
                }
            }

            // Prevent oddities with overshooting
            entity.Position = interaction.To;

            interaction.Moved -= interaction.Distance;
        }

        private IEnumerator exitPipeMovement(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            if (entity == null || entity.Scene == null)
            {
                yield break;
            }

            Vector2 previousPosition = entity.Position;
            Vector2 currentPosition = entity.Position;

            bool colliding = runtime.CollideFirst<ClearPipeSolid>(entity) != null;

            while (colliding && !interaction.ExitEarly)
            {
                interaction?.OnPipeUpdate(entity, interaction);

                entity.Position += interaction.DirectionVector * TransportSpeed * Engine.DeltaTime;

                previousPosition = currentPosition;
                currentPosition = entity.Position;

                colliding = runtime.CollideFirst<ClearPipeSolid>(entity) != null;

                if (colliding)
                {
                    yield return null;
                }
            }

            if (interaction.ExitEarly)
            {
                yield break;
            }

            // Correct for overshooting the exit, attempt to place entity as close as possible to the pipe

            Vector2 lowerValue = previousPosition;
            Vector2 upperValue = currentPosition;
            Vector2 lastUnblocked = entity.Position;

            while (runtime.LengthSquared(lowerValue - upperValue) > 0.5f)
            {
                entity.Position = runtime.Lerp(lowerValue, upperValue, 0.5f);

                if (runtime.CollideFirst<ClearPipeSolid>(entity) != null)
                {
                    lowerValue = entity.Position;
                }
                else
                {
                    upperValue = entity.Position;
                    lastUnblocked = entity.Position;
                }
            }

            entity.Position = lastUnblocked;
        }

        private void ejectFromPipe(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            // Fix float positions, causes weird collision bugs for entities
            entity.Position = new Vector2((int)Math.Round(entity.Position.X), (int)Math.Round(entity.Position.Y));

            interaction.OnPipeExit?.Invoke(entity, interaction);
            interaction.CurrentClearPipe = null;
            interaction.Moved = 0f;

            runtime.CurrentlyTransportedEntities.Remove(entity);
        }

        private IEnumerator pipeMovement(SceneEntityState entity, bool fromStart, bool canBounceBack = true,
            Vector2? forcedStartPosition = null)
        {
            ClearPipeInteraction interaction = runtime.GetClearPipeInteraction(entity);

            int startIndex = fromStart ? 0 : nodes.Length - 1;
            int lastIndex = fromStart ? nodes.Length - 1 : 0;
            int direction = fromStart ? 1 : -1;

            ClearPipeDirection transportStartDirection = fromStart ? startDirection : endDirection;
            ClearPipeDirection transportEndDirection = fromStart ? endDirection : startDirection;

            if (forcedStartPosition != null || runtime.CanTransportEntity(entity, transportStartDirection))
            {
                runtime.CurrentlyTransportedEntities.Add(entity);
                interaction.CurrentClearPipe = this;
                interaction.ExitEarly = false;
                interaction?.OnPipeEnter?.Invoke(entity, interaction);

                // Check if we are entering the pipe or bouncing back from a blocked exit
                if (forcedStartPosition != null)
                {
                    entity.Position = forcedStartPosition.Value;
                }
                else
                {
                    bool firstEntry = forcedStartPosition == null;

                    // Gracefully attempt to move to the first node
                    IEnumerator enumerator = moveBetweenNodes(entity, interaction, entity.Position, nodes[startIndex],
                        TransportSpeed * transportSpeedEnterMultiplier, firstEntry);

                    if (legacyPipeMovementYields)
                    {
                        yield return enumerator;
                    }
                    else
                    {
                        while (enumerator.MoveNext())
                        {
                            yield return null;
                        }
                    }
                }

                // Follow the nodes
                for (int i = startIndex; i != lastIndex && !interaction.ExitEarly; i += direction)
                {
                    IEnumerator enumerator = moveBetweenNodes(entity, interaction, nodes[i], nodes[i + direction]);

                    if (legacyPipeMovementYields)
                    {
                        yield return enumerator;
                    }
                    else
                    {
                        while (enumerator.MoveNext())
                        {
                            yield return null;
                        }
                    }
                }

                if (interaction.ExitEarly)
                {
                    ejectFromPipe(entity, interaction);

                    yield break;
                }

                // Check if we can exit the pipe
                if (runtime.CanExitPipe(entity, interaction.DirectionVector, TransportSpeed))
                {
                    IEnumerator enumerator = exitPipeMovement(entity, interaction);

                    if (legacyPipeMovementYields)
                    {
                        yield return enumerator;
                    }
                    else
                    {
                        while (enumerator.MoveNext())
                        {
                            yield return null;
                        }
                    }
                }

                // Send back if it gets stuck in a solid
                if (entity != null && entity.Scene != null &&
                    runtime.CollideFirst<SourceSolidEntityState>(entity) != null && !interaction.ExitEarly)
                {
                    if (canBounceBack)
                    {
                        entity.Position = nodes[lastIndex] + interaction.PipeRenderOffset;

                        IEnumerator enumerator = pipeMovement(entity, !fromStart, false, entity.Position);

                        if (legacyPipeMovementYields)
                        {
                            yield return enumerator;
                        }
                        else
                        {
                            while (enumerator.MoveNext())
                            {
                                yield return null;
                            }
                        }
                    }
                    else
                    {
                        // TODO: This should try to kill theo etc?
                        ejectFromPipe(entity, interaction);
                    }
                }
                else
                {
                    ejectFromPipe(entity, interaction);
                }
            }
        }
    }
}
