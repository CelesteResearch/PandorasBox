using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Gameplay.Mechanisms.DashSwitches;
using Celeste.Game.Entities.Platforms.SwapBlocks;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Input.Gameplay;
using Celeste.Mods.PandorasBox.Color;
using Celeste.Mods.PandorasBox.Components.Holdables;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Environment.Wind;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;

namespace Celeste.Mods.PandorasBox.Entities.Holdables
{
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.PropellerBox")]
    [CustomEntity("pandorasBox/propellerBox")]
    public class PropellerBox : SourceActorEntityState
    {
        public static float BoostLaunchSpeed = -280f;

        public static float BoostLaunchThreshold = -120f;

        private readonly ISourceEngineContext engine;
        private ISourceGameInput Input => ((ISourceGameInputEnvironment)engine).Input;
        private AtlasData GameAtlas => ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas;

        public static PropellerBox Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            EntityData data) => new(level.EngineContext, data, offset);

        private ISourceAudioEventReference Play(string path) =>
            ((IOriginalGameplayProcess)engine).AudioSession.Play(path, Position,
                ((IOriginalGameplayProcess)engine).SaveContext);

        private float noGravityTimer;

        private float hardVerticalHitSoundCooldown;

        private OriginalLevelSceneRoot level;

        private string texture;

        private SourceCollisionHandler onCollideH;

        private SourceCollisionHandler onCollideV;

        private Vector2 prevLiftSpeed;

        private Vector2 previousPosition;

        private float boostDuration;

        private Sprite flashOverlaySprite;
        private readonly Action<string> flashFinished;

        private List<Sprite> chargeSprites;

        private PackedColor flashUseColor;

        private PackedColor flashRechargedColor;

        private InteractibleHoldable interactibleHoldable;

        public Vector2 Speed;

        public HoldableState Hold;

        public int MaxCharges;

        public int Charges;

        private bool rechargeOnGround;

        private string glideMode;

        public bool HasBoosted;

        public PropellerBox(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(new PixelActor(null, data.Position + offset), engine,
                ((ISourcePlatformTargetMotionEnvironment)engine).TargetMotionPolicy)
        {
            this.engine = engine;
            base.Depth = 100;
            base.Collider = new HitboxColliderGeometry(8f, 10f, -4f, -10f);
            MaxCharges = data.Int("maxCharges", 3);
            Charges = MaxCharges;
            texture = data.Attr("texture", "default");
            flashUseColor = ColorReader.GetColor(data.Attr("flashUseColor", "#3F437C"));
            flashRechargedColor = ColorReader.GetColor(data.Attr("flashChargeColor", "#5A1C1C"));
            glideMode = data.Attr("glideMode", "AfterUse");
            rechargeOnGround = data.Bool("rechargeOnGround", true);
            chargeSprites = new List<Sprite>();
            for (int i = 0; i <= MaxCharges; i++)
            {
                addChargeSprite(i);
            }

            Add(flashOverlaySprite = new Sprite(engine, GameAtlas,
                "objects/pandorasBox/propellerBox/" + texture + "/flash_overlay"));
            flashOverlaySprite.Add("flash_overlay", "", 0.045f);
            flashOverlaySprite.Justify = new Vector2(0.5f, 1f);
            flashFinished = FlashFinished;
            Add(Hold = new HoldableState(engine));
            Hold.PickupCollider = new HitboxColliderGeometry(16f, 22f, -8f, -16f);
            Hold.OnPickup = OnPickup;
            Hold.OnRelease = OnRelease;
            Hold.DangerousCheck = DangerousCheck;
            Hold.OnHitSpring = value => HitSpring((ISourceSpringEntity)value);
            Hold.SpeedGetter = () => Speed;
            Hold.SlowFall = glideMode == "Always";
            onCollideH = OnCollideH;
            onCollideV = OnCollideV;
            LiftSpeedGraceTime = 0.1f;
            Add(new VertexLightState(engine, base.Collider.Center, PackedColor.White, 1f, 32, 64));
            Add(new WindMoverState(WindMode));
            updateChargeSpriteVisibility();
            base.Tags = ((IOriginalGameTagsEnvironment)engine).Tags.TransitionUpdate.Value;
        }

        private bool animationExists(string key)
        {
            return GameAtlas.HasAtlasSubtextures(key);
        }

        private void addChargeSprite(int charge)
        {
            string text = $"{charge}_charges";
            string text2 = "objects/pandorasBox/propellerBox/" + texture + "/" + text;
            Sprite sprite;
            if (animationExists(text2))
            {
                sprite = new Sprite(engine, GameAtlas, text2);
            }
            else
            {
                sprite = new Sprite(engine, GameAtlas,
                    "objects/pandorasBox/propellerBox/" + texture + "/default_charges");
                text = "default_charges";
            }

            sprite.AddLoop(text, "", 0.1f);
            sprite.Justify = new Vector2(0.5f, 1f);
            sprite.Play(text, restart: true);
            Add(sprite);
            chargeSprites.Add(sprite);
        }

        private void updateChargeSpriteVisibility()
        {
            for (int i = 0; i <= MaxCharges; i++)
            {
                Sprite sprite = chargeSprites[i];
                sprite.Visible = i == Charges;
            }
        }

        private void changeChargeSpriteRate(float rate)
        {
            for (int i = 0; i <= MaxCharges; i++)
            {
                Sprite sprite = chargeSprites[i];
                sprite.Rate = rate;
            }
        }

        private void OnCollideV(PixelCollision data)
        {
            if (data.Hit is OriginalDashSwitchState)
            {
                (data.Hit as OriginalDashSwitchState).OnDashCollide(null, Vector2.UnitY * Math.Sign(Speed.Y));
            }

            if (Speed.Y > 0f)
            {
                if (hardVerticalHitSoundCooldown <= 0f)
                {
                    Play("event:/game/04_cliffside/greenbooster_end");
                    hardVerticalHitSoundCooldown = 0.5f;
                }
                else
                {
                    Play("event:/game/04_cliffside/greenbooster_end").SetVolume(.6f);
                }
            }

            if (Speed.Y > 160f)
            {
                ImpactParticles(data.Direction);
            }

            if (Speed.Y > 140f && !(data.Hit is OriginalSwapBlockState) && !(data.Hit is OriginalDashSwitchState))
            {
                Speed.Y *= -0.6f;
            }
            else
            {
                Speed.Y = 0f;
            }
        }

        private void OnCollideH(PixelCollision data)
        {
            Play("event:/game/04_cliffside/greenbooster_enter");
            if (data.Hit is OriginalDashSwitchState)
            {
                (data.Hit as OriginalDashSwitchState).OnDashCollide(null, Vector2.UnitX * Math.Sign(Speed.X));
            }

            if (Math.Abs(Speed.X) > 100f)
            {
                ImpactParticles(data.Direction);
            }

            Speed.X *= -0.4f;
        }

        private bool DangerousCheck(object collider)
        {
            return !Hold.IsHeld && Speed != Vector2.Zero;
        }

        public bool HitSpring(ISourceSpringEntity spring)
        {
            if (!Hold.IsHeld)
            {
                if (spring.Orientation == SpringOrientation.Floor && Speed.Y >= 0f)
                {
                    Speed.X *= 0.5f;
                    Speed.Y = -160f;
                    noGravityTimer = 0.15f;
                    return true;
                }

                if (spring.Orientation == SpringOrientation.WallLeft && Speed.X <= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f);
                    Speed.X = 220f;
                    Speed.Y = -80f;
                    noGravityTimer = 0.1f;
                    return true;
                }

                if (spring.Orientation == SpringOrientation.WallRight && Speed.X >= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f);
                    Speed.X = -220f;
                    Speed.Y = -80f;
                    noGravityTimer = 0.1f;
                    return true;
                }
            }

            return false;
        }

        private void OnRelease(Vector2 force)
        {
            Tags &= ~((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value;
            if (force.X != 0f && force.Y == 0f)
            {
                force.Y = -0.4f;
            }

            Speed = force * 200f;
            if (Speed != Vector2.Zero)
            {
                noGravityTimer = 0.1f;
            }

            interactibleHoldable?.OnHoldRelease?.Invoke(Hold);
        }

        private void OnPickup()
        {
            Speed = Vector2.Zero;
            Tags |= ((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value;
            interactibleHoldable?.OnHoldPickup?.Invoke(Hold);
        }

        public override void Update()
        {
            if (boostDuration > 0f)
            {
                boostDuration -= engine.DeltaTime;
            }

            if (hardVerticalHitSoundCooldown > 0f)
            {
                hardVerticalHitSoundCooldown -= engine.DeltaTime;
            }

            Hold.CheckAgainstColliders();
            updateVisuals();
            makeSparks();
            if ((Hold.IsHeld && Hold.Holder.OnGround()) || (!Hold.IsHeld && OnGround()))
            {
                landed();
            }

            if (Hold.IsHeld)
            {
                prevLiftSpeed = Vector2.Zero;
                interactibleHoldable?.OnHoldUpdate?.Invoke(Hold);
            }
            else
            {
                if (OnGround())
                {
                    CarryableActorMotionSteps.AdvanceGroundSpeed(this, engine, ref Speed, ref prevLiftSpeed,
                        ref noGravityTimer);
                }
                else if (Hold.ShouldHaveGravity)
                {
                    float terminal = noGravityTimer > 0f ? 30f : level.Wind.Y < 0f ? 0f : 30f;
                    CarryableActorMotionSteps.AdvanceAirSpeed(engine, ref Speed, ref noGravityTimer, terminal);
                }

                previousPosition = Body.ExactPosition;
                MoveH(Speed.X * engine.DeltaTime, onCollideH);
                MoveV(Speed.Y * engine.DeltaTime, onCollideV);
            }

            base.Update();
        }

        private void ImpactParticles(Vector2 dir)
        {
            float direction;
            Vector2 position;
            Vector2 positionRange;
            if (dir.X > 0f)
            {
                direction = (float)Math.PI;
                position = new Vector2(base.Right, base.Y - 4f);
                positionRange = Vector2.UnitY * 6f;
            }
            else if (dir.X < 0f)
            {
                direction = 0f;
                position = new Vector2(base.Left, base.Y - 4f);
                positionRange = Vector2.UnitY * 6f;
            }
            else if (dir.Y > 0f)
            {
                direction = -(float)Math.PI / 2f;
                position = new Vector2(base.X, base.Bottom);
                positionRange = Vector2.UnitX * 6f;
            }
            else
            {
                direction = (float)Math.PI / 2f;
                position = new Vector2(base.X, base.Top);
                positionRange = Vector2.UnitX * 6f;
            }

            level.Particles.Emit(((IOriginalGameplayProcess)engine).ParticleTypes.TheoCrystal.P_Impact, 12, position,
                positionRange, direction);
        }

        private void WindMode(Vector2 wind)
        {
            if (!Hold.IsHeld && !(Hold.Entity as PropellerBox).OnGround())
            {
                if (wind.X != 0f)
                {
                    MoveH(wind.X * 0.3f);
                }

                if (wind.Y != 0f)
                {
                    MoveV(wind.Y * 0.7f);
                }
            }
        }

        private void makeSparks()
        {
            if (boostDuration > 0f && ((ISourceScene)Scene).SourceScene.Clock.OnInterval(.06f, engine))
            {
                ((OriginalLevelSceneRoot)Scene).ParticlesFG.Emit(
                    ((IOriginalGameplayProcess)engine).ParticleTypes.ClutterSwitch.P_ClutterFly, 1, base.Center,
                    Vector2.One * 12f);
            }
        }

        private void onHoldUpdate(HoldableState hold)
        {
            bool flag = hold.Holder.Sprite.CurrentAnimationID == "pickup";
            if ((Input.Dash.Pressed || Input.CrouchDash.Pressed) && !flag && Charges > 0)
            {
                useCharge(hold);
            }
        }

        private void updateVisuals()
        {
            if (Hold.IsHeld)
            {
                if (boostDuration > 0f)
                {
                    changeChargeSpriteRate(2.5f);
                }
                else if (HasBoosted)
                {
                    changeChargeSpriteRate(1.6f);
                }
                else
                {
                    changeChargeSpriteRate(1f);
                }
            }
            else if (OnGround())
            {
                changeChargeSpriteRate(0.4f);
            }
            else
            {
                changeChargeSpriteRate(1f);
            }
        }

        private void FlashFinished(string animation) => flashOverlaySprite.Visible = false;

        private void showChargeFlash()
        {
            flashOverlaySprite.Play("flash_overlay", restart: true);
            flashOverlaySprite.Visible = true;
            flashOverlaySprite.OnFinish = flashFinished;
        }

        private void useCharge(HoldableState hold)
        {
            RoomPlayerSession holder = hold.Holder;
            if (holder.Speed.Y > BoostLaunchThreshold)
            {
                holder.Data.Motion.Normal.Speed.Y = BoostLaunchSpeed;
                if (holder.Machine.State == 8)
                {
                    holder.Machine.State = 0;
                }

                HasBoosted = true;
                boostDuration = 1.2f;
                if (glideMode == "AfterUse")
                {
                    hold.SlowFall = true;
                }

                Play("event:/game/01_forsaken_city/birdbros_thrust");
                holder.Launch.Begin();
                Charges--;
                flashOverlaySprite.Color = flashUseColor;
                updateChargeSpriteVisibility();
                showChargeFlash();
                Input.Dash.ConsumeBuffer();
            }
        }

        private void landed()
        {
            if (glideMode == "AfterUse")
            {
                Hold.SlowFall = false;
            }

            if (rechargeOnGround && MaxCharges > Charges)
            {
                Charges = MaxCharges;
                HasBoosted = false;
                flashOverlaySprite.Color = flashRechargedColor;
                updateChargeSpriteVisibility();
                showChargeFlash();
            }
        }

        private void onHoldPickup(HoldableState hold)
        {
            Input.Dash.ConsumeBuffer();
        }

        public override void Added(object scene)
        {
            level = scene as OriginalLevelSceneRoot;
            interactibleHoldable = new InteractibleHoldable(this);
            interactibleHoldable.OnHoldUpdate = onHoldUpdate;
            Add(interactibleHoldable);
            base.Added(scene);
        }
    }
}
