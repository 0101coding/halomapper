namespace HaloMapper
{
    public class ReflectionMapPlan<TSource, TDestination> : ReflectionMapPlan
    {
        public Dictionary<string, MemberPlan> MemberPlans { get; } = new();
        public Func<object, object>? Constructor { get; set; }

        public ReflectionMapPlan() : base(typeof(TSource), typeof(TDestination), new List<MemberPlan>(), null, null, null)
        {
        }
    }

    public class ReflectionMapPlan : IMapPlan
    {
        private readonly Type _sourceType;
        private readonly Type _destType;
        private readonly List<MemberPlan> _memberPlans;
        private readonly Func<object>? _constructor;
        private readonly Action<object, object>? _beforeMap;
        private readonly Action<object, object>? _afterMap;

        public ReflectionMapPlan(Type sourceType, Type destType, List<MemberPlan> memberPlans,
            Func<object>? constructor, Action<object, object>? beforeMap, Action<object, object>? afterMap)
        {
            _sourceType = sourceType;
            _destType = destType;
            _memberPlans = memberPlans;
            _constructor = constructor;
            _beforeMap = beforeMap;
            _afterMap = afterMap;
        }

        public object Map(object source, object? destination, Mapper mapper)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var srcType = source.GetType();
            if (srcType != _sourceType && !_sourceType.IsAssignableFrom(srcType))
                throw new InvalidOperationException($"Invalid source type: {srcType.Name}");

            var dest = destination ?? _constructor?.Invoke() ?? Activator.CreateInstance(_destType)!;

            _beforeMap?.Invoke(source, dest);

            foreach (var mp in _memberPlans)
            {
                if (mp.Ignore) continue;
                var value = mp.Resolver != null
                    ? mp.Resolver(source, dest)
                    : mp.SourceGetter?.Invoke(source);

                if (value == null && mp.NullSubstitute != null)
                    value = mp.NullSubstitute;

                if (mp.Condition != null && !mp.Condition(source, dest))
                    continue;

                if (value != null && mp.DestinationType != null && value.GetType() != mp.DestinationType)
                {
                    // First try type converter
                    var convertedValue = mapper.Configuration._typeConverters.Convert(value, value.GetType(), mp.DestinationType);
                    if (convertedValue != null)
                    {
                        value = convertedValue;
                    }
                    // Then try nested mapping
                    else if (mapper.Configuration.TryGetPlan(value.GetType(), mp.DestinationType, out var plan))
                    {
                        value = plan.Map(value, null, mapper);
                    }
                }

                mp.DestSetter?.Invoke(dest, value);
            }

            _afterMap?.Invoke(source, dest);
            return dest;
        }

        public class MemberPlan
        {
             public string DestinationName { get; set; } = default!;
    public Func<object, object?>? SourceGetter { get; set; }
    public Action<object, object?>? DestSetter { get; set; }
    public Type? DestinationType { get; set; }
    public bool Ignore { get; set; }
    public Func<object, object, bool>? Condition { get; set; }
    public object? NullSubstitute { get; set; }
    public Func<object, object, object?>? Resolver { get; set; } 
        }
    }
}