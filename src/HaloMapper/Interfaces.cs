        using System;
using System.Collections.Generic;

namespace HaloMapper
{
    /// <summary>
    /// Defines methods for object mapping operations.
    /// </summary>
    public interface IMapper
    {
    /// <summary>
    /// Maps the source object to a new destination object of type <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TDestination">Destination type.</typeparam>
    /// <param name="source">The source object to map.</param>
    /// <returns>The mapped destination object.</returns>
    TDestination Map<TSource, TDestination>(TSource source);
    /// <summary>
    /// Maps the source object to the provided destination object of type <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TDestination">Destination type.</typeparam>
    /// <param name="source">The source object to map.</param>
    /// <param name="destination">The destination object to populate.</param>
    /// <returns>The mapped destination object.</returns>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
    /// <summary>
    /// Maps a collection of source objects to a collection of destination objects of type <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TDestination">Destination type.</typeparam>
    /// <param name="source">The collection of source objects to map.</param>
    /// <returns>A collection of mapped destination objects.</returns>
    IEnumerable<TDestination> MapCollection<TSource, TDestination>(IEnumerable<TSource> source);
    }

    /// <summary>
    /// Defines a type converter for converting from <typeparamref name="TSource"/> to <typeparamref name="TDestination"/>.
    /// </summary>
    public interface ITypeConverter<TSource, TDestination>
    {
    /// <summary>
    /// Converts the source object to the destination type.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <returns>The converted destination object.</returns>
    TDestination Convert(TSource source);
    }

    /// <summary>
    /// Defines a resolver for custom member value mapping.
    /// </summary>
    public interface IMemberValueResolver<TSource, TDestination, TMember>
    {
    /// <summary>
    /// Resolves the value for a member during mapping.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="destination">The destination object.</param>
    /// <param name="destMember">The current value of the destination member.</param>
    /// <returns>The resolved member value.</returns>
    TMember Resolve(TSource source, TDestination destination, TMember destMember);
    }
    /// <summary>
    /// Defines methods for configuring member mappings.
    /// </summary>
    public interface IMappingExpression<TSource, TDestination>
    {
    /// <summary>
    /// Configures a mapping for a specific member of the destination type.
    /// </summary>
    /// <typeparam name="TMember">The member type.</typeparam>
    /// <param name="destinationMember">The name of the destination member.</param>
    /// <param name="mapFunc">The mapping function.</param>
    void ForMember<TMember>(string destinationMember, Func<TSource, TMember> mapFunc);
    }
    /// <summary>
    /// Defines a mapping plan for mapping objects.
    /// </summary>
    public interface IMapPlan
    {
    /// <summary>
    /// Maps the source object to the destination object using the provided mapper.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="destination">The destination object (optional).</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <returns>The mapped destination object.</returns>
    object Map(object source, object? destination, Mapper mapper);
    }

    /// <summary>
    /// Defines options for configuring member mappings.
    /// </summary>
    public interface IMemberOptions<TSource, TDestination, TMember>
    {
    /// <summary>
    /// Configures the member to be mapped from the specified function.
    /// </summary>
    /// <param name="mapFunc">The mapping function.</param>
    void MapFrom(Func<TSource, TMember> mapFunc);
    /// <summary>
    /// Ignores the member during mapping.
    /// </summary>
    void Ignore();
    /// <summary>
    /// Configures a condition for mapping the member.
    /// </summary>
    /// <param name="predicate">The condition predicate.</param>
    void Condition(Func<TSource, TDestination, bool> predicate);
    /// <summary>
    /// Configures a substitute value to use if the source value is null.
    /// </summary>
    /// <param name="value">The substitute value.</param>
    void NullSubstitute(TMember value);
    /// <summary>
    /// Configures a custom resolver for the member.
    /// </summary>
    /// <param name="resolver">The resolver function.</param>
    void ResolveUsing(Func<TSource, TDestination, object?> resolver);
    }
}