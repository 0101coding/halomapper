using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HaloMapper.Validation
{
    public class ConfigurationValidator
    {
        private readonly MapperConfiguration _configuration;
        private readonly HashSet<(Type, Type)> _validatedTypes = new();

        public ConfigurationValidator(MapperConfiguration configuration)
        {
            _configuration = configuration;
        }

        public ValidationResult ValidateAll()
        {
            var result = new ValidationResult();
            _validatedTypes.Clear();

            // Validate all registered mappings
            foreach (var ((sourceType, destType), _) in _configuration._plans)
            {
                var mappingResult = ValidateMapping(sourceType, destType);
                result.Errors.AddRange(mappingResult.Errors);
                result.Warnings.AddRange(mappingResult.Warnings);
            }

            // Check for circular references
            CheckForCircularReferences(result);

            return result;
        }

        public ValidationResult ValidateMapping<TSource, TDestination>()
        {
            return ValidateMapping(typeof(TSource), typeof(TDestination));
        }

        public ValidationResult ValidateMapping(Type sourceType, Type destinationType)
        {
            var result = new ValidationResult();
            
            if (_validatedTypes.Contains((sourceType, destinationType)))
                return result; // Already validated

            _validatedTypes.Add((sourceType, destinationType));

            // Check if mapping exists
            if (!_configuration.TryGetPlan(sourceType, destinationType, out var plan))
            {
                result.AddError($"No mapping configuration found", sourceType, destinationType);
                return result;
            }

            // Validate member mappings
            ValidateMemberMappings(sourceType, destinationType, result);

            return result;
        }

        private void ValidateMemberMappings(Type sourceType, Type destinationType, ValidationResult result)
        {
            var sourceProperties = GetMappableProperties(sourceType);
            var destProperties = GetMappableProperties(destinationType);

            foreach (var destProp in destProperties)
            {
                // Check if destination property can be mapped
                if (!CanMapToProperty(sourceType, destProp, sourceProperties))
                {
                    // Check if there's explicit configuration for this member
                    if (!HasExplicitMemberConfiguration(sourceType, destinationType, destProp.Name))
                    {
                        // Complex types that can't be mapped should be errors, not warnings
                        if (IsComplexType(destProp.PropertyType))
                        {
                            result.AddError(
                                $"Cannot map complex destination member '{destProp.Name}' of type '{destProp.PropertyType.Name}' - no mapping configuration found", 
                                sourceType, 
                                destinationType, 
                                destProp.Name);
                        }
                        else
                        {
                            result.AddWarning(
                                $"Unmapped destination member '{destProp.Name}'", 
                                sourceType, 
                                destinationType, 
                                destProp.Name);
                        }
                    }
                }
                else
                {
                    // Validate type compatibility
                    var sourceProp = sourceProperties.FirstOrDefault(p => p.Name == destProp.Name);
                    if (sourceProp != null)
                    {
                        ValidateTypeCompatibility(sourceProp, destProp, result, sourceType, destinationType);
                    }
                }
            }

            // Check for unmapped source properties (informational)
            foreach (var sourceProp in sourceProperties)
            {
                if (!destProperties.Any(d => d.Name == sourceProp.Name))
                {
                    result.AddWarning(
                        $"Source member '{sourceProp.Name}' is not mapped to any destination member", 
                        sourceType, 
                        destinationType, 
                        sourceProp.Name);
                }
            }
        }

        private bool CanMapToProperty(Type sourceType, PropertyInfo destProp, PropertyInfo[] sourceProperties)
        {
            // Direct property match
            if (sourceProperties.Any(p => p.Name == destProp.Name))
                return true;

            // Check for flattening possibilities
            if (CanFlattenToProperty(sourceType, destProp.Name, sourceProperties))
                return true;

            return false;
        }

        private bool CanFlattenToProperty(Type sourceType, string destPropertyName, PropertyInfo[] sourceProperties)
        {
            // Simple flattening check: look for nested properties
            foreach (var sourceProp in sourceProperties)
            {
                if (sourceProp.PropertyType.IsClass && sourceProp.PropertyType != typeof(string))
                {
                    var nestedProperties = GetMappableProperties(sourceProp.PropertyType);
                    var expectedNestedName = destPropertyName.StartsWith(sourceProp.Name) 
                        ? destPropertyName.Substring(sourceProp.Name.Length)
                        : null;

                    if (!string.IsNullOrEmpty(expectedNestedName) && 
                        nestedProperties.Any(p => p.Name == expectedNestedName))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasExplicitMemberConfiguration(Type sourceType, Type destinationType, string memberName)
        {
            // This would need to be implemented based on how member configurations are stored
            // For now, assume no explicit configuration tracking
            return false;
        }

        private void ValidateTypeCompatibility(PropertyInfo sourceProp, PropertyInfo destProp, 
            ValidationResult result, Type sourceType, Type destinationType)
        {
            var sourcePropertyType = sourceProp.PropertyType;
            var destPropertyType = destProp.PropertyType;

            // Same type or assignable
            if (sourcePropertyType == destPropertyType || destPropertyType.IsAssignableFrom(sourcePropertyType))
                return;

            // Check for nullable compatibility
            var sourceUnderlyingType = Nullable.GetUnderlyingType(sourcePropertyType);
            var destUnderlyingType = Nullable.GetUnderlyingType(destPropertyType);

            if (sourceUnderlyingType != null || destUnderlyingType != null)
            {
                var actualSourceType = sourceUnderlyingType ?? sourcePropertyType;
                var actualDestType = destUnderlyingType ?? destPropertyType;
                
                if (actualSourceType == actualDestType || actualDestType.IsAssignableFrom(actualSourceType))
                    return;
            }

            // Check for type converter
            if (_configuration._typeConverters.TryGetConverter(sourcePropertyType, destPropertyType, out _))
                return;

            // Check for nested mapping
            if (_configuration.TryGetPlan(sourcePropertyType, destPropertyType, out _))
                return;

            // Check if types can be converted using System.Convert
            if (CanConvertUsingSystemConvert(sourcePropertyType, destPropertyType))
                return;

            result.AddError(
                $"Cannot map property '{sourceProp.Name}' of type {sourcePropertyType.Name} to property '{destProp.Name}' of type {destPropertyType.Name}",
                sourceType,
                destinationType,
                destProp.Name);
        }

        private bool CanConvertUsingSystemConvert(Type sourceType, Type destType)
        {
            try
            {
                // Check if System.Convert can handle the conversion
                return (sourceType.IsPrimitive || sourceType == typeof(string) || sourceType == typeof(DateTime) || sourceType == typeof(decimal)) &&
                       (destType.IsPrimitive || destType == typeof(string) || destType == typeof(DateTime) || destType == typeof(decimal));
            }
            catch
            {
                return false;
            }
        }

        private void CheckForCircularReferences(ValidationResult result)
        {
            var visited = new HashSet<(Type, Type)>();
            var recursionStack = new HashSet<(Type, Type)>();

            foreach (var (sourceType, destType) in _configuration._plans.Keys)
            {
                if (HasCircularReference(sourceType, destType, visited, recursionStack))
                {
                    result.AddError($"Circular reference detected in mapping chain", sourceType, destType);
                }
            }
        }

        private bool HasCircularReference(Type sourceType, Type destType, 
            HashSet<(Type, Type)> visited, HashSet<(Type, Type)> recursionStack)
        {
            var key = (sourceType, destType);
            
            if (recursionStack.Contains(key))
                return true;

            if (visited.Contains(key))
                return false;

            visited.Add(key);
            recursionStack.Add(key);

            // Check nested mappings
            var properties = GetMappableProperties(destType);
            foreach (var prop in properties)
            {
                if (prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
                {
                    var sourceProp = GetMappableProperties(sourceType).FirstOrDefault(p => p.Name == prop.Name);
                    if (sourceProp != null && _configuration.TryGetPlan(sourceProp.PropertyType, prop.PropertyType, out _))
                    {
                        if (HasCircularReference(sourceProp.PropertyType, prop.PropertyType, visited, recursionStack))
                        {
                            recursionStack.Remove(key);
                            return true;
                        }
                    }
                }
            }

            recursionStack.Remove(key);
            return false;
        }

        private PropertyInfo[] GetMappableProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                      .Where(p => p.CanRead && (p.CanWrite || p.PropertyType.IsClass))
                      .ToArray();
        }

        private bool IsComplexType(Type type)
        {
            // Treat custom classes (not primitives, not string, not DateTime, not decimal, not enums) as complex
            return type.IsClass && 
                   type != typeof(string) && 
                   type != typeof(DateTime) &&
                   type != typeof(decimal) &&
                   !type.IsPrimitive &&
                   !type.IsEnum;
        }
    }
}