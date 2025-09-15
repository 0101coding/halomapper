using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using HaloMapper.TypeConverters;
  using HaloMapper.Validation;
  
  namespace HaloMapper
  {
    /// <summary>
    /// Provides configuration for object mappings, type converters, and profiles in HaloMapper.
    /// </summary>
    public class MapperConfiguration
      {
          internal readonly ConcurrentDictionary<(Type, Type), IMapPlan> _plans = new();
          internal readonly ConcurrentDictionary<(Type, Type), object> _expressions = new();
          internal readonly ITypeConverterRegistry _typeConverters = new TypeConverterRegistry();

          private readonly List<Profile> _profiles = new();
          
          /// <summary>
          /// Gets or sets whether compiled expressions are used for mapping (recommended for performance).
          /// </summary>
          public bool UseCompiledExpressions { get; set; } = true;
  
          /// <summary>
          /// Adds a mapping profile to the configuration.
          /// </summary>
          /// <param name="p">The profile to add.</param>
          public void AddProfile(Profile p) => _profiles.Add(p);

          /// <summary>
          /// Adds a type converter for the specified source and destination types.
          /// </summary>
          /// <typeparam name="TSource">Source type.</typeparam>
          /// <typeparam name="TDestination">Destination type.</typeparam>
          /// <param name="converter">The type converter to add.</param>
          public void AddTypeConverter<TSource, TDestination>(ITypeConverter<TSource, TDestination> converter)
          {
              _typeConverters.AddConverter(converter);
          }

          /// <summary>
          /// Adds a type converter for the specified source and destination types using reflection.
          /// </summary>
          /// <param name="sourceType">Source type.</param>
          /// <param name="destinationType">Destination type.</param>
          /// <param name="converter">The type converter instance.</param>
          public void AddTypeConverter(Type sourceType, Type destinationType, object converter)
          {
              _typeConverters.AddConverter(sourceType, destinationType, converter);
          }
  
          /// <summary>
          /// Creates a mapping expression between <typeparamref name="TSource"/> and <typeparamref name="TDestination"/>.
          /// Optionally accepts a configuration action for customizing the mapping.
          /// </summary>
          /// <typeparam name="TSource">Source type.</typeparam>
          /// <typeparam name="TDestination">Destination type.</typeparam>
          /// <param name="cfg">Optional configuration for the mapping expression.</param>
          /// <returns>The mapping expression.</returns>
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
  
          /// <summary>
          /// Creates and adds a profile of type <typeparamref name="T"/> to the configuration.
          /// </summary>
          /// <typeparam name="T">The profile type to create and add.</typeparam>
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
  
          /// <summary>
          /// Attempts to retrieve a mapping plan for the specified source and destination types.
          /// </summary>
          /// <param name="s">Source type.</param>
          /// <param name="d">Destination type.</param>
          /// <param name="plan">The mapping plan, if found.</param>
          /// <returns>True if a plan exists; otherwise, false.</returns>
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

          /// <summary>
          /// Validates all mapping configurations and returns the result.
          /// </summary>
          /// <returns>The validation result for all mappings.</returns>
          public ValidationResult ValidateConfiguration()
          {
              var validator = new ConfigurationValidator(this);
              return validator.ValidateAll();
          }

          /// <summary>
          /// Validates the mapping configuration for the specified source and destination types.
          /// </summary>
          /// <typeparam name="TSource">Source type.</typeparam>
          /// <typeparam name="TDestination">Destination type.</typeparam>
          /// <returns>The validation result for the mapping.</returns>
          public ValidationResult ValidateMapping<TSource, TDestination>()
          {
              var validator = new ConfigurationValidator(this);
              return validator.ValidateMapping<TSource, TDestination>();
          }

          /// <summary>
          /// Throws an exception if the mapping configuration is invalid.
          /// </summary>
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