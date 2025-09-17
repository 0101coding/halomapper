using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace HaloMapper
{
    public class CompiledMapPlan<TSource, TDestination> : IMapPlan
    {
        private readonly Func<TSource, TDestination, Mapper, TDestination> _compiledMapper;
        private readonly Type _sourceType = typeof(TSource);
        private readonly Type _destinationType = typeof(TDestination);

        public CompiledMapPlan(
            List<ReflectionMapPlan.MemberPlan> memberPlans,
            Func<object>? constructor,
            Action<object, object>? beforeMap,
            Action<object, object>? afterMap)
        {
            _compiledMapper = BuildCompiledExpression(memberPlans, constructor, beforeMap, afterMap);
        }

        public object Map(object source, object? destination, Mapper mapper)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Check for recursion
            var recursionKey = $"{_sourceType.FullName}->{_destinationType.FullName}";
            var context = mapper.GetMappingContext();

            if (context.IsInRecursion(recursionKey))
            {
                // Return default instance to break recursion
                return destination ?? default(TDestination)!;
            }

            context.EnterMapping(recursionKey);
            try
            {
                var typedSource = (TSource)source;
                var typedDestination = destination != null ? (TDestination)destination : default(TDestination)!;

                return _compiledMapper(typedSource, typedDestination, mapper)!;
            }
            finally
            {
                context.ExitMapping(recursionKey);
            }
        }

        private Func<TSource, TDestination, Mapper, TDestination> BuildCompiledExpression(
            List<ReflectionMapPlan.MemberPlan> memberPlans,
            Func<object>? constructor,
            Action<object, object>? beforeMap,
            Action<object, object>? afterMap)
        {
            var sourceParam = Expression.Parameter(typeof(TSource), "source");
            var destinationParam = Expression.Parameter(typeof(TDestination), "destination");
            var mapperParam = Expression.Parameter(typeof(Mapper), "mapper");

            var expressions = new List<Expression>();

            // Handle constructor
            Expression destinationVar;
            if (constructor != null)
            {
                var constructorCall = Expression.Call(Expression.Constant(constructor), constructor.GetType().GetMethod("Invoke")!);
                destinationVar = Expression.Variable(typeof(TDestination), "dest");
                expressions.Add(Expression.Assign(destinationVar, Expression.Convert(constructorCall, typeof(TDestination))));
            }
            else
            {
                destinationVar = Expression.Variable(typeof(TDestination), "dest");
                var nullCheck = Expression.Equal(destinationParam, Expression.Constant(null, typeof(TDestination)));
                var newInstance = Expression.New(typeof(TDestination));
                var assignment = Expression.Condition(nullCheck, newInstance, destinationParam);
                expressions.Add(Expression.Assign(destinationVar, assignment));
            }

            // Handle before map
            if (beforeMap != null)
            {
                var beforeMapCall = Expression.Call(
                    Expression.Constant(beforeMap),
                    beforeMap.GetType().GetMethod("Invoke")!,
                    Expression.Convert(sourceParam, typeof(object)),
                    Expression.Convert(destinationVar, typeof(object))
                );
                expressions.Add(beforeMapCall);
            }

            // Handle member mappings
            foreach (var memberPlan in memberPlans)
            {
                if (memberPlan.Ignore) continue;

                var memberExpression = BuildMemberMappingExpression(
                    sourceParam, destinationVar, mapperParam, memberPlan);
                
                if (memberExpression != null)
                    expressions.Add(memberExpression);
            }

            // Handle after map
            if (afterMap != null)
            {
                var afterMapCall = Expression.Call(
                    Expression.Constant(afterMap),
                    afterMap.GetType().GetMethod("Invoke")!,
                    Expression.Convert(sourceParam, typeof(object)),
                    Expression.Convert(destinationVar, typeof(object))
                );
                expressions.Add(afterMapCall);
            }

            // Return destination
            expressions.Add(destinationVar);

            var block = Expression.Block(new[] { (ParameterExpression)destinationVar }, expressions);
            var lambda = Expression.Lambda<Func<TSource, TDestination, Mapper, TDestination>>(
                block, sourceParam, destinationParam, mapperParam);

            return lambda.Compile();
        }

        private Expression? BuildMemberMappingExpression(
            ParameterExpression sourceParam,
            Expression destinationVar,
            ParameterExpression mapperParam,
            ReflectionMapPlan.MemberPlan memberPlan)
        {
            if (memberPlan.DestSetter == null) return null;

            Expression? valueExpression = null;

            // Handle custom resolver
            if (memberPlan.Resolver != null)
            {
                valueExpression = Expression.Call(
                    Expression.Constant(memberPlan.Resolver),
                    memberPlan.Resolver.GetType().GetMethod("Invoke")!,
                    Expression.Convert(sourceParam, typeof(object)),
                    Expression.Convert(destinationVar, typeof(object))
                );
            }
            // Handle source getter
            else if (memberPlan.SourceGetter != null)
            {
                valueExpression = Expression.Call(
                    Expression.Constant(memberPlan.SourceGetter),
                    memberPlan.SourceGetter.GetType().GetMethod("Invoke")!,
                    Expression.Convert(sourceParam, typeof(object))
                );
            }

            if (valueExpression == null) return null;

            // Handle condition
            if (memberPlan.Condition != null)
            {
                var conditionExpression = Expression.Call(
                    Expression.Constant(memberPlan.Condition),
                    memberPlan.Condition.GetType().GetMethod("Invoke")!,
                    Expression.Convert(sourceParam, typeof(object)),
                    Expression.Convert(destinationVar, typeof(object))
                );

                var setterCall = Expression.Call(
                    Expression.Constant(memberPlan.DestSetter),
                    memberPlan.DestSetter.GetType().GetMethod("Invoke")!,
                    Expression.Convert(destinationVar, typeof(object)),
                    valueExpression
                );

                return Expression.IfThen(conditionExpression, setterCall);
            }

            // Simple setter call
            return Expression.Call(
                Expression.Constant(memberPlan.DestSetter),
                memberPlan.DestSetter.GetType().GetMethod("Invoke")!,
                Expression.Convert(destinationVar, typeof(object)),
                valueExpression
            );
        }
    }

    public static class CompiledMapPlanFactory
    {
        public static IMapPlan CreateCompiledPlan(
            Type sourceType,
            Type destinationType,
            List<ReflectionMapPlan.MemberPlan> memberPlans,
            Func<object>? constructor,
            Action<object, object>? beforeMap,
            Action<object, object>? afterMap)
        {
            var planType = typeof(CompiledMapPlan<,>).MakeGenericType(sourceType, destinationType);
            return (IMapPlan)Activator.CreateInstance(planType, memberPlans, constructor, beforeMap, afterMap)!;
        }
    }
}