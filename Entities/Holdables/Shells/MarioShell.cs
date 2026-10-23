// SPDX-License-Identifier: MIT
// Copyright (C) 2018-2020 Cruor

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Graphics.Content;
using Celeste.Graphics.Animation;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Holdables;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Mods.PandorasBox.Color;

namespace Celeste.Mods.PandorasBox.Entities.Holdables.Shells
{
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.MarioShell")]
    [CustomEntity("pandorasBox/shell")]
    internal partial class MarioShell : SourceActorEntityState
    {
        public float grace;

        public float springGrace;

        public float turnTime;

        public Vector2 Speed;

        public List<PackedColor> colors;

        public float colorSpeed;

        public float timeAcc;

        public string texture;

        public int id;

        public bool dangerous;

        public bool disco;

        public bool grabbable;

        public float baseSpeed;

        public float throwingHeightMultiplier;

        public float fallingAcceleration;

        public float maxFallingSpeed;

        public HoldableState Hold;

        public object scene;

        public Vector2 prevLiftspeed;

        private Sprite decorationIdle;

        private Sprite decorationMoving;

        private Sprite shellIdle;

        private Sprite shellMoving;

        private BloomPointState bloom;

        private VertexLightState light;

        private HitboxColliderGeometry pickupMovingCollider;

        private HitboxColliderGeometry pickupIdleCollider;

        private HitboxColliderGeometry shellNotHeldCollider;

        private HitboxColliderGeometry shellHeldCollider;

        private SourceCollisionHandler onCollideH;

        private SourceCollisionHandler onCollideV;

        public static float GRAVITY_HORIZONTAL_SPRING = -140f;

        public static float GRAVITY_VERTICAL_SPRING = -240f;

        public static float GRAVITY_AERIAL_KICK = -80f;

        public static float GRACE_PUSH = 0.1f;

        public static float GRACE_THROW = 0.1f;

        public static float GRACE_BOUNCE = 0.1f;

        public static float GRACE_SPRING = 0.1f;

        public static float DISCO_TURN_DELAY = 0.3f;

        public static float BASE_SPEED = 200f;

        public static float THROWING_HEIGHT_MULTIPLIER = 0f;

        public static float FALLING_ACCELERATION = 400f;

        public static float MAX_FALLING_SPEED = 200f;

        public static Dictionary<string, bool> dangerousTextures = new Dictionary<string, bool>
        {
            { "koopa", false },
            { "spiny", true },
            { "beetle", false },
            { "bowserjr", true }
        };

        private bool alreadyAdded
        {
            get
            {
                foreach (var entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(shellKey))
                {
                    var other = (MarioShell)entity;
                    if (other.id == id && Hold.IsHeld != other.Hold.IsHeld) return true;
                }

                return false;
            }
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            this.scene = scene;
            if (alreadyAdded)
            {
                RemoveSelf();
            }
        }

        private readonly ISourceEngineContext engine;
        private readonly string shellKey, playerKey;

        public static MarioShell Load(OriginalLevelSceneRoot level, OriginalLevelRoomData levelData, Vector2 offset,
            EntityData data)
            => new MarioShell(level.EngineContext, data, offset);

        public MarioShell(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(new PixelActor(null, data.Position + offset), engine,
                ((ISourcePlatformTargetMotionEnvironment)engine).TargetMotionPolicy)
        {
            this.engine = engine;
            var catalog = ((ISourceSceneCatalogEnvironment)engine).SceneCatalog;
            shellKey = catalog.Find(typeof(MarioShell)).RegistryKey;
            playerKey = catalog.Find(typeof(RoomPlayerSession)).RegistryKey;
            colors = new List<PackedColor>();
            timeAcc = 0f;
            bool lights = EntityAttributeReader.Bool(data.Values, "lights", false);
            texture = EntityAttributeReader.Attr(data.Values, "texture", "koopa");
            dangerous = EntityAttributeReader.Bool(data.Values, "dangerous",
                dangerousTextures.ContainsKey(texture) && dangerousTextures[texture]);
            disco = EntityAttributeReader.Bool(data.Values, "disco", false);
            grabbable = EntityAttributeReader.Bool(data.Values, "grabbable", true);
            baseSpeed = EntityAttributeReader.Float(data.Values, "baseSpeed", BASE_SPEED);
            throwingHeightMultiplier =
                EntityAttributeReader.Float(data.Values, "throwingHeightMultiplier", THROWING_HEIGHT_MULTIPLIER);
            maxFallingSpeed = EntityAttributeReader.Float(data.Values, "maxFallingSpeed", MAX_FALLING_SPEED);
            fallingAcceleration = EntityAttributeReader.Float(data.Values, "fallingAcceleration", FALLING_ACCELERATION);
            string color = EntityAttributeReader.Attr(data.Values, "color", "Green");
            colorSpeed = EntityAttributeReader.Float(data.Values, "colorSpeed", .8f);
            foreach (string part in color.Split(',')) colors.Add(ColorReader.GetColor(part));
            Speed = new Vector2(baseSpeed * Math.Sign(EntityAttributeReader.Int(data.Values, "direction", 0)), 0f);
            grace = 0f;
            prevLiftspeed = Vector2.Zero;
            pickupIdleCollider = new HitboxColliderGeometry(18f, 18f, -9f, -9f);
            pickupMovingCollider = new HitboxColliderGeometry(0f, 0f, 0f, 0f);
            shellHeldCollider = new HitboxColliderGeometry(8f, 14f, -4f, -7f);
            shellNotHeldCollider = new HitboxColliderGeometry(14f, 14f, -7f, -7f);
            Add(new PlayerColliderComponentState(OnPlayer));
            Collider = shellNotHeldCollider;
            onCollideH = OnCollideH;
            onCollideV = OnCollideV;
            decorationIdle = CreateSprite("deco_idle");
            shellIdle = CreateSprite("shell_idle");
            shellIdle.Color = colors[0];
            decorationMoving = CreateSprite("deco_moving");
            shellMoving = CreateSprite("shell_moving");
            shellMoving.Color = colors[0];
            Add(Hold = new HoldableState(engine));
            Hold.PickupCollider = new HitboxColliderGeometry(18f, 18f, -9f, -9f);
            Hold.OnPickup = OnPickup;
            Hold.OnRelease = OnRelease;
            Hold.SpeedGetter = () => Speed;
            Hold.DangerousCheck = DangerousCheck;
            Hold.OnHitSpring = value => HitSpring((ISourceSpringEntity)value);
            decorationMoving.Visible = shellMoving.Visible = false;
            id = data.ID;
            if (lights)
            {
                Add(bloom = new BloomPointState(1f, 16f));
                Add(light = new VertexLightState(engine, Collider.Center, PackedColor.White, 1f, 8, 24));
                bloom.Visible = light.Visible = true;
            }
        }

        private Sprite CreateSprite(string animation)
        {
            var sprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                "objects/pandorasBox/shells/" + texture + "/" + animation);
            Add(sprite);
            sprite.AddLoop(animation, "", .1f);
            sprite.Play(animation, true, false);
            sprite.CenterOrigin();
            return sprite;
        }

        public override void Render(ISourceRenderContext context)
        {
            OwnedComponents.Render(context);
        }
    }
}
