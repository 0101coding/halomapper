using System;
using System.Collections.Generic;
using System.Threading;

namespace HaloMapper
{
    /// <summary>
    /// Provides object mapping functionality using configured mapping plans.
    /// </summary>
    public class Mapper : IMapper
    {
    /// <summary>
    /// Gets the mapping configuration used by this mapper.
    /// </summary>
    public MapperConfiguration Configuration { get; }

    private readonly ThreadLocal<MappingContext> _context = new(() => new MappingContext());

        /// <summary>
        /// Initializes a new instance of the <see cref="Mapper"/> class with the specified configuration.
        /// </summary>
        /// <param name="config">The mapping configuration.</param>
        public Mapper(MapperConfiguration config)
        {
            Configuration = config;
        }

        /// <summary>
        /// Maps the source object to a new destination object of type <typeparamref name="TDestination"/>.
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <param name="source">The source object to map.</param>
        /// <returns>The mapped destination object.</returns>
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

        /// <summary>
        /// Maps the source object to the provided destination object of type <typeparamref name="TDestination"/>.
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <param name="source">The source object to map.</param>
        /// <param name="destination">The destination object to populate.</param>
        /// <returns>The mapped destination object.</returns>
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

    /// <summary>
    /// Maps a collection of source objects to a collection of destination objects of type <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TDestination">Destination type.</typeparam>
    /// <param name="source">The collection of source objects to map.</param>
    /// <returns>A collection of mapped destination objects.</returns>
    public IEnumerable<TDestination> MapCollection<TSource, TDestination>(IEnumerable<TSource> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            foreach (var item in source)
            {
                yield return Map<TSource, TDestination>(item);
            }
        }

        /// <summary>
        /// Maps an object to the specified destination type.
        /// </summary>
        /// <param name="source">The source object to map.</param>
        /// <param name="destinationType">The destination type.</param>
        /// <returns>The mapped destination object.</returns>
        public object Map(object source, Type destinationType)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var sourceType = source.GetType();

            if (!Configuration.TryGetPlan(sourceType, destinationType, out var plan))
            {
                // Try to create plan using reflection
                var ensureMethod = typeof(MapperConfiguration).GetMethod("EnsurePlan")!.MakeGenericMethod(sourceType, destinationType);
                ensureMethod.Invoke(Configuration, null);
                plan = Configuration._plans[(sourceType, destinationType)];
            }

            return plan.Map(source, null, this);
        }

        /// <summary>
        /// Gets the mapping context for the current thread.
        /// </summary>
        /// <returns>The current mapping context.</returns>
        internal MappingContext GetMappingContext()
        {
            return _context.Value!;
        }
    }
}