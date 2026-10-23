// Copyright (C) 2018-2020 Cruor
// SPDX-License-Identifier: MIT

using System.Numerics;

namespace Celeste.Mods.PandorasBox.Runtime.DreamDashes;

internal sealed class DreamDashWallState
{
    internal bool HasRotation;
    internal float Rotation;
    internal Vector2 RenderOffset, Speed;
}
