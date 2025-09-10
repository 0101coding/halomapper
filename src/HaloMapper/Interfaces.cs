        using System;
using System.Collections.Generic;

namespace HaloMapper
{
    public interface IMapper
    {
        TDestination Map<TSource, TDestination>(TSource source);
        TDestination Map<TSource, TDestination>(TSource source, TDestination destination);
        IEnumerable<TDestination> MapCollection<TSource, TDestination>(IEnumerable<TSource> source);
    }

    public interface ITypeConverter<TSource, TDestination>
    {
        TDestination Convert(TSource source);
    }

    public interface IMemberValueResolver<TSource, TDestination, TMember>
    {
        TMember Resolve(TSource source, TDestination destination, TMember destMember);
    }
    public interface IMappingExpression<TSource, TDestination>
    {
        void ForMember<TMember>(string destinationMember, Func<TSource, TMember> mapFunc);
    }
    public interface IMapPlan
    {
        object Map(object source, object? destination, Mapper mapper);
    }

    public interface IMemberOptions<TSource, TDestination, TMember>
    {
        void MapFrom(Func<TSource, TMember> mapFunc);
        void Ignore();
        void Condition(Func<TSource, TDestination, bool> predicate);
        void NullSubstitute(TMember value);
        void ResolveUsing(Func<TSource, TDestination, object?> resolver);
    }
}