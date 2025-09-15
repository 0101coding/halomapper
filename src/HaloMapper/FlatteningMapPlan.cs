using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HaloMapper
{
    public class FlatteningMapPlan : IMapPlan
    {
        private readonly Type _sourceType;
        private readonly Type _destinationType;
        private readonly List<FlatteningMemberPlan> _memberPlans;
        private readonly Func<object>? _constructor;
        private readonly Action<object, object>? _beforeMap;
        private readonly Action<object, object>? _afterMap;

        public FlatteningMapPlan(
            Type sourceType, 
            Type destinationType,
            List<FlatteningMemberPlan> memberPlans,
            Func<object>? constructor,
            Action<object, object>? beforeMap,
            Action<object, object>? afterMap)
        {
            _sourceType = sourceType;
            _destinationType = destinationType;
            _memberPlans = memberPlans;
            _constructor = constructor;
            _beforeMap = beforeMap;
            _afterMap = afterMap;
        }

        public object Map(object source, object? destination, Mapper mapper)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Check for recursion - use a simple depth counter in the mapper context
            var recursionKey = $"{source.GetType().FullName}->{_destinationType.FullName}";
            var context = mapper.GetMappingContext();

            if (context.IsInRecursion(recursionKey))
            {
                // Return default instance to break recursion
                return destination ?? _constructor?.Invoke() ?? Activator.CreateInstance(_destinationType)!;
            }

            context.EnterMapping(recursionKey);
            try
            {
                var dest = destination ?? _constructor?.Invoke() ?? Activator.CreateInstance(_destinationType)!;

                _beforeMap?.Invoke(source, dest);

                foreach (var plan in _memberPlans)
                {
                    if (plan.Ignore) continue;

                    var value = plan.GetSourceValue(source, mapper);

                    if (value == null && plan.NullSubstitute != null)
                        value = plan.NullSubstitute;

                    if (plan.Condition != null && !plan.Condition(source, dest))
                        continue;

                    plan.SetDestinationValue(dest, value, mapper);
                }

                _afterMap?.Invoke(source, dest);
                return dest;
            }
            finally
            {
                context.ExitMapping(recursionKey);
            }
        }

        public class FlatteningMemberPlan
        {
            public string DestinationName { get; set; } = default!;
            public Type? DestinationType { get; set; }
            public PropertyPath? SourcePath { get; set; }
            public Func<object, object?>? SourceGetter { get; set; }
            public Action<object, object?>? DestSetter { get; set; }
            public bool Ignore { get; set; }
            public Func<object, object, bool>? Condition { get; set; }
            public object? NullSubstitute { get; set; }
            public Func<object, object, object?>? Resolver { get; set; }

            public object? GetSourceValue(object source, Mapper mapper)
            {
                if (Resolver != null)
                    return Resolver(source, source); // placeholder destination

                if (SourceGetter != null)
                    return SourceGetter(source);

                return SourcePath?.GetValue(source, mapper);
            }

            public void SetDestinationValue(object destination, object? value, Mapper mapper)
            {
                if (DestSetter != null && value != null && DestinationType != null)
                {
                    // Handle type conversion if needed
                    if (value.GetType() != DestinationType)
                    {
                        var convertedValue = mapper.Configuration._typeConverters.Convert(value, value.GetType(), DestinationType);
                        if (convertedValue != null)
                        {
                            value = convertedValue;
                        }
                        else if (mapper.Configuration.TryGetPlan(value.GetType(), DestinationType, out var plan))
                        {
                            value = plan.Map(value, null, mapper);
                        }
                    }

                    DestSetter(destination, value);
                }
                else if (DestSetter != null)
                {
                    DestSetter(destination, value);
                }
            }
        }
    }

    public class PropertyPath
    {
        public List<PropertyInfo> Properties { get; }
        public Type FinalType => Properties.LastOrDefault()?.PropertyType ?? typeof(object);

        public PropertyPath(List<PropertyInfo> properties)
        {
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public static PropertyPath Parse(Type sourceType, string path)
        {
            var properties = new List<PropertyInfo>();
            var currentType = sourceType;
            var parts = path.Split('.');

            foreach (var part in parts)
            {
                var prop = currentType.GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null)
                    throw new ArgumentException($"Property '{part}' not found on type '{currentType.Name}' in path '{path}'");

                properties.Add(prop);
                currentType = prop.PropertyType;
            }

            return new PropertyPath(properties);
        }

        public object? GetValue(object source, Mapper mapper)
        {
            object? current = source;

            foreach (var prop in Properties)
            {
                if (current == null) return null;

                current = prop.GetValue(current);
            }

            return current;
        }

        public override string ToString()
        {
            return string.Join(".", Properties.Select(p => p.Name));
        }
    }

    public static class FlatteningMapPlanFactory
    {
        public static FlatteningMapPlan CreateFlatteningPlan(
            Type sourceType,
            Type destinationType,
            Func<object>? constructor,
            Action<object, object>? beforeMap,
            Action<object, object>? afterMap,
            Dictionary<string, ReflectionMapPlan.MemberPlan>? explicitMemberPlans = null)
        {
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>();
            var destProperties = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                               .Where(p => p.CanWrite)
                                               .ToArray();

            foreach (var destProp in destProperties)
            {
                // Check for explicit configuration first
                if (explicitMemberPlans?.ContainsKey(destProp.Name) == true)
                {
                    var explicitPlan = explicitMemberPlans[destProp.Name];
                    memberPlans.Add(new FlatteningMapPlan.FlatteningMemberPlan
                    {
                        DestinationName = destProp.Name,
                        DestinationType = destProp.PropertyType,
                        DestSetter = (dest, val) => destProp.SetValue(dest, val),
                        Ignore = explicitPlan.Ignore,
                        Condition = explicitPlan.Condition,
                        NullSubstitute = explicitPlan.NullSubstitute,
                        Resolver = explicitPlan.Resolver,
                        SourceGetter = explicitPlan.SourceGetter,
                        SourcePath = null // Don't try to parse path for explicitly configured members
                    });
                    continue;
                }

                // Try to find a matching source path
                var sourcePath = FindSourcePath(sourceType, destProp.Name);
                if (sourcePath != null)
                {
                    memberPlans.Add(new FlatteningMapPlan.FlatteningMemberPlan
                    {
                        DestinationName = destProp.Name,
                        DestinationType = destProp.PropertyType,
                        SourcePath = sourcePath,
                        DestSetter = (dest, val) => destProp.SetValue(dest, val)
                    });
                }
            }

            return new FlatteningMapPlan(sourceType, destinationType, memberPlans, constructor, beforeMap, afterMap);
        }

        private static PropertyPath? FindSourcePath(Type sourceType, string destinationPropertyName)
        {
            // First, try direct property match
            var directProp = sourceType.GetProperty(destinationPropertyName, BindingFlags.Public | BindingFlags.Instance);
            if (directProp != null)
            {
                return new PropertyPath(new List<PropertyInfo> { directProp });
            }

            // Try flattening scenarios
            var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                            .Where(p => p.CanRead && p.PropertyType.IsClass && p.PropertyType != typeof(string))
                                            .ToArray();

            foreach (var sourceProp in sourceProperties)
            {
                if (destinationPropertyName.StartsWith(sourceProp.Name))
                {
                    var remainingName = destinationPropertyName.Substring(sourceProp.Name.Length);
                    if (!string.IsNullOrEmpty(remainingName))
                    {
                        // Try to find the nested property
                        var nestedPath = FindSourcePath(sourceProp.PropertyType, remainingName);
                        if (nestedPath != null)
                        {
                            var combinedPath = new List<PropertyInfo> { sourceProp };
                            combinedPath.AddRange(nestedPath.Properties);
                            return new PropertyPath(combinedPath);
                        }
                    }
                }
            }

            return null;
        }
    }
}