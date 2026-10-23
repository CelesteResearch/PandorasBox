using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Mathematics.LinearAlgebra;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Environment.Water;
using VertexPositionColor = Celeste.Graphics.Rendering.Vertices.SourceVertexPositionColor;

namespace Celeste.Mods.PandorasBox.Entities.Water
{
    internal sealed class ColoredWaterSurface : OriginalWaterState.Surface
    {
        private readonly ColoredWater owner;
        private readonly ISourceEngineContext engine;
        private readonly WaterAppearance appearance;
        private readonly float[] surfaceHeightCache;
        private const int horizontalWaterHeightVisiblityBuffer = 24;
        private const int verticalWaterHeightVisiblityBuffer = 24;
        private const int rayMaxLength = 128;

        public ColoredWaterSurface(ColoredWater owner, ISourceEngineContext engine, Vector2 position,
            Vector2 outwards, float width, float bodyHeight, WaterAppearance appearance)
            : base(engine, position, outwards, width, bodyHeight, appearance)
        {
            this.owner = owner;
            this.engine = engine;
            this.appearance = appearance;
            surfaceHeightCache = new float[(int)Math.Ceiling(width / 4f) + 1];
        }

        private Vector2 GetVisibleSurfaceRange(OriginalWaterState.Surface surface)
        {
            int num = 0;
            int num2 = surface.Width;
            float x = surface.Position.X;
            float y = surface.Position.Y;
            float num3 = surface.Width / 2;
            if (surface.Outwards.Y == -1f)
            {
                x -= num3;
                num = (int)ScalarMath.Clamp(owner.Camera.Left - (float)horizontalWaterHeightVisiblityBuffer - x, 0f,
                    surface.Width);
                num2 = (int)ScalarMath.Clamp(owner.Camera.Right + (float)horizontalWaterHeightVisiblityBuffer - x, 0f,
                    surface.Width);
            }
            else if (surface.Outwards.Y == 1f)
            {
                x -= num3;
                num2 = surface.Width -
                       (int)ScalarMath.Clamp(owner.Camera.Left - (float)horizontalWaterHeightVisiblityBuffer - x, 0f,
                           surface.Width);
                num = surface.Width -
                      (int)ScalarMath.Clamp(owner.Camera.Right + (float)horizontalWaterHeightVisiblityBuffer - x, 0f,
                          surface.Width);
            }
            else if (surface.Outwards.X == -1f)
            {
                y -= num3;
                num2 = surface.Width -
                       (int)ScalarMath.Clamp(owner.Camera.Top - (float)verticalWaterHeightVisiblityBuffer - y, 0f,
                           surface.Width);
                num = surface.Width -
                      (int)ScalarMath.Clamp(owner.Camera.Bottom + (float)verticalWaterHeightVisiblityBuffer - y, 0f,
                          surface.Width);
            }
            else if (surface.Outwards.X == 1f)
            {
                y -= num3;
                num = (int)ScalarMath.Clamp(owner.Camera.Top - (float)verticalWaterHeightVisiblityBuffer - y, 0f,
                    surface.Width);
                num2 = (int)ScalarMath.Clamp(owner.Camera.Bottom + (float)verticalWaterHeightVisiblityBuffer - y, 0f,
                    surface.Width);
            }

            if (!IsSurfaceSectionVisible(surface, num, num2))
            {
                num = 0;
                num2 = -1;
            }

            return new Vector2(num, num2);
        }

        private bool IsSurfaceSectionVisible(OriginalWaterState.Surface surface, float position1, float position2,
            float depth = 6f)
        {
            float num = surface.Position.X;
            float num2 = surface.Position.Y;
            float num3 = surface.Position.X;
            float num4 = surface.Position.Y;
            float num5 = surface.Width;
            float num6 = num5 / 2f;
            if (surface.Outwards.Y == -1f)
            {
                num += position1 - num6;
                num3 += position2 - num6;
                num4 += depth;
            }
            else if (surface.Outwards.Y == 1f)
            {
                num += num6 - position2;
                num3 += num6 - position1;
                num2 -= depth;
            }
            else if (surface.Outwards.X == -1f)
            {
                num2 += num6 - position2;
                num4 += num6 - position1;
                num3 += depth;
            }
            else if (surface.Outwards.X == 1f)
            {
                num2 += position1 - num6;
                num4 += position2 - num6;
                num -= depth;
            }

            bool flag = num < owner.Camera.Right + (float)horizontalWaterHeightVisiblityBuffer &&
                        num3 > owner.Camera.Left - (float)horizontalWaterHeightVisiblityBuffer;
            bool flag2 = num2 < owner.Camera.Bottom + (float)verticalWaterHeightVisiblityBuffer &&
                         num4 > owner.Camera.Top - (float)verticalWaterHeightVisiblityBuffer;
            return flag && flag2;
        }

        private float GetCachedSurfaceHeight(OriginalWaterState.Surface surface, float[] cache, float position,
            float timer)
        {
            int num = (int)Math.Floor(position / 4f);
            if (num < 0 || num >= cache.Length)
            {
                return 6f;
            }

            if (cache[num] == 0f)
            {
                cache[num] = CustomGetHeight(surface, position, timer);
            }

            return cache[num];
        }

        private float CustomGetHeight(OriginalWaterState.Surface surface, float position, float timer)
        {
            if (position < 0f || position > (float)surface.Width)
            {
                return 0f;
            }

            float num = 0f;
            foreach (OriginalWaterState.Ripple ripple in surface.Ripples)
            {
                float num2 = Math.Abs(ripple.Position - position);
                float num3 = 0f;
                num3 = ((!(num2 < 12f))
                    ? ((!(num2 < 16f)) ? ((!(num2 <= 32f)) ? 0f : ((num2 - 32f) / 16f * 0.75f)) : (-0.75f))
                    : (num2 / 16f * -1.75f + 1f));
                num += num3 * ripple.Height *
                       ((ISourceEasingEnvironment)engine).EasingArithmetic.CubeIn(1f - ripple.Percent);
            }

            num = ScalarMath.Clamp(num, -4f, 4f);
            foreach (OriginalWaterState.Tension tension in surface.Tensions)
            {
                float t = ScalarMath.ClampedMap(Math.Abs(tension.Position - position), 0f, 24f, 1f, 0f);
                num += ((ISourceEasingEnvironment)engine).EasingArithmetic.CubeOut(t) * tension.Strength * 12f;
            }

            float num4 = position / (float)surface.Width;
            num *= Math.Min(0.5f + num4 * 5f, 1f);
            num *= Math.Min(0.5f + (1f - num4) * 5f, 1f);
            num += (float)Math.Sin(timer + position * 0.1f);
            return num + 6f;
        }

        public override void Update()
        {
            var self = this;
            float num = timer;
            timer = num + engine.DeltaTime;
            Vector2 vector = Vector2Geometry.Perpendicular(self.Outwards);
            for (int num2 = self.Ripples.Count - 1; num2 >= 0; num2--)
            {
                OriginalWaterState.Ripple ripple = self.Ripples[num2];
                if (ripple.Percent > 1f)
                {
                    self.Ripples.RemoveAt(num2);
                }
                else
                {
                    ripple.Position += ripple.Speed * engine.DeltaTime;
                    if (ripple.Position < 0f || ripple.Position > (float)self.Width)
                    {
                        ripple.Speed = 0f - ripple.Speed;
                        ripple.Position = ScalarMath.Clamp(ripple.Position, 0f, self.Width);
                    }

                    ripple.Percent += engine.DeltaTime / ripple.Duration;
                }
            }

            if (!IsSurfaceSectionVisible(self, 0f, self.Width, rayMaxLength))
            {
                return;
            }

            VertexPositionColor[] array = mesh;
            int num3 = fillStartIndex;
            int num4 = surfaceStartIndex;
            int num5 = rayStartIndex;
            float[] cache = surfaceHeightCache;
            Array.Clear(cache, 0, cache.Length);
            Vector2 visibleSurfaceRange = GetVisibleSurfaceRange(self);
            int num6 = (int)visibleSurfaceRange.X;
            int num7 = (int)visibleSurfaceRange.Y;
            int num8 = num6;
            int num9 = num3;
            int num10 = num4;
            float num11 = self.Width / 2;
            float num12 = GetCachedSurfaceHeight(self, cache, num8, num);
            while (num8 < num7)
            {
                int num13 = num8;
                int num14 = Math.Min(num8 + 4, self.Width);
                float cachedSurfaceHeight = GetCachedSurfaceHeight(self, cache, num14, num);
                Vector2 vector2 = self.Outwards * num12;
                Vector2 vector3 = self.Outwards * cachedSurfaceHeight;
                Vector2 vector4 = self.Position + vector * (0f - num11 + (float)num13);
                Vector2 vector5 = self.Position + vector * (0f - num11 + (float)num14);
                array[num9].Position = new Vector3(vector4 + vector2, 0f);
                array[num9 + 1].Position = new Vector3(vector5 + vector3, 0f);
                array[num9 + 2].Position = new Vector3(vector4, 0f);
                array[num9 + 3].Position = new Vector3(vector5 + vector3, 0f);
                array[num9 + 4].Position = new Vector3(vector5, 0f);
                array[num9 + 5].Position = new Vector3(vector4, 0f);
                array[num10].Position = new Vector3(vector4 + self.Outwards * (num12 + 1f), 0f);
                array[num10 + 1].Position = new Vector3(vector5 + self.Outwards * (cachedSurfaceHeight + 1f), 0f);
                array[num10 + 2].Position = new Vector3(vector4 + vector2, 0f);
                array[num10 + 3].Position = new Vector3(vector5 + self.Outwards * (cachedSurfaceHeight + 1f), 0f);
                array[num10 + 4].Position = new Vector3(vector5 + vector3, 0f);
                array[num10 + 5].Position = new Vector3(vector4 + vector2, 0f);
                num8 += 4;
                num9 += 6;
                num10 += 6;
                num12 = cachedSurfaceHeight;
            }

            Vector2 vector6 = self.Position + vector * ((float)(-self.Width) / 2f);
            int num15 = num5;
            bool flag = IsSurfaceSectionVisible(self, 0f, self.Width, rayMaxLength);
            foreach (OriginalWaterState.Ray ray in self.Rays)
            {
                if (ray.Percent > 1f)
                {
                    ray.Reset(0f);
                }

                ray.Percent += engine.DeltaTime / ray.Duration;
                float num16 = 1f;
                if (!flag)
                {
                    num15 += 6;
                    continue;
                }

                float num17 = Math.Max(0f, ray.Position - ray.Width / 2f);
                float num18 = Math.Min(self.Width, ray.Position + ray.Width / 2f);
                if (!IsSurfaceSectionVisible(self, num17, num18, rayMaxLength))
                {
                    num15 += 6;
                    continue;
                }

                if (ray.Percent < 0.1f)
                {
                    num16 = ray.Percent * 10f;
                }
                else if (ray.Percent > 0.9f)
                {
                    num16 = 1f - (ray.Percent - 0.9f) * 10f;
                }

                float num19 = Math.Min(self.BodyHeight, 0.7f * ray.Length);
                Vector2 vector7 = self.Outwards * num19;
                PackedColor color = appearance.RayTopColor * num16;
                float num20 = 0.3f * ray.Length;
                Vector2 value = vector6 + vector * num17 +
                                self.Outwards * GetCachedSurfaceHeight(self, cache, num17, num);
                Vector2 value2 = vector6 + vector * num18 +
                                 self.Outwards * GetCachedSurfaceHeight(self, cache, num18, num);
                Vector2 value3 = vector6 + vector * (num18 - num20) - vector7;
                Vector2 value4 = vector6 + vector * (num17 - num20) - vector7;
                Vector3 position = new Vector3(value2, 0f);
                Vector3 position2 = new Vector3(value4, 0f);
                array[num15].Position = new Vector3(value, 0f);
                array[num15].Color = color;
                array[num15 + 1].Position = position;
                array[num15 + 1].Color = color;
                array[num15 + 2].Position = position2;
                array[num15 + 3].Position = position;
                array[num15 + 3].Color = color;
                array[num15 + 4].Position = new Vector3(value3, 0f);
                array[num15 + 5].Position = position2;
                num15 += 6;
            }
        }
    }
}
