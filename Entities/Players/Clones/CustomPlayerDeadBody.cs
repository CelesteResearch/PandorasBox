using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Simulation;
using Celeste.Game.Execution.Levels;
using Celeste.Simulation.Presentation.Player.Death;
using System;
using System.Collections;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components.Animation;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Player.Presentation.Hair;
using Celeste.Simulation.Gameplay.Player.Presentation.Sprites;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.ScreenEffects;
using Celeste.Graphics.Display;

namespace Celeste.Mods.PandorasBox.Entities.Players.Clones
{
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.CustomPlayerDeadBody")]
    public class CustomPlayerDeadBody : SceneEntityState
    {
        public Action DeathAction;
        public float ActionDelay;
        public bool HasGolden;
        private PlayerHair hair;
        private PlayerSprite sprite;
        private VertexLightState light;
        private DeathEffect deathEffect;
        private bool finished;

        private readonly bool endLevel;
        private readonly bool muted;
        private PackedColor initialHairColor;
        private readonly RoomPlayerSession player;
        private Vector2 bounce;
        private int facing;
        private float scale = 1f;

        public CustomPlayerDeadBody(RoomPlayerSession player, Vector2 direction, bool endLevel = false,
            bool muted = true)
            : base(((global::Celeste.Core.Components.ISourceEntitySourceDataEnvironment)player.EngineContext)
                .EntitySourceDataRegistration)
        {
            Depth = -1000000;
            this.player = player;
            this.endLevel = endLevel;
            this.muted = muted;
            facing = player.Data.Facing;
            Position = player.Position;
            Components.Add(hair = player.Hair);
            Components.Add(sprite = player.Sprite);
            Components.Add(light = player.Light);
            sprite.Color = PackedColor.White;
            initialHairColor = hair.Color;
            bounce = direction;
            Components.Add(new SimulationCoroutine(player.EngineContext, DeathRoutine()));
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (!(bounce != Vector2.Zero)) return;
            if (Math.Abs(bounce.X) > Math.Abs(bounce.Y))
            {
                sprite.Play("deadside");
                facing = -Math.Sign(bounce.X);
                return;
            }

            bounce = Vector2Geometry.AngleToVector(ScalarMath.AngleApproach(
                Vector2Geometry.Angle(bounce),
                Vector2Geometry.Angle(new Vector2(-player.Data.Facing, 0f)), .5f), 1f);
            sprite.Play(bounce.Y < 0f ? "deadup" : "deaddown");
        }

        private void PlayAudio(string name, Vector2 position)
        {
            if (!muted)
                ((ISourceSpatialAudioEnvironment)player.EngineContext).SpatialAudio.Play(name, position);
        }

        private IEnumerator DeathRoutine()
        {
            var level = (OriginalLevelSceneRoot)Scene;
            ISourceEngineContext engine = level.EngineContext;
            if (bounce != Vector2.Zero)
            {
                scale = 1.5f;
                PlayAudio("event:/char/madeline/predeath", Position);
                ((ISourceFreezeEnvironment)engine).Freeze(.05f);
                yield return null;
                Vector2 from = Position;
                Vector2 to = from + bounce * 24f;
                var tween = SourceTweenState.Create(engine.TweenPool, SourceTweenState.TweenMode.Oneshot,
                    ((ISourceEasingEnvironment)engine).EasingArithmetic.CubeOut, .5f, start: true);
                Components.Add(tween);
                tween.OnUpdate = t =>
                {
                    Position = from + (to - from) * t.Eased;
                    scale = 1.5f - t.Eased * .5f;
                    sprite.Rotation = (float)(Math.Floor(t.Eased * 4f) * 6.2831854820251465);
                };
                yield return tween.Duration * .75f;
                tween.Stop();
            }

            Position += Vector2.UnitY * -5f;
            level.Displacement.AddBurst(Position, .3f, 0f, 80f, 1f, null, null);
            level.Shake(.3f);
            ((ISourceRumbleEnvironment)engine).RumbleSession.Rumble(2, 2);
            PlayAudio(HasGolden ? "event:/new_content/char/madeline/death_golden" : "event:/char/madeline/death",
                Position);
            deathEffect = new DeathEffect(initialHairColor, engine, Center - Position);
            deathEffect.OnUpdate = f => light.Alpha = 1f - f;
            Components.Add(deathEffect);
            yield return deathEffect.Duration * .65f;
            if (ActionDelay > 0f) yield return ActionDelay;
            End();
        }

        private void End()
        {
            if (finished || !endLevel) return;
            finished = true;
            var level = (OriginalLevelSceneRoot)Scene;
            if (DeathAction == null) DeathAction = level.Reload;
            level.DoScreenWipe(false, DeathAction, false);
        }

        public override void Update()
        {
            base.Update();
            if (player.Input[global::Celeste.Input.Gameplay.GameAction.MenuConfirm].Pressed && !finished) End();
            hair.Color = sprite.CurrentAnimationFrame == 0 ? PackedColor.White : initialHairColor;
        }

        public override void Render(ISourceRenderContext context)
        {
            if (deathEffect == null)
            {
                sprite.Scale.X = facing * scale;
                sprite.Scale.Y = scale;
                hair.Facing = facing;
                base.Render(context);
            }
            else deathEffect.Render(context);
        }
    }
}
