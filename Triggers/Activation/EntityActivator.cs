using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Startup;
using Celeste.Graphics.Geometry.TwoD.Camera;
using Celeste.Mods.PandorasBox.Reflection;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Triggers.Foundation;

namespace Celeste.Mods.PandorasBox.Triggers.Activation
{
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.EntityActivator")]
    [CustomEntity("pandorasBox/entityActivator")]
    [SceneTracked]
    public sealed class EntityActivator : SourceTriggerEntityState
    {
        private readonly ISourceEngineContext engine;

        public EntityActivationEffect Mode;

        public EntityActivationMode ActivationMode;

        public HashSet<Type> Targets;

        public bool UseTracked;

        public string Flag;

        public float UpdateInterval;

        public bool ChangeCollidable;

        public bool ChangeActive;

        public bool ChangeVisible;

        public bool CacheTargets;

        public bool AffectComponents;

        private bool previousFlagValue = false;

        private bool updateFlagValues = false;

        private List<object> cachedTargets;

        private Vector2 previousCameraPosition;

        private float previousCameraZoom;

        public EntityActivator(EntityData data, Vector2 offset, ISourceEngineContext engine)
            : base(((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset,
                data.Width, data.Height, ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
        {
            Tags = ((IOriginalGameTagsEnvironment)engine).Tags.TransitionUpdate.Value;
            this.engine = engine;
            Mode = EntityAttributeReader.Enum(data.Values, "mode",
                EntityActivationEffect.ActivateInsideDeactivateOutside);
            ActivationMode = EntityAttributeReader.Enum(data.Values, "activationMode", EntityActivationMode.OnEnter);
            Targets = EntityTypeFilter.GetTypes(engine, data.Attr("targets"));
            UseTracked = data.Bool("useTracked", true);
            Flag = data.Attr("flag");
            ChangeCollidable = data.Bool("changeCollision", true);
            ChangeActive = data.Bool("changeActive", true);
            ChangeVisible = data.Bool("changeVisible", true);
            CacheTargets = data.Bool("cacheTargets");
            AffectComponents = data.Bool("affectComponents");
            UpdateInterval = data.Float("updateInterval", -1f);
        }

        public override void OnEnter(RoomPlayerSession player)
        {
            if (ActivationMode == EntityActivationMode.OnEnter)
            {
                UpdateEntities();
            }
        }

        public override void OnStay(RoomPlayerSession player)
        {
            if (ActivationMode == EntityActivationMode.OnStay)
            {
                UpdateEntities();
            }
        }

        public override void OnLeave(RoomPlayerSession player)
        {
            base.OnLeave(player);
            if (ActivationMode == EntityActivationMode.OnLeave)
            {
                UpdateEntities();
            }
        }

        public override void Update()
        {
            if (ActivationMode == EntityActivationMode.OnUpdate && OnInterval())
            {
                UpdateEntities();
            }
            else if (ActivationMode == EntityActivationMode.OnCameraMoved)
            {
                CameraState camera = ((OriginalLevelSceneRoot)Scene).Camera;
                if (Math.Abs(camera.X - previousCameraPosition.X) > 8f ||
                    Math.Abs(camera.Y - previousCameraPosition.Y) > 8f || camera.Zoom != previousCameraZoom)
                {
                    UpdateEntities();
                    previousCameraPosition = camera.Position;
                    previousCameraZoom = camera.Zoom;
                }
            }
            else if (updateFlagValues)
            {
                bool flag = ((OriginalLevelSceneRoot)Scene).Session.GetFlag(Flag);
                if (ActivationMode == EntityActivationMode.OnFlagActive && flag && OnInterval())
                {
                    UpdateEntities();
                }
                else if (ActivationMode == EntityActivationMode.OnFlagInactive && !flag && OnInterval())
                {
                    UpdateEntities();
                }
                else if (ActivationMode == EntityActivationMode.OnFlagActivated && flag && !previousFlagValue)
                {
                    UpdateEntities();
                }
                else if (ActivationMode == EntityActivationMode.OnFlagDeactivated && !flag && previousFlagValue)
                {
                    UpdateEntities();
                }

                previousFlagValue = flag;
            }

            base.Update();
        }

        public override void Awake(object scene)
        {
            if (CacheTargets)
            {
                UpdateTargetCache();
            }

            if (!string.IsNullOrEmpty(Flag))
            {
                previousFlagValue = ((OriginalLevelSceneRoot)Scene)?.Session?.GetFlag(Flag) == true;
                updateFlagValues = true;
            }

            CameraState camera = ((OriginalLevelSceneRoot)Scene).Camera;
            previousCameraPosition = camera.Position;
            previousCameraZoom = camera.Zoom;
            if (ActivationMode == EntityActivationMode.OnAwake)
            {
                UpdateEntities();
            }

            if (ActivationMode == EntityActivationMode.OnCameraMoved)
            {
                ((ISourceScene)scene).SourceScene.OnEndOfFrame += delegate { UpdateEntities(); };
            }

            if (updateFlagValues)
            {
                if (ActivationMode == EntityActivationMode.OnFlagActive && previousFlagValue)
                {
                    UpdateEntities();
                }
                else if (ActivationMode == EntityActivationMode.OnFlagInactive && !previousFlagValue)
                {
                    UpdateEntities();
                }
            }
        }

        public void UpdateEntities()
        {
            switch (Mode)
            {
                case EntityActivationEffect.ActivateInsideDeactivateOutside:
                    ActivateInsideDeactivateOutside();
                    break;
                case EntityActivationEffect.ActivateInside:
                    ActivateInside();
                    break;
                case EntityActivationEffect.DeactivateInside:
                    DeactivateInside();
                    break;
                case EntityActivationEffect.ActivateOutside:
                    ActivateOutside();
                    break;
                case EntityActivationEffect.DeactivateOutside:
                    DeactivateOutside();
                    break;
                case EntityActivationEffect.ActivateOnScreenDeactivateOffScreen:
                    ActivateOnScreenDeactivateOffScreen();
                    break;
                default:
                    ((IEverestLoggerProcessEnvironment)engine).Logger.Log(EverestLogLevel.Warn, "PandorasBox",
                        $"Unsupported entity activator mode: {Mode}");
                    break;
            }
        }

        public void UpdateTargetCache()
        {
            cachedTargets = FindTargetEntities(skipCache: true);
        }

        public List<object> FindTargetEntities(bool skipCache = false)
        {
            if (CacheTargets && !skipCache)
            {
                return cachedTargets;
            }

            return EntityTypeFilter.FindTargets(((ISourceScene)Scene).SourceScene.Entities, Targets, UseTracked);
        }

        public bool OnInterval()
        {
            return UpdateInterval <= 0f || ((OriginalLevelSceneRoot)Scene).OnInterval(UpdateInterval);
        }

        public void UpdateTarget(object target, bool visible, bool active, bool collidable)
        {
            var state = (ISourceMutableEntityState)target;
            if (ChangeCollidable)
            {
                state.Collidable = collidable;
            }

            if (ChangeVisible)
            {
                state.Visible = visible;
            }

            if (ChangeActive)
            {
                state.Active = active;
            }

            if (!AffectComponents || (!ChangeVisible && !ChangeActive))
            {
                return;
            }

            var enumerator = ((ISourceOwnedComponentList)target).OwnedComponents.GetValueEnumerator();
            try
            {
                while (enumerator.MoveNext())
                {
                    SourceComponentState component = enumerator.Current;
                    if (ChangeVisible)
                    {
                        component.Visible = visible;
                    }

                    if (ChangeActive)
                    {
                        component.Active = active;
                    }
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }

        public bool EntityInside(object target)
        {
            var entity = (ISourceEntityCollider)target;
            if (entity.Collider == null)
            {
                return entity.Position.X >= base.Collider.AbsoluteLeft &&
                       entity.Position.X <= base.Collider.AbsoluteRight &&
                       entity.Position.Y >= base.Collider.AbsoluteTop &&
                       entity.Position.Y <= base.Collider.AbsoluteBottom;
            }

            return entity.Collider.Collide(base.Collider);
        }

        public void ActivateInsideDeactivateOutside()
        {
            List<object> list = FindTargetEntities();
            foreach (object item in list)
            {
                if (EntityInside(item))
                {
                    UpdateTarget(item, visible: true, active: true, collidable: true);
                }
                else
                {
                    UpdateTarget(item, visible: false, active: false, collidable: false);
                }
            }
        }

        public void ActivateInside()
        {
            List<object> list = FindTargetEntities();
            foreach (object item in list)
            {
                if (EntityInside(item))
                {
                    UpdateTarget(item, visible: true, active: true, collidable: true);
                }
            }
        }

        public void DeactivateInside()
        {
            List<object> list = FindTargetEntities();
            foreach (object item in list)
            {
                if (EntityInside(item))
                {
                    UpdateTarget(item, visible: false, active: false, collidable: false);
                }
            }
        }

        public void ActivateOutside()
        {
            List<object> list = FindTargetEntities();
            foreach (object item in list)
            {
                if (!EntityInside(item))
                {
                    UpdateTarget(item, visible: true, active: true, collidable: true);
                }
            }
        }

        public void DeactivateOutside()
        {
            List<object> list = FindTargetEntities();
            foreach (object item in list)
            {
                if (!EntityInside(item))
                {
                    UpdateTarget(item, visible: false, active: false, collidable: false);
                }
            }
        }

        private void ActivateOnScreenDeactivateOffScreen()
        {
            ColliderGeometry collider = base.Collider;
            CameraState camera = ((OriginalLevelSceneRoot)Scene).Camera;
            float num = camera.Right - camera.Left;
            float num2 = camera.Bottom - camera.Top;
            base.Collider = new HitboxColliderGeometry(num * 3f, num2 * 3f, camera.Position.X - num - Position.X,
                camera.Position.Y - num2 - Position.Y);
            List<object> list = FindTargetEntities();
            foreach (object item in list)
            {
                if (EntityInside(item))
                {
                    UpdateTarget(item, visible: true, active: true, collidable: true);
                }
                else
                {
                    UpdateTarget(item, visible: false, active: false, collidable: false);
                }
            }

            base.Collider = collider;
        }
    }
}
