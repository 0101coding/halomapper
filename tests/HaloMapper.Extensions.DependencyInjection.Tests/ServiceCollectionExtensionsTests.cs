using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Xunit;
using HaloMapper.Extensions.DependencyInjection;

namespace HaloMapper.Extensions.DependencyInjection.Tests
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddHaloMapper_WithConfiguration_RegistersServices()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddHaloMapper(config =>
            {
                config.CreateMap<SourceModel, DestModel>();
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var mapperConfig = serviceProvider.GetRequiredService<MapperConfiguration>();
            var mapper = serviceProvider.GetRequiredService<IMapper>();

            Assert.NotNull(mapperConfig);
            Assert.NotNull(mapper);
            Assert.IsType<Mapper>(mapper);
        }

        [Fact]
        public void AddHaloMapper_WithAssembly_RegistersProfilesFromAssembly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddHaloMapper(Assembly.GetExecutingAssembly());
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var source = new TestSourceModel { Name = "Test", Value = 42 };
            var result = mapper.Map<TestSourceModel, TestDestModel>(source);

            Assert.Equal("Test", result.Name);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void AddHaloMapper_WithProfileTypes_RegistersSpecificProfiles()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddHaloMapper(typeof(TestMappingProfile));
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            var source = new TestSourceModel { Name = "Test", Value = 42 };
            var result = mapper.Map<TestSourceModel, TestDestModel>(source);

            Assert.Equal("Test", result.Name);
            Assert.Equal(42, result.Value);
        }

        [Fact]
        public void AddHaloMapper_WithMarkerType_RegistersFromMarkerAssembly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddHaloMapper<TestMappingProfile>();
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            Assert.NotNull(mapper);
        }

        [Fact]
        public void AddHaloMapper_WithoutParameters_ScansCallingAssembly()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddHaloMapper();
            var serviceProvider = services.BuildServiceProvider();

            // Assert
            var mapper = serviceProvider.GetRequiredService<IMapper>();
            Assert.NotNull(mapper);
        }

        [Fact]
        public void AddScopedHaloMapper_RegistersScopedMapper()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act
            services.AddScopedHaloMapper(config =>
            {
                config.CreateMap<SourceModel, DestModel>();
            });

            var serviceProvider = services.BuildServiceProvider();

            // Assert
            using var scope1 = serviceProvider.CreateScope();
            using var scope2 = serviceProvider.CreateScope();

            var mapper1 = scope1.ServiceProvider.GetRequiredService<IMapper>();
            var mapper2 = scope2.ServiceProvider.GetRequiredService<IMapper>();

            Assert.NotNull(mapper1);
            Assert.NotNull(mapper2);
            Assert.NotSame(mapper1, mapper2); // Different instances per scope
        }

        [Fact]
        public void AddHaloMapper_WithInvalidConfiguration_ThrowsException()
        {
            // Arrange
            var services = new ServiceCollection();

            // Act &amp; Assert
            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                services.AddHaloMapper(config =>
                {
                    config.CreateMap<SourceModel, InvalidDestModel>(); // This will have validation errors
                });
            });

            Assert.Contains("configuration is invalid", exception.Message);
        }

        [Fact]
        public void AddHaloMapper_InHostBuilder_Works()
        {
            // Arrange &amp; Act
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddHaloMapper(config =>
                    {
                        config.CreateMap<SourceModel, DestModel>();
                    });
                })
                .Build();

            // Assert
            var mapper = host.Services.GetRequiredService<IMapper>();
            Assert.NotNull(mapper);

            var source = new SourceModel { Name = "Integration Test" };
            var result = mapper.Map<SourceModel, DestModel>(source);
            Assert.Equal("Integration Test", result.Name);
        }

        [Fact]
        public void MapperConfiguration_AddProfilesFromAssembly_FindsAllProfiles()
        {
            // Arrange
            var config = new MapperConfiguration();

            // Act
            config.AddProfilesFromAssembly(Assembly.GetExecutingAssembly());

            // Assert
            var mapper = new Mapper(config);
            var source = new TestSourceModel { Name = "Assembly Test", Value = 100 };
            var result = mapper.Map<TestSourceModel, TestDestModel>(source);

            Assert.Equal("Assembly Test", result.Name);
            Assert.Equal(100, result.Value);
        }

        // Test Models
        public class SourceModel
        {
            public string Name { get; set; } = default!;
        }

        public class DestModel
        {
            public string Name { get; set; } = default!;
        }

        public class TestSourceModel
        {
            public string Name { get; set; } = default!;
            public int Value { get; set; }
        }

        public class TestDestModel
        {
            public string Name { get; set; } = default!;
            public int Value { get; set; }
        }

        public class InvalidDestModel
        {
            public ComplexObject ComplexProp { get; set; } = default!; // No mapping exists
        }

        public class ComplexObject
        {
            public string Value { get; set; } = default!;
        }
    }

    // Test Profile that will be discovered
    public class TestMappingProfile : Profile
    {
        public override void Configure()
        {
            CreateMap<ServiceCollectionExtensionsTests.TestSourceModel, ServiceCollectionExtensionsTests.TestDestModel>();
        }
    }
}