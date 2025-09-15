using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace HaloMapper
{
    /// <summary>
    /// Represents the configuration for mapping between <typeparamref name="TSource"/> and <typeparamref name="TDestination"/>.
    /// </summary>
    public class MappingExpression<TSource, TDestination>
    {
        private readonly MapperConfiguration? _config;
        private readonly List<Action<ReflectionMapPlan.MemberPlan>> _memberConfigs = new();
        private Func<TSource, TDestination>? _constructUsing;
        private Action<TSource, TDestination>? _beforeMap;
        private Action<TSource, TDestination>? _afterMap;
        private readonly ReflectionMapPlan<TSource, TDestination> _reflectionPlan;
        private bool _enableFlattening = true;

    /// <summary>
    /// Initializes a new instance of <see cref="MappingExpression{TSource, TDestination}"/> with a reflection plan.
    /// </summary>
    /// <param name="reflectionPlan">The reflection mapping plan.</param>
    public MappingExpression(ReflectionMapPlan<TSource, TDestination> reflectionPlan)
        {
            _reflectionPlan = reflectionPlan;
        }

    /// <summary>
    /// Initializes a new instance of <see cref="MappingExpression{TSource, TDestination}"/> with a mapper configuration.
    /// </summary>
    /// <param name="config">The mapper configuration.</param>
    public MappingExpression(MapperConfiguration config)
        {
            _config = config;
            _reflectionPlan = new ReflectionMapPlan<TSource, TDestination>();
        }

    /// <summary>
    /// Configures a mapping for a specific member of the destination type.
    /// </summary>
    /// <typeparam name="TMember">The member type.</typeparam>
    /// <param name="destinationMember">Expression selecting the destination member.</param>
    /// <param name="memberOptions">Options for configuring the member mapping.</param>
    /// <returns>The mapping expression for chaining.</returns>
    public MappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<MemberOptions<TSource, TDestination, TMember>> memberOptions)
    {
        var destName = GetMemberName(destinationMember);
        var destProp = typeof(TDestination).GetProperty(destName, BindingFlags.Public | BindingFlags.Instance);
        
        var plan = new ReflectionMapPlan.MemberPlan
        {
            DestinationName = destName,
            DestinationType = destProp?.PropertyType,
            DestSetter = destProp != null ? (dest, val) => destProp.SetValue(dest, val) : null
        };

        var options = new MemberOptions<TSource, TDestination, TMember>(plan);
        memberOptions(options);

        _reflectionPlan.MemberPlans[destName] = plan;
        CompletePlan();
        return this;
    }

        /// <summary>
        /// Configures a custom constructor for the destination type.
        /// </summary>
        /// <param name="ctor">The constructor function.</param>
        /// <returns>The mapping expression for chaining.</returns>
        public MappingExpression<TSource, TDestination> ConstructUsing(
            Func<TSource, TDestination> ctor)
        {
            _constructUsing = ctor;
            _reflectionPlan.Constructor = s => ctor((TSource)s);
            CompletePlan();
            return this;
        }


        /// <summary>
        /// Configures an action to run before mapping.
        /// </summary>
        /// <param name="action">The action to run before mapping.</param>
        /// <returns>The mapping expression for chaining.</returns>
        public MappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action)
        {
            _beforeMap = action;
            CompletePlan();
            return this;
        }

        /// <summary>
        /// Configures an action to run after mapping.
        /// </summary>
        /// <param name="action">The action to run after mapping.</param>
        /// <returns>The mapping expression for chaining.</returns>
        public MappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action)
        {
            _afterMap = action;
            CompletePlan();
            return this;
        }

        /// <summary>
        /// Enables automatic flattening of nested properties during mapping.
        /// </summary>
        /// <param name="enable">Whether to enable flattening (default: true).</param>
        /// <returns>The mapping expression for chaining.</returns>
        public MappingExpression<TSource, TDestination> EnableFlattening(bool enable = true)
        {
            _enableFlattening = enable;
            return this;
        }

        /// <summary>
        /// Disables automatic flattening of nested properties during mapping.
        /// </summary>
        /// <returns>The mapping expression for chaining.</returns>
        public MappingExpression<TSource, TDestination> DisableFlattening()
        {
            return EnableFlattening(false);
        }

        // Called to complete the configuration and build the plan
        internal void CompletePlan()
        {
            if (_config != null)
            {
                var plan = BuildPlan();
                _config._plans[(typeof(TSource), typeof(TDestination))] = plan;
                _config._expressions.TryRemove((typeof(TSource), typeof(TDestination)), out _);
            }
        }

        internal IMapPlan BuildPlan()
        {
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>();
            
            // Add configured member plans from _reflectionPlan.MemberPlans
            foreach (var configuredPlan in _reflectionPlan.MemberPlans.Values)
            {
                memberPlans.Add(configuredPlan);
            }
            
            // Add default mappings for properties not explicitly configured
            foreach (var prop in typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite) continue;
                if (_reflectionPlan.MemberPlans.ContainsKey(prop.Name)) continue; // Skip already configured

                var srcProp = typeof(TSource).GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance);
                var mp = new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = prop.Name,
                    DestinationType = prop.PropertyType,
                    DestSetter = (dest, val) => prop.SetValue(dest, val),
                    SourceGetter = srcProp != null ? (Func<object, object?>)(src => srcProp.GetValue(src)) : null
                };
                memberPlans.Add(mp);
            }

            // Add member configs (legacy support)
            foreach (var apply in _memberConfigs)
            {
                var mp = new ReflectionMapPlan.MemberPlan();
                apply(mp);
                memberPlans.Add(mp);
            }

            Func<object>? constructor = _constructUsing != null ? () => (object)_constructUsing(default(TSource)!) : null;
            Action<object, object>? beforeMap = _beforeMap != null ? (src, dest) => _beforeMap((TSource)src, (TDestination)dest) : null;
            Action<object, object>? afterMap = _afterMap != null ? (src, dest) => _afterMap((TSource)src, (TDestination)dest) : null;

            // Use flattening plan if enabled
            if (_enableFlattening)
            {
                return FlatteningMapPlanFactory.CreateFlatteningPlan(
                    typeof(TSource),
                    typeof(TDestination),
                    constructor,
                    beforeMap,
                    afterMap,
                    _reflectionPlan.MemberPlans
                );
            }

            // Use compiled expressions if enabled and configuration is available
            if (_config?.UseCompiledExpressions == true)
            {
                return CompiledMapPlanFactory.CreateCompiledPlan(
                    typeof(TSource),
                    typeof(TDestination),
                    memberPlans,
                    constructor,
                    beforeMap,
                    afterMap
                );
            }

            return new ReflectionMapPlan(
                typeof(TSource),
                typeof(TDestination),
                memberPlans,
                constructor,
                beforeMap,
                afterMap
            );
        }
        /// <summary>
        /// Gets the name of the member selected by the expression.
        /// </summary>
        /// <typeparam name="TDest">Destination type.</typeparam>
        /// <typeparam name="TMember">Member type.</typeparam>
        /// <param name="destinationMember">Expression selecting the member.</param>
        /// <returns>The name of the member.</returns>
        public static string GetMemberName<TDest, TMember>(
             Expression<Func<TDest, TMember>> destinationMember)
        {
            var names = new List<string>();
            Expression? expr = destinationMember.Body;

            while (expr != null)
            {
                if (expr is MemberExpression memberExpr)
                {
                    names.Insert(0, memberExpr.Member.Name);
                    expr = memberExpr.Expression;
                }
                else if (expr is UnaryExpression unaryExpr &&
                         unaryExpr.Operand is MemberExpression memberOperand)
                {
                    names.Insert(0, memberOperand.Member.Name);
                    expr = memberOperand.Expression;
                }
                else
                {
                    break;
                }
            }

            if (names.Count == 0)
                throw new ArgumentException(
                    $"Expression '{destinationMember}' does not refer to a valid property or field.");

            return string.Join(".", names);
        }

    }
    
    /// <summary>
    /// Provides options for configuring member mappings in a mapping expression.
    /// </summary>
    public class MemberOptions<TSource, TDestination, TMember> : IMemberOptions<TSource, TDestination, TMember>
    {
        private readonly ReflectionMapPlan.MemberPlan _plan;

    /// <summary>
    /// Initializes a new instance of <see cref="MemberOptions{TSource, TDestination, TMember}"/>.
    /// </summary>
    /// <param name="plan">The member plan to configure.</param>
    public MemberOptions(ReflectionMapPlan.MemberPlan plan)
        {
            _plan = plan;
        }

    /// <summary>
    /// Configures the member to be mapped from the specified function.
    /// </summary>
    /// <param name="mapFunc">The mapping function.</param>
    public void MapFrom(Func<TSource, TMember> mapFunc)
        {
            _plan.SourceGetter = s => (object?)mapFunc((TSource)s);
        }

    /// <summary>
    /// Ignores the member during mapping.
    /// </summary>
    public void Ignore()
        {
            _plan.Ignore = true;
        }

    /// <summary>
    /// Configures a condition for mapping the member.
    /// </summary>
    /// <param name="predicate">The condition predicate.</param>
    public void Condition(Func<TSource, TDestination, bool> predicate)
        {
            _plan.Condition = (s, d) => predicate((TSource)s, (TDestination)d);
        }

    /// <summary>
    /// Configures a substitute value to use if the source value is null.
    /// </summary>
    /// <param name="value">The substitute value.</param>
    public void NullSubstitute(TMember value)
        {
            _plan.NullSubstitute = value;
        }

    /// <summary>
    /// Configures a custom resolver for the member.
    /// </summary>
    /// <param name="resolver">The resolver function.</param>
    public void ResolveUsing(Func<TSource, TDestination, object?> resolver)
        {
            _plan.Resolver = (s, d) => resolver((TSource)s, (TDestination)d);
        }
    }
}