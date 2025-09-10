namespace HaloMapper
{
    public abstract class Profile
    {
        internal List<Action<MapperConfiguration>> Actions { get; } = new();

        protected void CreateMap<TSource, TDestination>(Action<MappingExpression<TSource, TDestination>>? cfg = null)
        {
            Actions.Add(c => c.CreateMap(cfg));
        }

        public abstract void Configure();
    }
}