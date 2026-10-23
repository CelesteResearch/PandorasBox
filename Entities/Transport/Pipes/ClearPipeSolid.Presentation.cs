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
    public partial class ClearPipeSolid
    {
        private static string getCornerType(ClearPipeDirection direction, ClearPipeDirection nextDirection)
        {
            if (direction == ClearPipeDirection.Right && nextDirection == ClearPipeDirection.Up ||
                direction == ClearPipeDirection.Down && nextDirection == ClearPipeDirection.Left)
            {
                return "upLeft";
            }
            else if (direction == ClearPipeDirection.Down && nextDirection == ClearPipeDirection.Right ||
                     direction == ClearPipeDirection.Left && nextDirection == ClearPipeDirection.Up)
            {
                return "upRight";
            }
            else if (direction == ClearPipeDirection.Left && nextDirection == ClearPipeDirection.Down ||
                     direction == ClearPipeDirection.Up && nextDirection == ClearPipeDirection.Right)
            {
                return "downRight";
            }
            else if (direction == ClearPipeDirection.Up && nextDirection == ClearPipeDirection.Left ||
                     direction == ClearPipeDirection.Right && nextDirection == ClearPipeDirection.Down)
            {
                return "downLeft";
            }
            else
            {
                return "unknown";
            }
        }

        private void addPipeCornerVisuals(ClearPipeDirection direction, ClearPipeDirection nextDirection)
        {
            if (length < pipeWidth)
            {
                return;
            }

            string cornerType = getCornerType(direction, nextDirection);

            if (cornerType != "upLeft" && cornerType != "upRight" && cornerType != "downRight" &&
                cornerType != "downLeft")
            {
                return;
            }

            AtlasEntry nonWallTexture =
                GetTextureQuad(pipeTexture, StraightTextureQuads["horizontal"]["middle"]); // TODO - Better texture?
            AtlasEntry innerCornerTexture = GetTextureQuad(pipeTexture, CornerTextureQuads[cornerType]["inner"]);
            AtlasEntry outerCornerTexture = GetTextureQuad(pipeTexture, CornerTextureQuads[cornerType]["outer"]);
            AtlasEntry verticalWallTexture =
                GetTextureQuad(pipeTexture, CornerTextureQuads[cornerType]["vertical_wall"]);
            AtlasEntry horizontalWallTexture =
                GetTextureQuad(pipeTexture, CornerTextureQuads[cornerType]["horizontal_wall"]);

            float verticalWallX = -1;
            float horizontalWallY = -1;

            float offsetX = 0;
            float offsetY = 0;

            if (cornerType == "upLeft")
            {
                verticalWallX = pipeWidth - 8;
                horizontalWallY = pipeWidth - 8;
            }
            else if (cornerType == "upRight")
            {
                verticalWallX = 0;
                horizontalWallY = pipeWidth - 8;
            }
            else if (cornerType == "downRight")
            {
                verticalWallX = 0;
                horizontalWallY = 0;
            }
            else if (cornerType == "downLeft")
            {
                verticalWallX = pipeWidth - 8;
                horizontalWallY = 0;
            }

            if (direction == ClearPipeDirection.Right)
            {
                offsetX = length - pipeWidth;
            }
            else if (direction == ClearPipeDirection.Down)
            {
                offsetY = length - pipeWidth;
            }

            for (int x = 0; x < pipeWidth; x += 8)
            {
                for (int y = 0; y < pipeWidth; y += 8)
                {
                    SpriteImage image;

                    bool onVertical = x == verticalWallX;
                    bool onHorizontal = y == horizontalWallY;
                    bool innerCorner = x == Math.Abs(verticalWallX - pipeWidth + 8) &&
                                       y == Math.Abs(horizontalWallY - pipeWidth + 8);

                    if (innerCorner)
                    {
                        image = new SpriteImage(innerCornerTexture);
                    }
                    else if (onVertical && onHorizontal)
                    {
                        image = new SpriteImage(outerCornerTexture);
                    }
                    else if (onVertical)
                    {
                        image = new SpriteImage(verticalWallTexture);
                    }
                    else if (onHorizontal)
                    {
                        image = new SpriteImage(horizontalWallTexture);
                    }
                    else
                    {
                        image = new SpriteImage(nonWallTexture);
                    }

                    image.X = x + offsetX;
                    image.Y = y + offsetY;

                    Add(image);
                }
            }
        }

        private void addPipeVisuals()
        {
            ClearPipeDirection direction = GetPipeExitDirection(endNode, startNode);
            ClearPipeDirection nextDirection = GetPipeExitDirection(nextNode, endNode);

            bool horizontalPipe = direction == ClearPipeDirection.Right || direction == ClearPipeDirection.Left;
            bool verticalPipe = direction == ClearPipeDirection.Down || direction == ClearPipeDirection.Up;

            float straightLength = !endNodeExit ? length - pipeWidth : length;
            float offset = !endNodeExit && (direction == ClearPipeDirection.Up || direction == ClearPipeDirection.Left)
                ? pipeWidth
                : 0;

            if (horizontalPipe)
            {
                for (int x = 0; x < straightLength; x += 8)
                {
                    string columnType = "section";

                    SpriteImage top;
                    SpriteImage bottom;
                    AtlasEntry middleTexture;

                    if (direction == ClearPipeDirection.Left && x == straightLength - 8 && startNodeExit ||
                        direction == ClearPipeDirection.Right && x == straightLength - 8 && endNodeExit)
                    {
                        columnType = "rightExit";
                    }
                    else if (direction == ClearPipeDirection.Left && x == 0 && endNodeExit ||
                             direction == ClearPipeDirection.Right && x == 0 && startNodeExit)
                    {
                        columnType = "leftExit";
                    }

                    if (columnType == "rightExit")
                    {
                        top = new SpriteImage(GetTextureQuad(pipeTexture, ExitTexturesQuads["right"]["top"]));
                        bottom = new SpriteImage(GetTextureQuad(pipeTexture, ExitTexturesQuads["right"]["bottom"]));

                        middleTexture = GetTextureQuad(pipeTexture, ExitTexturesQuads["right"]["middle"]);
                    }
                    else if (columnType == "leftExit")
                    {
                        top = new SpriteImage(GetTextureQuad(pipeTexture, ExitTexturesQuads["left"]["top"]));
                        bottom = new SpriteImage(GetTextureQuad(pipeTexture, ExitTexturesQuads["left"]["bottom"]));

                        middleTexture = GetTextureQuad(pipeTexture, ExitTexturesQuads["left"]["middle"]);
                    }
                    else
                    {
                        top = new SpriteImage(GetTextureQuad(pipeTexture, StraightTextureQuads["horizontal"]["top"]));
                        bottom = new SpriteImage(GetTextureQuad(pipeTexture,
                            StraightTextureQuads["horizontal"]["bottom"]));

                        middleTexture = GetTextureQuad(pipeTexture, StraightTextureQuads["horizontal"]["middle"]);
                    }

                    top.X = x + offset;
                    top.Y = 0;

                    bottom.X = x + offset;
                    bottom.Y = pipeWidth - 8;

                    Add(top);
                    Add(bottom);

                    for (int y = 8; y < pipeWidth - 8; y += 8)
                    {
                        SpriteImage middle = new SpriteImage(middleTexture);

                        middle.X = x + offset;
                        middle.Y = y;

                        Add(middle);
                    }
                }
            }
            else if (verticalPipe)
            {
                for (int y = 0; y < straightLength; y += 8)
                {
                    string rowType = "section";

                    SpriteImage left;
                    SpriteImage right;
                    AtlasEntry middleTexture;

                    if (direction == ClearPipeDirection.Up && y == straightLength - 8 && startNodeExit ||
                        direction == ClearPipeDirection.Down && y == straightLength - 8 && endNodeExit)
                    {
                        rowType = "downExit";
                    }
                    else if (direction == ClearPipeDirection.Up && y == 0 && endNodeExit ||
                             direction == ClearPipeDirection.Down && y == 0 && startNodeExit)
                    {
                        rowType = "upExit";
                    }

                    if (rowType == "upExit")
                    {
                        left = new SpriteImage(GetTextureQuad(pipeTexture, ExitTexturesQuads["up"]["left"]));
                        right = new SpriteImage(GetTextureQuad(pipeTexture, ExitTexturesQuads["up"]["right"]));

                        middleTexture = GetTextureQuad(pipeTexture, ExitTexturesQuads["up"]["middle"]);
                    }
                    else if (rowType == "downExit")
                    {
                        left = new SpriteImage(GetTextureQuad(pipeTexture, ExitTexturesQuads["down"]["left"]));
                        right = new SpriteImage(GetTextureQuad(pipeTexture, ExitTexturesQuads["down"]["right"]));

                        middleTexture = GetTextureQuad(pipeTexture, ExitTexturesQuads["down"]["middle"]);
                    }
                    else
                    {
                        left = new SpriteImage(GetTextureQuad(pipeTexture, StraightTextureQuads["vertical"]["left"]));
                        right = new SpriteImage(GetTextureQuad(pipeTexture, StraightTextureQuads["vertical"]["right"]));

                        middleTexture = GetTextureQuad(pipeTexture, StraightTextureQuads["vertical"]["middle"]);
                    }

                    left.X = 0;
                    left.Y = y + offset;

                    right.X = pipeWidth - 8;
                    right.Y = y + offset;

                    Add(left);
                    Add(right);

                    for (int x = 8; x < pipeWidth - 8; x += 8)
                    {
                        SpriteImage middle = new SpriteImage(middleTexture);

                        middle.X = x;
                        middle.Y = y + offset;

                        Add(middle);
                    }
                }
            }

            if (!endNodeExit)
            {
                addPipeCornerVisuals(direction, nextDirection);
            }
        }
    }
}
