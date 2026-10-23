// SPDX-License-Identifier: MIT

using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Mods.PandorasBox.Content.Switches;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;

namespace Celeste.Mods.PandorasBox.Entities.Switches
{
    [CustomEntity("pandorasBox/flagToggleSwitch")]
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.FlagToggleSwitch")]
    internal class FlagToggleSwitch : SceneEntityState
    {
        private const float Cooldown = 1f;
        private readonly ISourceEngineContext engine;
        private bool flagActive;
        private float cooldownTimer;
        private bool onlyOn;
        private bool onlyOff;
        private string flag;
        private int id;
        private bool silent;
        private bool playSounds;
        private Sprite sprite;
        private string fallbackFlag;
        private int fallbackId;
        private bool Usable => !((onlyOff && !flagActive) || (onlyOn && flagActive));

        private string FlagKey
        {
            get
            {
                if (!string.IsNullOrEmpty(flag)) return flag;
                if (fallbackFlag == null || fallbackId != id)
                {
                    fallbackId = id;
                    fallbackFlag = "pb_flagtoggleswitch_" + id;
                }

                return fallbackFlag;
            }
        }

        public FlagToggleSwitch(ISourceEngineContext engine, Vector2 position, int id, bool onlyOn, bool onlyOff,
            bool silent, string flag)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, position)
        {
            this.engine = engine;
            this.id = id;
            this.onlyOn = onlyOn;
            this.onlyOff = onlyOff;
            this.flag = flag;
            this.silent = silent;
            Collider = new HitboxColliderGeometry(16f, 24f, -8f, -12f);
            Add(new PlayerColliderComponentState(OnPlayer));
            Add(sprite = FlagToggleSwitchResources.For(engine).SpriteBank.Create("pandorasBoxFlagTooggleSwitch"));
            Depth = 2000;
        }

        public FlagToggleSwitch(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : this(engine, data.Position + offset, data.ID, data.Bool("onlyOn", false), data.Bool("onlyOff", false),
                data.Bool("silent", false), data.Attr("flag", ""))
        {
        }

        public override void Added(object scene)
        {
            base.Added(scene);
            flagActive = getFlag();
            SetSprite(false);
        }

        private void SetSprite(bool animate)
        {
            if (animate)
            {
                if (playSounds)
                    ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                        flagActive ? "event:/game/09_core/switch_to_hot" : "event:/game/09_core/switch_to_cold",
                        Position);
                if (Usable) sprite.Play(flagActive ? "activate" : "deactivate");
                else
                {
                    if (playSounds)
                        ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play("event:/game/09_core/switch_dies",
                            Position);
                    sprite.Play(flagActive ? "activateOff" : "deactivateOff");
                }
            }
            else if (Usable) sprite.Play(flagActive ? "activateLoop" : "deactivateLoop");
            else sprite.Play(flagActive ? "activateOffLoop" : "deactivateOffLoop");

            playSounds = false;
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            if (Usable && cooldownTimer <= 0f)
            {
                playSounds = !silent;
                flagActive = !flagActive;
                setFlag();
                SetSprite(true);
                var level = (OriginalLevelSceneRoot)Scene;
                ((IOriginalGameInputProcessEnvironment)engine).InputSource.Rumble(RumbleStrength.Medium,
                    RumbleLength.Medium);
                level.Flash(PackedColor.White * .15f, true);
                ((ISourceFreezeEnvironment)engine).Freeze(.05f);
                cooldownTimer = Cooldown;
            }
        }

        public override void Update()
        {
            base.Update();
            if (cooldownTimer > 0f) cooldownTimer -= engine.DeltaTime;
            bool value = getFlag();
            if (value != flagActive)
            {
                flagActive = value;
                SetSprite(true);
            }
        }

        private void setFlag()
        {
            if (Scene is OriginalLevelSceneRoot level) level.Session.SetFlag(FlagKey, flagActive);
        }

        private bool getFlag() => Scene is OriginalLevelSceneRoot level && level.Session.GetFlag(FlagKey);
    }
}
