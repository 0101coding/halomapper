using System;
using System.Linq;
using Xunit;

namespace HaloMapper.Tests
{
    public class ProfileTests
    {
        [Fact]
        public void Profile_BasicMapping_WorksCorrectly()
        {
            // Arrange
            var profile = new BasicMappingProfile();
            var config = new MapperConfiguration();

            // Act
            profile.Configure();
            profile.ApplyToConfiguration(config);

            var mapper = new Mapper(config);
            var source = new ProfileTestSource { Name = "John", Age = 30 };
            var result = mapper.Map<ProfileTestSource, ProfileTestDestination>(source);

            // Assert
            Assert.Equal("John", result.Name);
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public void Profile_CustomMapping_AppliesCustomConfiguration()
        {
            // Arrange
            var profile = new CustomMappingProfile();
            var config = new MapperConfiguration();

            // Act
            profile.Configure();
            profile.ApplyToConfiguration(config);

            var mapper = new Mapper(config);
            var source = new ProfileTestSource { Name = "John", Age = 30 };
            var result = mapper.Map<ProfileTestSource, ProfileTestDestination>(source);

            // Assert
            Assert.Equal("Custom: John", result.Name);
            Assert.Equal(0, result.Age); // Should be ignored
        }

        [Fact]
        public void Profile_MultipleMappings_CreatesAllMappings()
        {
            // Arrange
            var profile = new MultipleMappingsProfile();
            var config = new MapperConfiguration();

            // Act
            profile.Configure();
            profile.ApplyToConfiguration(config);

            var mapper = new Mapper(config);

            // Test first mapping
            var source1 = new ProfileTestSource { Name = "John" };
            var result1 = mapper.Map<ProfileTestSource, ProfileTestDestination>(source1);
            Assert.Equal("John", result1.Name);

            // Test second mapping
            var source2 = new AnotherSource { Value = "Test" };
            var result2 = mapper.Map<AnotherSource, AnotherDestination>(source2);
            Assert.Equal("Test", result2.Value);

            // Test third mapping
            var source3 = new ThirdSource { Data = 42 };
            var result3 = mapper.Map<ThirdSource, ThirdDestination>(source3);
            Assert.Equal(42, result3.Data);
        }

        [Fact]
        public void Profile_NestedMapping_WorksCorrectly()
        {
            // Arrange
            var profile = new NestedMappingProfile();
            var config = new MapperConfiguration();

            // Act
            profile.Configure();
            profile.ApplyToConfiguration(config);

            var mapper = new Mapper(config);
            var source = new NestedSource
            {
                Name = "Parent",
                Child = new ChildSource { Name = "Child", Value = 100 }
            };
            var result = mapper.Map<NestedSource, NestedDestination>(source);

            // Assert
            Assert.Equal("Parent", result.Name);
            Assert.NotNull(result.Child);
            Assert.Equal("Child", result.Child.Name);
            Assert.Equal(100, result.Child.Value);
        }

        [Fact]
        public void Profile_WithComplexMapping_HandlesAllFeatures()
        {
            // Arrange
            var profile = new ComplexMappingProfile();
            var config = new MapperConfiguration();

            // Act
            profile.Configure();
            profile.ApplyToConfiguration(config);

            var mapper = new Mapper(config);
            var source = new ComplexSource
            {
                Name = "John",
                Age = 30,
                IsActive = true,
                SecretValue = "Secret"
            };
            var result = mapper.Map<ComplexSource, ComplexDestination>(source);

            // Assert
            Assert.Equal("JOHN", result.Name); // Custom resolver
            Assert.Equal(30, result.Age); // Direct mapping
        }

        [Fact]
        public void Profile_EmptyProfile_DoesNotCauseErrors()
        {
            // Arrange
            var profile = new EmptyProfile();
            var config = new MapperConfiguration();

            // Act
            profile.Configure();
            profile.ApplyToConfiguration(config);

            // Assert - Should not throw
            var mapper = new Mapper(config);
            Assert.NotNull(mapper);
        }

        [Fact]
        public void Profile_InheritanceChain_WorksCorrectly()
        {
            // Arrange
            var profile = new DerivedProfile();
            var config = new MapperConfiguration();

            // Act
            profile.Configure();
            profile.ApplyToConfiguration(config);

            var mapper = new Mapper(config);

            // Test base mapping
            var source1 = new ProfileTestSource { Name = "Base" };
            var result1 = mapper.Map<ProfileTestSource, ProfileTestDestination>(source1);
            Assert.Equal("Base", result1.Name);

            // Test derived mapping
            var source2 = new DerivedSource { DerivedProperty = "Derived" };
            var result2 = mapper.Map<DerivedSource, DerivedDestination>(source2);
            Assert.Equal("Derived", result2.DerivedProperty);
        }

        [Fact]
        public void Profile_WithMapperConfiguration_IntegratesCorrectly()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.AddProfile(new BasicMappingProfile());

            // Act
            var mapper = new Mapper(config);
            var source = new ProfileTestSource { Name = "Integration", Age = 25 };
            var result = mapper.Map<ProfileTestSource, ProfileTestDestination>(source);

            // Assert
            Assert.Equal("Integration", result.Name);
            Assert.Equal(25, result.Age);
        }

        // Test Profile classes
        public class BasicMappingProfile : Profile
        {
            public override void Configure()
            {
                CreateMap<ProfileTestSource, ProfileTestDestination>();
            }

            public void ApplyToConfiguration(MapperConfiguration config)
            {
                foreach (var action in Actions)
                {
                    action(config);
                }
            }
        }

        public class CustomMappingProfile : Profile
        {
            public override void Configure()
            {
                CreateMap<ProfileTestSource, ProfileTestDestination>(cfg =>
                {
                    cfg.ForMember(dest => dest.Name, opt => opt.MapFrom(src => $"Custom: {src.Name}"));
                    cfg.ForMember(dest => dest.Age, opt => opt.Ignore());
                });
            }

            public void ApplyToConfiguration(MapperConfiguration config)
            {
                foreach (var action in Actions)
                {
                    action(config);
                }
            }
        }

        public class MultipleMappingsProfile : Profile
        {
            public override void Configure()
            {
                CreateMap<ProfileTestSource, ProfileTestDestination>();
                CreateMap<AnotherSource, AnotherDestination>();
                CreateMap<ThirdSource, ThirdDestination>();
            }

            public void ApplyToConfiguration(MapperConfiguration config)
            {
                foreach (var action in Actions)
                {
                    action(config);
                }
            }
        }

        public class NestedMappingProfile : Profile
        {
            public override void Configure()
            {
                CreateMap<ChildSource, ChildDestination>();
                CreateMap<NestedSource, NestedDestination>();
            }

            public void ApplyToConfiguration(MapperConfiguration config)
            {
                foreach (var action in Actions)
                {
                    action(config);
                }
            }
        }

        public class ComplexMappingProfile : Profile
        {
            public override void Configure()
            {
                CreateMap<ComplexSource, ComplexDestination>(cfg =>
                {
                    cfg.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name.ToUpper()));
                    cfg.ForMember(dest => dest.SecretValue, opt => opt.Ignore());
                });
            }

            public void ApplyToConfiguration(MapperConfiguration config)
            {
                foreach (var action in Actions)
                {
                    action(config);
                }
            }
        }

        public class EmptyProfile : Profile
        {
            public override void Configure()
            {
                // Intentionally empty
            }

            public void ApplyToConfiguration(MapperConfiguration config)
            {
                foreach (var action in Actions)
                {
                    action(config);
                }
            }
        }

        public class BaseProfile : Profile
        {
            public override void Configure()
            {
                CreateMap<ProfileTestSource, ProfileTestDestination>();
            }

            public void ApplyToConfiguration(MapperConfiguration config)
            {
                foreach (var action in Actions)
                {
                    action(config);
                }
            }
        }

        public class DerivedProfile : BaseProfile
        {
            public override void Configure()
            {
                base.Configure();
                CreateMap<DerivedSource, DerivedDestination>();
            }
        }

        // Test data classes
        public class ProfileTestSource
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public class ProfileTestDestination
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public class AnotherSource
        {
            public string? Value { get; set; }
        }

        public class AnotherDestination
        {
            public string? Value { get; set; }
        }

        public class ThirdSource
        {
            public int Data { get; set; }
        }

        public class ThirdDestination
        {
            public int Data { get; set; }
        }

        public class ChildSource
        {
            public string? Name { get; set; }
            public int Value { get; set; }
        }

        public class ChildDestination
        {
            public string? Name { get; set; }
            public int Value { get; set; }
        }

        public class NestedSource
        {
            public string? Name { get; set; }
            public ChildSource? Child { get; set; }
        }

        public class NestedDestination
        {
            public string? Name { get; set; }
            public ChildDestination? Child { get; set; }
        }

        public class ComplexSource
        {
            public string Name { get; set; } = default!;
            public int Age { get; set; }
            public bool IsActive { get; set; }
            public string? SecretValue { get; set; }
        }

        public class ComplexDestination
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            public string? Status { get; set; }
            public string? SecretValue { get; set; }
        }

        public class DerivedSource
        {
            public string? DerivedProperty { get; set; }
        }

        public class DerivedDestination
        {
            public string? DerivedProperty { get; set; }
        }
    }
}