namespace HaloMapper
{
    /// <summary>
    /// Represents a reflection-based mapping plan for a specific source and destination type.
    /// </summary>
    /// <typeparam name="TSource">Source type.</typeparam>
    /// <typeparam name="TDestination">Destination type.</typeparam>
    public class ReflectionMapPlan<TSource, TDestination> : ReflectionMapPlan
    {
    /// <summary>
    /// Gets the member plans for the mapping.
    /// </summary>
    public Dictionary<string, MemberPlan> MemberPlans { get; } = new();
    /// <summary>
    /// Gets or sets the constructor function for the destination type.
    /// </summary>
    public Func<object, object>? Constructor { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="ReflectionMapPlan{TSource, TDestination}"/>.
    /// </summary>
    public ReflectionMapPlan() : base(typeof(TSource), typeof(TDestination), new List<MemberPlan>(), null, null, null)
        {
        }
    }

    /// <summary>
    /// Represents a reflection-based mapping plan for arbitrary source and destination types.
    /// </summary>
    public class ReflectionMapPlan : IMapPlan
    {
        private readonly Type _sourceType;
        private readonly Type _destType;
        private readonly List<MemberPlan> _memberPlans;
        private readonly Func<object>? _constructor;
        private readonly Action<object, object>? _beforeMap;
        private readonly Action<object, object>? _afterMap;

        /// <summary>
        /// Initializes a new instance of <see cref="ReflectionMapPlan"/>.
        /// </summary>
        /// <param name="sourceType">Source type.</param>
        /// <param name="destType">Destination type.</param>
        /// <param name="memberPlans">List of member plans.</param>
        /// <param name="constructor">Constructor function for the destination type.</param>
        /// <param name="beforeMap">Action to run before mapping.</param>
        /// <param name="afterMap">Action to run after mapping.</param>
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

    /// <summary>
    /// Maps the source object to the destination object using the configured member plans.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="destination">The destination object (optional).</param>
    /// <param name="mapper">The mapper instance.</param>
    /// <returns>The mapped destination object.</returns>
        public object Map(object source, object? destination, Mapper mapper)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var srcType = source.GetType();
            if (srcType != _sourceType && !_sourceType.IsAssignableFrom(srcType))
                throw new InvalidOperationException($"Invalid source type: {srcType.Name}");

            // Check for recursion
            var recursionKey = $"{srcType.FullName}->{_destType.FullName}";
            var context = mapper.GetMappingContext();

            if (context.IsInRecursion(recursionKey))
            {
                // Return default instance to break recursion
                return destination ?? _constructor?.Invoke() ?? Activator.CreateInstance(_destType)!;
            }

            context.EnterMapping(recursionKey);
            try
            {
                object? dest = destination;
            if (dest == null)
            {
                if (_constructor != null)
                {
                    var constructed = _constructor.Invoke();
                    if (constructed == null)
                        throw new InvalidOperationException("Constructor returned null for destination object.");
                    dest = constructed;
                }
                else
                {
                    dest = Activator.CreateInstance(_destType) ?? throw new InvalidOperationException($"Could not create instance of type {_destType}");
                }
            }

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
            finally
            {
                context.ExitMapping(recursionKey);
            }
        }

    /// <summary>
    /// Represents the mapping plan for a single member in the destination type.
    /// </summary>
    public class MemberPlan
        {
            /// <summary>
            /// Gets or sets the name of the destination member.
            /// </summary>
            public string DestinationName { get; set; } = default!;
            /// <summary>
            /// Gets or sets the function to retrieve the value from the source object.
            /// </summary>
            public Func<object, object?>? SourceGetter { get; set; }
            /// <summary>
            /// Gets or sets the action to set the value on the destination object.
            /// </summary>
            public Action<object, object?>? DestSetter { get; set; }
            /// <summary>
            /// Gets or sets the type of the destination member.
            /// </summary>
            public Type? DestinationType { get; set; }
            /// <summary>
            /// Gets or sets a value indicating whether this member should be ignored during mapping.
            /// </summary>
            public bool Ignore { get; set; }
            /// <summary>
            /// Gets or sets the condition function to determine if the member should be mapped.
            /// </summary>
            public Func<object, object, bool>? Condition { get; set; }
            /// <summary>
            /// Gets or sets the substitute value to use if the source value is null.
            /// </summary>
            public object? NullSubstitute { get; set; }
            /// <summary>
            /// Gets or sets the custom resolver function for the member.
            /// </summary>
            public Func<object, object, object?>? Resolver { get; set; }
        }
    }
}