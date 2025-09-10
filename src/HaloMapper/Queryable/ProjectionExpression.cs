using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace HaloMapper.Queryable
{
    public static class ProjectionExpression
    {
        public static Expression<Func<TSource, TDestination>> CreateProjectionExpression<TSource, TDestination>(
            MapperConfiguration configuration)
        {
            return CreateProjectionExpression<TSource, TDestination>(configuration, new Dictionary<Type, ParameterExpression>());
        }

        private static Expression<Func<TSource, TDestination>> CreateProjectionExpression<TSource, TDestination>(
            MapperConfiguration configuration, Dictionary<Type, ParameterExpression> parameterMap)
        {
            var sourceParam = Expression.Parameter(typeof(TSource), "src");
            parameterMap[typeof(TSource)] = sourceParam;

            var body = CreateProjectionBody(sourceParam, typeof(TSource), typeof(TDestination), configuration, parameterMap);
            
            return Expression.Lambda<Func<TSource, TDestination>>(body, sourceParam);
        }

        private static Expression CreateProjectionBody(
            Expression sourceExpression,
            Type sourceType, 
            Type destinationType, 
            MapperConfiguration configuration,
            Dictionary<Type, ParameterExpression> parameterMap)
        {
            // Handle primitive types and direct assignments
            if (destinationType.IsAssignableFrom(sourceType))
            {
                return Expression.Convert(sourceExpression, destinationType);
            }

            // Handle nullable types
            var destUnderlyingType = Nullable.GetUnderlyingType(destinationType);
            if (destUnderlyingType != null)
            {
                var innerExpression = CreateProjectionBody(sourceExpression, sourceType, destUnderlyingType, configuration, parameterMap);
                return Expression.Convert(innerExpression, destinationType);
            }

            // Handle type conversion
            if (configuration._typeConverters.TryGetConverter(sourceType, destinationType, out var converter))
            {
                var convertMethod = converter!.GetType().GetMethod("Convert");
                if (convertMethod != null)
                {
                    return Expression.Call(Expression.Constant(converter), convertMethod, sourceExpression);
                }
            }

            // Handle complex object mapping
            if (destinationType.IsClass && destinationType != typeof(string))
            {
                return CreateObjectProjection(sourceExpression, sourceType, destinationType, configuration, parameterMap);
            }

            // Handle collections
            if (IsCollection(destinationType))
            {
                return CreateCollectionProjection(sourceExpression, sourceType, destinationType, configuration, parameterMap);
            }

            // Default conversion
            return Expression.Convert(sourceExpression, destinationType);
        }

        private static Expression CreateObjectProjection(
            Expression sourceExpression,
            Type sourceType,
            Type destinationType,
            MapperConfiguration configuration,
            Dictionary<Type, ParameterExpression> parameterMap)
        {
            var bindings = new List<MemberBinding>();
            var destProperties = destinationType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                              .Where(p => p.CanWrite)
                                              .ToArray();

            foreach (var destProp in destProperties)
            {
                var binding = CreateMemberBinding(sourceExpression, sourceType, destProp, configuration, parameterMap);
                if (binding != null)
                {
                    bindings.Add(binding);
                }
            }

            // Handle null source
            if (sourceType.IsClass)
            {
                var nullCheck = Expression.Equal(sourceExpression, Expression.Constant(null));
                var memberInit = Expression.MemberInit(Expression.New(destinationType), bindings);
                var defaultValue = Expression.Constant(null, destinationType);
                
                return Expression.Condition(nullCheck, defaultValue, memberInit);
            }

            return Expression.MemberInit(Expression.New(destinationType), bindings);
        }

        private static MemberBinding? CreateMemberBinding(
            Expression sourceExpression,
            Type sourceType,
            PropertyInfo destProperty,
            MapperConfiguration configuration,
            Dictionary<Type, ParameterExpression> parameterMap)
        {
            // Direct property mapping
            var sourceProp = sourceType.GetProperty(destProperty.Name, BindingFlags.Public | BindingFlags.Instance);
            if (sourceProp != null && sourceProp.CanRead)
            {
                var sourcePropertyExpression = Expression.Property(sourceExpression, sourceProp);
                var mappedExpression = CreateProjectionBody(
                    sourcePropertyExpression, 
                    sourceProp.PropertyType, 
                    destProperty.PropertyType, 
                    configuration, 
                    parameterMap);

                return Expression.Bind(destProperty, mappedExpression);
            }

            // Try flattening
            var flattenedBinding = TryCreateFlattenedBinding(sourceExpression, sourceType, destProperty, configuration, parameterMap);
            if (flattenedBinding != null)
            {
                return flattenedBinding;
            }

            // Default value
            var defaultValue = GetDefaultValue(destProperty.PropertyType);
            return Expression.Bind(destProperty, Expression.Constant(defaultValue, destProperty.PropertyType));
        }

        private static MemberBinding? TryCreateFlattenedBinding(
            Expression sourceExpression,
            Type sourceType,
            PropertyInfo destProperty,
            MapperConfiguration configuration,
            Dictionary<Type, ParameterExpression> parameterMap)
        {
            var destPropName = destProperty.Name;
            var sourceProperties = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                            .Where(p => p.CanRead)
                                            .ToArray();

            // Look for nested property patterns
            foreach (var sourceProp in sourceProperties)
            {
                if (destPropName.StartsWith(sourceProp.Name) && sourceProp.PropertyType.IsClass && sourceProp.PropertyType != typeof(string))
                {
                    var remainingName = destPropName.Substring(sourceProp.Name.Length);
                    var nestedProp = sourceProp.PropertyType.GetProperty(remainingName, BindingFlags.Public | BindingFlags.Instance);
                    
                    if (nestedProp != null && nestedProp.CanRead)
                    {
                        var sourcePropertyExpression = Expression.Property(sourceExpression, sourceProp);
                        var nestedPropertyExpression = Expression.Property(sourcePropertyExpression, nestedProp);
                        
                        // Handle null checking for nested properties
                        var nullCheck = Expression.Equal(sourcePropertyExpression, Expression.Constant(null));
                        var defaultValue = GetDefaultValue(destProperty.PropertyType);
                        var conditionalExpression = Expression.Condition(
                            nullCheck,
                            Expression.Constant(defaultValue, destProperty.PropertyType),
                            Expression.Convert(nestedPropertyExpression, destProperty.PropertyType)
                        );

                        return Expression.Bind(destProperty, conditionalExpression);
                    }
                }
            }

            return null;
        }

        private static Expression CreateCollectionProjection(
            Expression sourceExpression,
            Type sourceType,
            Type destinationType,
            MapperConfiguration configuration,
            Dictionary<Type, ParameterExpression> parameterMap)
        {
            var sourceElementType = GetElementType(sourceType);
            var destElementType = GetElementType(destinationType);

            if (sourceElementType != null && destElementType != null)
            {
                // Create selector for individual elements
                var selectorParam = Expression.Parameter(sourceElementType, "item");
                var elementProjection = CreateProjectionBody(
                    selectorParam, 
                    sourceElementType, 
                    destElementType, 
                    configuration, 
                    parameterMap);
                
                var selector = Expression.Lambda(elementProjection, selectorParam);

                // Call Select method
                var selectMethod = typeof(System.Linq.Enumerable)
                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .First(m => m.Name == "Select" && m.GetParameters().Length == 2)
                    .MakeGenericMethod(sourceElementType, destElementType);

                var selectCall = Expression.Call(selectMethod, sourceExpression, selector);

                // Convert to appropriate collection type
                if (destinationType.IsArray)
                {
                    var toArrayMethod = typeof(System.Linq.Enumerable)
                        .GetMethod("ToArray", BindingFlags.Static | BindingFlags.Public)!
                        .MakeGenericMethod(destElementType);
                    
                    return Expression.Call(toArrayMethod, selectCall);
                }
                else if (destinationType.IsGenericType && destinationType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var toListMethod = typeof(System.Linq.Enumerable)
                        .GetMethod("ToList", BindingFlags.Static | BindingFlags.Public)!
                        .MakeGenericMethod(destElementType);
                    
                    return Expression.Call(toListMethod, selectCall);
                }

                return selectCall;
            }

            return sourceExpression;
        }

        private static bool IsCollection(Type type)
        {
            if (type.IsArray) return true;
            if (type.IsGenericType)
            {
                var genericDef = type.GetGenericTypeDefinition();
                return genericDef == typeof(IEnumerable<>) ||
                       genericDef == typeof(ICollection<>) ||
                       genericDef == typeof(IList<>) ||
                       genericDef == typeof(List<>);
            }
            return false;
        }

        private static Type? GetElementType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments();
                if (genericArgs.Length == 1)
                    return genericArgs[0];
            }

            return null;
        }

        private static object? GetDefaultValue(Type type)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type);
            return null;
        }
    }
}