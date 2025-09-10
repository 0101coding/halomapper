using System;
using System.Collections.Generic;

namespace HaloMapper
{
    public class Mapper : IMapper
    {
        public MapperConfiguration Configuration { get; }

        public Mapper(MapperConfiguration config)
        {
            Configuration = config;
        }

        public TDestination Map<TSource, TDestination>(TSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (!Configuration.TryGetPlan(typeof(TSource), typeof(TDestination), out var plan))
            {
                Configuration.EnsurePlan<TSource, TDestination>();
                plan = Configuration._plans[(typeof(TSource), typeof(TDestination))];
            }

            return (TDestination)plan.Map(source!, null, this);
        }

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (!Configuration.TryGetPlan(typeof(TSource), typeof(TDestination), out var plan))
            {
                Configuration.EnsurePlan<TSource, TDestination>();
                plan = Configuration._plans[(typeof(TSource), typeof(TDestination))];
            }

            return (TDestination)plan.Map(source!, destination!, this);
        }

        public IEnumerable<TDestination> MapCollection<TSource, TDestination>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            foreach (var item in source)
            {
                yield return Map<TSource, TDestination>(item);
            }
        }
    }
}