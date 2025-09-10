using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace HaloMapper
{
    public class MappingExpression<TSource, TDestination>
    {
        private readonly MapperConfiguration? _config;
        private readonly List<Action<ReflectionMapPlan.MemberPlan>> _memberConfigs = new();
        private Func<TSource, TDestination>? _constructUsing;
        private Action<TSource, TDestination>? _beforeMap;
        private Action<TSource, TDestination>? _afterMap;
        private readonly ReflectionMapPlan<TSource, TDestination> _reflectionPlan;
        private bool _enableFlattening = true;

        public MappingExpression(ReflectionMapPlan<TSource, TDestination> reflectionPlan)
        {
            _reflectionPlan = reflectionPlan;
        }

        public MappingExpression(MapperConfiguration config)
        {
            _config = config;
            _reflectionPlan = new ReflectionMapPlan<TSource, TDestination>();
        }

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

        public MappingExpression<TSource, TDestination> ConstructUsing(
            Func<TSource, TDestination> ctor)
        {
            _constructUsing = ctor;
            _reflectionPlan.Constructor = s => ctor((TSource)s);
            CompletePlan();
            return this;
        }


        public MappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action)
        {
            _beforeMap = action;
            CompletePlan();
            return this;
        }

        public MappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action)
        {
            _afterMap = action;
            CompletePlan();
            return this;
        }

        public MappingExpression<TSource, TDestination> EnableFlattening(bool enable = true)
        {
            _enableFlattening = enable;
            return this;
        }

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
    
    public class MemberOptions<TSource, TDestination, TMember> : IMemberOptions<TSource, TDestination, TMember>
    {
        private readonly ReflectionMapPlan.MemberPlan _plan;

        public MemberOptions(ReflectionMapPlan.MemberPlan plan)
        {
            _plan = plan;
        }

        public void MapFrom(Func<TSource, TMember> mapFunc)
        {
            _plan.SourceGetter = s => (object?)mapFunc((TSource)s);
        }

        public void Ignore()
        {
            _plan.Ignore = true;
        }

        public void Condition(Func<TSource, TDestination, bool> predicate)
        {
            _plan.Condition = (s, d) => predicate((TSource)s, (TDestination)d);
        }

        public void NullSubstitute(TMember value)
        {
            _plan.NullSubstitute = value;
        }

        public void ResolveUsing(Func<TSource, TDestination, object?> resolver)
        {
            _plan.Resolver = (s, d) => resolver((TSource)s, (TDestination)d);
        }
    }
}