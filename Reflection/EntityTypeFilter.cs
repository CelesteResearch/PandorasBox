using System;
using System.Collections.Generic;
using Celeste.Core.Scheduling;
using Celeste.Everest;
using Celeste.Everest.Reflection;
using Celeste.Scene.Tracking;

namespace Celeste.Mods.PandorasBox.Reflection
{
    public static class EntityTypeFilter
    {
        public static HashSet<Type> GetTypes(ISourceEngineContext engine, string names, char separator = ',')
        {
            var types = new HashSet<Type>();
            if (string.IsNullOrEmpty(names)) return types;
            var assembly = ((ISourceEverestReflectionEnvironment)engine).Reflection.GetFakeEntryAssembly();
            foreach (string name in names.Split(separator)) types.Add(assembly.GetType(name));
            return types;
        }

        public static List<object> FindTargets(SceneEntityRegistry registry, HashSet<Type> targets, bool useTracked)
        {
            var entities = new List<object>();
            if (useTracked)
            {
                foreach (Type type in targets)
                    if (type != null && registry.TryGetTrackedEntities(type, out var tracked))
                        entities.AddRange(tracked);
            }
            else
            {
                foreach (object entity in registry.Entities)
                    if (targets.Contains(entity.GetType()))
                        entities.Add(entity);
            }

            return entities;
        }
    }
}
