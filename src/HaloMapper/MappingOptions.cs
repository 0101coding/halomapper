using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using HaloMapper.TypeConverters;
  using HaloMapper.Validation;
  
  namespace HaloMapper
  {
      public class MapperConfiguration
      {
          internal readonly ConcurrentDictionary<(Type, Type), IMapPlan> _plans = new();
          internal readonly ConcurrentDictionary<(Type, Type), object> _expressions = new();
          internal readonly ITypeConverterRegistry _typeConverters = new TypeConverterRegistry();

          private readonly List<Profile> _profiles = new();
          
          public bool UseCompiledExpressions { get; set; } = true;
  
          public void AddProfile(Profile p) => _profiles.Add(p);

          public void AddTypeConverter<TSource, TDestination>(ITypeConverter<TSource, TDestination> converter)
          {
              _typeConverters.AddConverter(converter);
          }

          public void AddTypeConverter(Type sourceType, Type destinationType, object converter)
          {
              _typeConverters.AddConverter(sourceType, destinationType, converter);
          }
  
          public MappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>(Action<MappingExpression<TSource, TDestination>>? cfg = null)
          {
              var expr = new MappingExpression<TSource, TDestination>(this);
              if (cfg != null)
              {
                  cfg.Invoke(expr);
                  // Build plan immediately if configuration was provided via lambda
                  _plans[(typeof(TSource), typeof(TDestination))] = expr.BuildPlan();
              }
              else
              {
                  // Store the expression for later configuration via method chaining
                  // But also build a default plan immediately in case no chaining happens
                  _expressions[(typeof(TSource), typeof(TDestination))] = expr;
                  _plans[(typeof(TSource), typeof(TDestination))] = expr.BuildPlan();
              }
              return expr;
          }
  
          public void CreateProfile<T>() where T : Profile, new()
          {
              var p = new T();
              p.Configure();
              AddProfile(p);
              foreach (var action in p.Actions)
              {
                  action(this);
              }
          }
  
          public bool TryGetPlan(Type s, Type d, out IMapPlan plan)
          {
              if (_plans.TryGetValue((s, d), out plan))
                  return true;
              
              // Check if we have an expression that needs to be built
              if (_expressions.TryGetValue((s, d), out var exprObj))
              {
                  // Use reflection to call BuildPlan on the expression
                  var buildPlanMethod = exprObj.GetType().GetMethod("BuildPlan");
                  if (buildPlanMethod != null)
                  {
                      plan = (IMapPlan)buildPlanMethod.Invoke(exprObj, null)!;
                      _plans[(s, d)] = plan;
                      _expressions.TryRemove((s, d), out _);
                      return true;
                  }
              }
              
              plan = null!;
              return false;
          }
  
          // helper to ensure a map exists - used for nested mapping and reverse maps
          internal void EnsurePlan<TSource, TDestination>()
          {
              if (!_plans.ContainsKey((typeof(TSource), typeof(TDestination))))
              {
                  var expr = new MappingExpression<TSource, TDestination>(this);
                  _plans[(typeof(TSource), typeof(TDestination))] = expr.BuildPlan();
              }
          }

          public ValidationResult ValidateConfiguration()
          {
              var validator = new ConfigurationValidator(this);
              return validator.ValidateAll();
          }

          public ValidationResult ValidateMapping<TSource, TDestination>()
          {
              var validator = new ConfigurationValidator(this);
              return validator.ValidateMapping<TSource, TDestination>();
          }

          public void AssertConfigurationIsValid()
          {
              var result = ValidateConfiguration();
              if (!result.IsValid)
              {
                  throw new InvalidOperationException($"Configuration validation failed:\n{result}");
              }
          }
      }
  
      
  }