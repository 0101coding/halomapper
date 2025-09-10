using System;
using System.Linq;
using System.Linq.Expressions;
using HaloMapper.Queryable;

namespace HaloMapper.Extensions
{
    public static class QueryableExtensions
    {
        public static IQueryable<TDestination> ProjectTo<TDestination>(
            this IQueryable source, 
            MapperConfiguration configuration)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var sourceType = source.ElementType;
            var destinationType = typeof(TDestination);

            // Create projection expression
            var projectionExpression = CreateProjectionExpressionForQueryable(
                sourceType, destinationType, configuration);

            // Apply projection to queryable
            var selectMethod = typeof(System.Linq.Queryable)
                .GetMethods()
                .First(m => m.Name == "Select" && 
                           m.GetParameters().Length == 2 &&
                           m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>))
                .MakeGenericMethod(sourceType, destinationType);

            var result = selectMethod.Invoke(null, new object[] { source, projectionExpression });
            return (IQueryable<TDestination>)result!;
        }

        public static IQueryable<TDestination> ProjectTo<TSource, TDestination>(
            this IQueryable<TSource> source,
            MapperConfiguration configuration)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            var projectionExpression = ProjectionExpression.CreateProjectionExpression<TSource, TDestination>(configuration);
            return source.Select(projectionExpression);
        }

        public static IQueryable<TDestination> ProjectTo<TSource, TDestination>(
            this IQueryable<TSource> source,
            MapperConfiguration configuration,
            params Expression<Func<TDestination, object>>[] membersToExpand)
        {
            // For now, ignore membersToExpand and use basic projection
            // In a full implementation, this would optimize the projection to include only requested members
            return source.ProjectTo<TSource, TDestination>(configuration);
        }

        private static LambdaExpression CreateProjectionExpressionForQueryable(
            Type sourceType, 
            Type destinationType, 
            MapperConfiguration configuration)
        {
            var method = typeof(ProjectionExpression)
                .GetMethod("CreateProjectionExpression", new[] { typeof(MapperConfiguration) })!
                .MakeGenericMethod(sourceType, destinationType);

            return (LambdaExpression)method.Invoke(null, new object[] { configuration })!;
        }
    }
}

