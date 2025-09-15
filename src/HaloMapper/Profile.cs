namespace HaloMapper
{
    /// <summary>
    /// Base class for defining mapping profiles in HaloMapper.
    /// Inherit from this class and implement <see cref="Configure"/> to define your object mappings.
    /// </summary>
    public abstract class Profile
    {
    /// <summary>
    /// Internal list of mapping configuration actions to be applied to the <see cref="MapperConfiguration"/>.
    /// </summary>
    protected internal List<Action<MapperConfiguration>> Actions { get; } = new();

        /// <summary>
        /// Defines a mapping between <typeparamref name="TSource"/> and <typeparamref name="TDestination"/>.
        /// Optionally accepts a configuration action for customizing the mapping.
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <param name="cfg">Optional configuration for the mapping expression.</param>
        protected void CreateMap<TSource, TDestination>(Action<MappingExpression<TSource, TDestination>>? cfg = null)
        {
            Actions.Add(c => c.CreateMap(cfg));
        }

    /// <summary>
    /// Implement this method to define your object mappings using <see cref="CreateMap"/>.
    /// </summary>
    public abstract void Configure();
    }
}