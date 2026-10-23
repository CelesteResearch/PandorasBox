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
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.MarioClearPipeSolid")]
    [SceneTracked(true)]
    public partial class ClearPipeSolid : SourceSolidEntityState
    {
        private Vector2 startNode;
        private Vector2 endNode;
        private Vector2 nextNode;

        private bool startNodeExit;
        private bool endNodeExit;

        private float length;
        private float pipeWidth;

        private string texturePath;
        private AtlasEntry pipeTexture;

        public static Dictionary<string, Dictionary<string, Vector2>> ExitTexturesQuads =
            new Dictionary<string, Dictionary<string, Vector2>>()
            {
                {
                    "up", new Dictionary<string, Vector2>()
                    {
                        { "left", new Vector2(0, 3) },
                        { "middle", new Vector2(1, 3) },
                        { "right", new Vector2(2, 3) },
                    }
                },
                {
                    "down", new Dictionary<string, Vector2>()
                    {
                        { "left", new Vector2(0, 5) },
                        { "middle", new Vector2(1, 5) },
                        { "right", new Vector2(2, 5) },
                    }
                },
                {
                    "left", new Dictionary<string, Vector2>()
                    {
                        { "top", new Vector2(0, 0) },
                        { "middle", new Vector2(0, 1) },
                        { "bottom", new Vector2(0, 2) },
                    }
                },
                {
                    "right", new Dictionary<string, Vector2>()
                    {
                        { "top", new Vector2(2, 0) },
                        { "middle", new Vector2(2, 1) },
                        { "bottom", new Vector2(2, 2) },
                    }
                },
            };

        public static Dictionary<string, Dictionary<string, Vector2>> StraightTextureQuads =
            new Dictionary<string, Dictionary<string, Vector2>>()
            {
                {
                    "vertical", new Dictionary<string, Vector2>()
                    {
                        { "left", new Vector2(0, 4) },
                        { "middle", new Vector2(1, 4) },
                        { "right", new Vector2(2, 4) },
                    }
                },
                {
                    "horizontal", new Dictionary<string, Vector2>()
                    {
                        { "top", new Vector2(1, 0) },
                        { "middle", new Vector2(1, 1) },
                        { "bottom", new Vector2(1, 2) },
                    }
                },
            };

        public static Dictionary<string, Dictionary<string, Vector2>> CornerTextureQuads =
            new Dictionary<string, Dictionary<string, Vector2>>()
            {
                {
                    "upLeft", new Dictionary<string, Vector2>()
                    {
                        { "inner", new Vector2(3, 0) },
                        { "outer", new Vector2(5, 2) },
                        { "horizontal_middle", new Vector2(3, 1) },
                        { "horizontal_wall", new Vector2(3, 2) },
                        { "vertical_middle", new Vector2(4, 0) },
                        { "vertical_wall", new Vector2(5, 0) },
                    }
                },
                {
                    "upRight", new Dictionary<string, Vector2>()
                    {
                        { "inner", new Vector2(8, 0) },
                        { "outer", new Vector2(6, 2) },
                        { "horizontal_middle", new Vector2(8, 1) },
                        { "horizontal_wall", new Vector2(8, 2) },
                        { "vertical_middle", new Vector2(7, 0) },
                        { "vertical_wall", new Vector2(6, 0) },
                    }
                },
                {
                    "downRight", new Dictionary<string, Vector2>()
                    {
                        { "inner", new Vector2(8, 5) },
                        { "outer", new Vector2(6, 3) },
                        { "horizontal_middle", new Vector2(8, 4) },
                        { "horizontal_wall", new Vector2(8, 3) },
                        { "vertical_middle", new Vector2(7, 5) },
                        { "vertical_wall", new Vector2(6, 5) },
                    }
                },
                {
                    "downLeft", new Dictionary<string, Vector2>()
                    {
                        { "inner", new Vector2(3, 5) },
                        { "outer", new Vector2(5, 3) },
                        { "horizontal_middle", new Vector2(3, 4) },
                        { "horizontal_wall", new Vector2(3, 3) },
                        { "vertical_middle", new Vector2(4, 5) },
                        { "vertical_wall", new Vector2(5, 5) },
                    }
                },
            };

        public static AtlasEntry GetTextureQuad(AtlasEntry texture, Vector2 position)
        {
            return texture.GetSubtexture((int)position.X * 8, (int)position.Y * 8, 8, 8);
        }

        public ClearPipeSolid(ISourceEngineContext engine, ISourceSolidWorld world, Vector2 position, float width,
            float height, float length, float pipeWidth, string texturePath, int surfaceSound, Vector2 startNode,
            Vector2 endNode, Vector2 nextNode, bool startNodeExit, bool endNodeExit) : base(position, width, height,
            true,
            world)
        {
            this.startNode = startNode;
            this.endNode = endNode;
            this.nextNode = nextNode;

            this.startNodeExit = startNodeExit;
            this.endNodeExit = endNodeExit;

            this.length = length;
            this.pipeWidth = pipeWidth;

            this.texturePath = texturePath;

            pipeTexture =
                ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas[
                    $"objects/pandorasBox/clearPipe/{texturePath}/pipe"];

            Depth = -11000;
            SurfaceSoundIndex = surfaceSound >= 0 ? surfaceSound : 11; // 11 = Deactivated Space Jam,

            addPipeVisuals();

            EnableAssistModeChecks = false;
        }

        public static ClearPipeSolid FromNodes(ISourceEngineContext engine, ISourceSolidWorld world, Vector2 startNode,
            Vector2 endNode, Vector2 nextNode, bool startNodeExit, bool endNodeExit, float pipeWidth,
            string texturePath,
            int surfaceSound)
        {
            ClearPipeDirection direction = GetPipeExitDirection(endNode, startNode);

            float length = ((ISourceVectorEnvironment)engine).VectorArithmetic.Length(endNode - startNode);

            float halfPipeWidth = pipeWidth / 2f;

            Vector2 widthVector = Vector2.Zero;
            Vector2 lengthVector = Vector2.Zero;

            Vector2 position = startNode;
            float width = 8f;
            float height = 8f;

            if (startNodeExit)
            {
                length += halfPipeWidth;
            }

            if (endNodeExit)
            {
                length -= halfPipeWidth;
            }

            switch (direction)
            {
                case ClearPipeDirection.Up:
                    position = new Vector2(endNode.X - halfPipeWidth, endNode.Y - (endNodeExit ? 0 : halfPipeWidth));
                    width = pipeWidth;
                    height = length;
                    break;

                case ClearPipeDirection.Right:
                    position = new Vector2(startNode.X + (startNodeExit ? 0 : halfPipeWidth),
                        startNode.Y - halfPipeWidth);
                    width = length;
                    height = pipeWidth;
                    break;

                case ClearPipeDirection.Down:
                    position = new Vector2(startNode.X - halfPipeWidth,
                        startNode.Y + (startNodeExit ? 0 : halfPipeWidth));
                    width = pipeWidth;
                    height = length;
                    break;

                case ClearPipeDirection.Left:
                    position = new Vector2(endNode.X - (endNodeExit ? 0 : halfPipeWidth), endNode.Y - halfPipeWidth);
                    width = length;
                    height = pipeWidth;
                    break;

                default:
                    break;
            }

            // Weird offset on non multiple of 16 widths (8, 24, etc)
            if (pipeWidth / 8 % 2 == 1)
            {
                if (endNodeExit && direction == ClearPipeDirection.Right ||
                    startNodeExit && direction == ClearPipeDirection.Left)
                {
                    width -= endNodeExit && startNodeExit ? 0f : 4f;
                    length -= endNodeExit && startNodeExit ? 0f : 4f;
                }
                else if (startNodeExit && direction == ClearPipeDirection.Up ||
                         endNodeExit && direction == ClearPipeDirection.Down)
                {
                    height -= endNodeExit && startNodeExit ? 0f : 4f;
                    length -= endNodeExit && startNodeExit ? 0f : 4f;
                }
            }

            return new ClearPipeSolid(engine, world, position, width, height, length, pipeWidth, texturePath,
                surfaceSound,
                startNode, endNode, nextNode, startNodeExit, endNodeExit);
        }
    }
}
