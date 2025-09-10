using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HaloMapper.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds HaloMapper services to the service collection with automatic profile discovery
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="assemblies">Assemblies to scan for profiles</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddHaloMapper(this IServiceCollection services, params Assembly[] assemblies)
        {
            return services.AddHaloMapper(config =>
            {
                config.AddProfiles(assemblies);
            });
        }

        /// <summary>
        /// Adds HaloMapper services to the service collection with configuration action
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configAction">Configuration action</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddHaloMapper(this IServiceCollection services, Action<MapperConfiguration> configAction)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configAction == null) throw new ArgumentNullException(nameof(configAction));

            var config = new MapperConfiguration();
            configAction(config);

            // Validate configuration at startup
            var validationResult = config.ValidateConfiguration();
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"HaloMapper configuration is invalid:\n{validationResult}");
            }

            services.AddSingleton(config);
            services.AddTransient<IMapper, Mapper>();

            return services;
        }

        /// <summary>
        /// Adds HaloMapper services with specific profile types
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="profileTypes">Profile types to register</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddHaloMapper(this IServiceCollection services, params Type[] profileTypes)
        {
            return services.AddHaloMapper(config =>
            {
                config.AddProfiles(profileTypes);
            });
        }

        /// <summary>
        /// Adds HaloMapper services by scanning the calling assembly for profiles
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddHaloMapper(this IServiceCollection services)
        {
            var callingAssembly = Assembly.GetCallingAssembly();
            return services.AddHaloMapper(callingAssembly);
        }

        /// <summary>
        /// Adds HaloMapper services with a marker type from the assembly to scan
        /// </summary>
        /// <typeparam name="TMarker">Type from assembly to scan</typeparam>
        /// <param name="services">The service collection</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddHaloMapper<TMarker>(this IServiceCollection services)
        {
            return services.AddHaloMapper(typeof(TMarker).Assembly);
        }

        /// <summary>
        /// Adds a scoped mapper instance with custom configuration
        /// </summary>
        /// <param name="services">The service collection</param>
        /// <param name="configAction">Configuration action</param>
        /// <returns>The service collection for chaining</returns>
        public static IServiceCollection AddScopedHaloMapper(this IServiceCollection services, Action<MapperConfiguration> configAction)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configAction == null) throw new ArgumentNullException(nameof(configAction));

            services.AddScoped<IMapper>(serviceProvider =>
            {
                var config = new MapperConfiguration();
                configAction(config);

                // Validate configuration
                var validationResult = config.ValidateConfiguration();
                if (!validationResult.IsValid)
                {
                    throw new InvalidOperationException($"HaloMapper configuration is invalid:\n{validationResult}");
                }

                return new Mapper(config);
            });

            return services;
        }
    }

    /// <summary>
    /// Extensions for MapperConfiguration to work with DI
    /// </summary>
    public static class MapperConfigurationExtensions
    {
        /// <summary>
        /// Add profiles from assemblies
        /// </summary>
        /// <param name="configuration">The mapper configuration</param>
        /// <param name="assemblies">Assemblies to scan</param>
        public static void AddProfiles(this MapperConfiguration configuration, params Assembly[] assemblies)
        {
            if (assemblies?.Length == 0)
            {
                assemblies = new[] { Assembly.GetCallingAssembly() };
            }

            foreach (var assembly in assemblies ?? Array.Empty<Assembly>())
            {
                AddProfilesFromAssembly(configuration, assembly);
            }
        }

        /// <summary>
        /// Add specific profile types
        /// </summary>
        /// <param name="configuration">The mapper configuration</param>
        /// <param name="profileTypes">Profile types to add</param>
        public static void AddProfiles(this MapperConfiguration configuration, params Type[] profileTypes)
        {
            foreach (var profileType in profileTypes ?? Array.Empty<Type>())
            {
                if (!typeof(Profile).IsAssignableFrom(profileType))
                {
                    throw new ArgumentException($"Type {profileType.Name} must inherit from Profile", nameof(profileTypes));
                }

                var profile = (Profile)Activator.CreateInstance(profileType)!;
                profile.Configure();
                configuration.AddProfile(profile);
            }
        }

        /// <summary>
        /// Add profiles from a specific assembly
        /// </summary>
        /// <param name="configuration">The mapper configuration</param>
        /// <param name="assembly">Assembly to scan</param>
        public static void AddProfilesFromAssembly(this MapperConfiguration configuration, Assembly assembly)
        {
            var profileTypes = assembly.GetTypes()
                .Where(t => typeof(Profile).IsAssignableFrom(t) 
                           && !t.IsAbstract 
                           && t.GetConstructor(Type.EmptyTypes) != null)
                .ToArray();

            configuration.AddProfiles(profileTypes);
        }

        /// <summary>
        /// Add profiles from assemblies containing the specified marker types
        /// </summary>
        /// <param name="configuration">The mapper configuration</param>
        /// <param name="markerTypes">Types whose assemblies should be scanned</param>
        public static void AddProfilesFromMarkerTypes(this MapperConfiguration configuration, params Type[] markerTypes)
        {
            var assemblies = markerTypes?.Select(t => t.Assembly).Distinct().ToArray() ?? Array.Empty<Assembly>();
            configuration.AddProfiles(assemblies);
        }
    }
}