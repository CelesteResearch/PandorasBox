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
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.MarioClearPipe")]
    [SceneTracked]
    [CustomEntity("pandorasBox/clearPipe")]
    public partial class ClearPipe : SceneEntityState
    {
        private readonly ClearPipeRuntime runtime;
        private string interactionRegistryKey;
        private ISourceEngineContext Engine => runtime.Engine;

        // Pixels smaller than the pipe width
        private static int pipeWidthColliderValue = 4;

        // Transport speed Per second
        public float TransportSpeed { get; protected set; } = 175f;
        private float transportSpeedEnterMultiplier = 0.75f;

        private int pipeWidth = 32;
        private int pipeColliderWidth = 28;
        private int pipeColliderDepth = 4;

        private string texturePath;
        private int surfaceSound;

        public bool HasPipeSolids { get; protected set; }

        private Vector2[] nodes;

        private ClearPipeDirection startDirection;
        private ClearPipeDirection endDirection;
        private Vector2 startDirectionVector;
        private Vector2 endDirectionVector;

        private HitboxColliderGeometry startCollider;
        private HitboxColliderGeometry endCollider;

        private bool legacyPipeMovementYields;

        private List<ClearPipeSolid> pipeSolids;

        public ClearPipe(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            runtime = ClearPipeRuntime.For(engine);
            Depth = -5;

            nodes = data.NodesWithPosition(offset);

            removeRedundantNodes();

            texturePath = data.Attr("texture", "glass");
            surfaceSound = data.Int("surfaceSound", -1);

            HasPipeSolids = data.Bool("hasPipeSolids", true);
            TransportSpeed = data.Float("transportSpeed", 175f);

            pipeWidth = data.Int("pipeWidth", 32);
            pipeColliderWidth = pipeWidth - pipeWidthColliderValue;

            startDirection = GetPipeExitDirection(nodes[0], nodes[1]);
            endDirection = GetPipeExitDirection(nodes[nodes.Length - 1], nodes[nodes.Length - 2]);

            startDirectionVector = runtime.GetPipeExitDirectionVector(nodes[0], nodes[1]);
            endDirectionVector = runtime.GetPipeExitDirectionVector(nodes[nodes.Length - 1], nodes[nodes.Length - 2]);

            startCollider = getPipeCollider(Vector2.Zero, startDirection, pipeWidth, pipeColliderWidth,
                pipeColliderDepth);
            endCollider = getPipeCollider(
                new Vector2(nodes.Last().X - nodes.First().X, nodes.Last().Y - nodes.First().Y),
                endDirection, pipeWidth, pipeColliderWidth, pipeColliderDepth);

            legacyPipeMovementYields = data.Bool("useLegacyMovement", true);

            Collider = new ColliderListGeometry(startCollider, endCollider);

            pipeSolids = new List<ClearPipeSolid>();
        }

        // Node is considered redundant if it does not change the direction
        private void removeRedundantNodes()
        {
            List<Vector2> newNodes = new List<Vector2>();

            Vector2 previousNode = Vector2.Zero;
            Vector2 previousDirection = Vector2.Zero;
            bool hasPreviousNode = false;

            foreach (Vector2 node in nodes)
            {
                if (hasPreviousNode)
                {
                    Vector2 direction = runtime.GetPipeExitDirectionVector(previousNode, node);

                    if (Math.Abs(direction.X - previousDirection.X) > 0.0005 ||
                        Math.Abs(direction.Y - previousDirection.Y) > 0.0005)
                    {
                        newNodes.Add(previousNode);
                    }

                    previousDirection = direction;
                }

                hasPreviousNode = true;
                previousNode = node;
            }

            newNodes.Add(nodes.Last());

            nodes = newNodes.ToArray();
        }

        private HitboxColliderGeometry getPipeCollider(Vector2 position, ClearPipeDirection exitDireciton,
            int pipeWidth,
            int pipeColliderWidth, int colliderDepth)
        {
            // Weird offset on Right/Down facing pipes with non multiples of 16 pipe width (8, 24, etc)
            switch (exitDireciton)
            {
                case ClearPipeDirection.Up:
                    return new HitboxColliderGeometry(pipeColliderWidth, colliderDepth,
                        position.X - pipeColliderWidth / 2,
                        position.Y - colliderDepth);

                case ClearPipeDirection.Right:
                    if (pipeWidth / 8 % 2 == 1 && nodes.Length > 2)
                    {
                        return new HitboxColliderGeometry(colliderDepth, pipeColliderWidth, position.X - 4,
                            position.Y - pipeColliderWidth / 2);
                    }
                    else
                    {
                        return new HitboxColliderGeometry(colliderDepth, pipeColliderWidth, position.X,
                            position.Y - pipeColliderWidth / 2);
                    }

                case ClearPipeDirection.Down:
                    if (pipeWidth / 8 % 2 == 1 && nodes.Length > 2)
                    {
                        return new HitboxColliderGeometry(pipeColliderWidth, colliderDepth,
                            position.X - pipeColliderWidth / 2, position.Y - 4);
                    }
                    else
                    {
                        return new HitboxColliderGeometry(pipeColliderWidth, colliderDepth,
                            position.X - pipeColliderWidth / 2, position.Y);
                    }

                case ClearPipeDirection.Left:
                    return new HitboxColliderGeometry(colliderDepth, pipeColliderWidth, position.X - colliderDepth,
                        position.Y - pipeColliderWidth / 2);

                default:
                    return new HitboxColliderGeometry(pipeWidth, pipeWidth, position.X - pipeWidth / 2,
                        position.Y - pipeWidth / 2);
            }
        }

        private void addPipeSolids(int pipeWidth)
        {
            Vector2 previousNode = Vector2.Zero;
            bool hasPreviousNode = false;

            int i = 0;

            float halfPipeWidth = pipeWidth / 2f;

            foreach (Vector2 node in nodes)
            {
                if (hasPreviousNode)
                {
                    bool startNodeExit = i == 1;
                    bool endNodeExit = i == nodes.Count() - 1;

                    Vector2 nextNode = nodes.ElementAtOrDefault(i + 1);

                    ClearPipeSolid pipeSolid = ClearPipeSolid.FromNodes(Engine,
                        ((Celeste.Game.Execution.Levels.OriginalLevelSceneRoot)Scene).SolidWorldFactory.Create(
                            previousNode,
                            true), previousNode, node, nextNode, startNodeExit, endNodeExit, pipeWidth, texturePath,
                        surfaceSound);

                    pipeSolids.Add(pipeSolid);
                    ((ISourceScene)Scene).SourceScene.QueueSourceEntity(pipeSolid);
                }

                hasPreviousNode = true;
                previousNode = node;
                i++;
            }
        }


        // Visually update exiting steps
        // Correct for exit overshooting


        // TODO - Cleanup and make more generic


        public override void Added(object scene)
        {
            base.Added(scene);
            interactionRegistryKey = ((ISourceScene)scene).SourceScene.Catalog.Find(typeof(ClearPipeInteraction)).Name;
        }

        public override void Update()
        {
            var components = ((ISourceScene)Scene).SourceScene.Components.GetTrackedComponents(interactionRegistryKey);

            foreach (ClearPipeInteraction component in components)
            {
                SceneEntityState entity = (SceneEntityState)component.Entity;

                if (entity.Collider == null)
                {
                    continue;
                }

                if (entity.Collider.Collide(startCollider))
                {
                    if (runtime.CanTransportEntity(entity, startDirection))
                    {
                        Add(new SimulationCoroutine(Engine, pipeMovement(entity, true)));
                    }
                }
                else if (entity.Collider.Collide(endCollider))
                {
                    if (runtime.CanTransportEntity(entity, endDirection))
                    {
                        Add(new SimulationCoroutine(Engine, pipeMovement(entity, false)));
                    }
                }
            }

            base.Update();
        }

        public override void Awake(object scene)
        {
            if (HasPipeSolids)
            {
                addPipeSolids(pipeWidth);
            }

            foreach (SceneEntityState entity in ((ISourceScene)Scene).SourceScene.Entities.Entities)
            {
                runtime.AddClearPipeInteraction(entity);
            }

            runtime.AllowComponentsForList(((ISourceScene)Scene).SourceScene);

            base.Awake(scene);
        }
    }
}
