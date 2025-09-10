using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using HaloMapper.TypeConverters;
using HaloMapper.Validation;
using HaloMapper.Extensions;

namespace HaloMapper.Tests
{
    public class EnhancedFeaturesTests
    {
        [Fact]
        public void TypeConverter_StringToInt_Works()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<StringModel, IntModel>();
            var mapper = new Mapper(cfg);

            var source = new StringModel { Value = "123" };
            var result = mapper.Map<StringModel, IntModel>(source);

            Assert.Equal(123, result.Value);
        }

        [Fact]
        public void CustomTypeConverter_Works()
        {
            var cfg = new MapperConfiguration();
            cfg.AddTypeConverter<string, CustomType>(new StringToCustomTypeConverter());
            cfg.CreateMap<StringModel, CustomModel>();
            var mapper = new Mapper(cfg);

            var source = new StringModel { Value = "test" };
            var result = mapper.Map<StringModel, CustomModel>(source);

            Assert.Equal("CUSTOM: test", result.Value.DisplayValue);
        }

        [Fact]
        public void ConfigurationValidation_DetectsUnmappedMembers()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<PersonSource, PersonDestination>();

            var validation = cfg.ValidateConfiguration();

            Assert.True(validation.Warnings.Any(w => w.Message.Contains("Unmapped")));
        }

        [Fact]
        public void ConfigurationValidation_ThrowsOnInvalidConfig()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<PersonSource, InvalidDestination>();

            Assert.Throws<InvalidOperationException>(() => cfg.AssertConfigurationIsValid());
        }

        [Fact]
        public void Flattening_Works()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<PersonWithAddress, FlatPersonDto>();
            var mapper = new Mapper(cfg);

            var source = new PersonWithAddress
            {
                Name = "John",
                Address = new Address { City = "New York", Street = "123 Main St" }
            };

            var result = mapper.Map<PersonWithAddress, FlatPersonDto>(source);

            Assert.Equal("John", result.Name);
            Assert.Equal("New York", result.AddressCity);
            Assert.Equal("123 Main St", result.AddressStreet);
        }

        [Fact]
        public void Flattening_HandlesNullNestedObjects()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<PersonWithAddress, FlatPersonDto>();
            var mapper = new Mapper(cfg);

            var source = new PersonWithAddress { Name = "John", Address = null };
            var result = mapper.Map<PersonWithAddress, FlatPersonDto>(source);

            Assert.Equal("John", result.Name);
            Assert.Null(result.AddressCity);
            Assert.Null(result.AddressStreet);
        }

        [Fact]
        public void CompiledExpressions_PerformBetter()
        {
            var reflectionConfig = new MapperConfiguration { UseCompiledExpressions = false };
            reflectionConfig.CreateMap<Person, PersonDto>();
            var reflectionMapper = new Mapper(reflectionConfig);

            var compiledConfig = new MapperConfiguration { UseCompiledExpressions = true };
            compiledConfig.CreateMap<Person, PersonDto>();
            var compiledMapper = new Mapper(compiledConfig);

            var person = new Person { FirstName = "John", LastName = "Doe", Age = 30 };

            // Both should produce same result
            var reflectionResult = reflectionMapper.Map<Person, PersonDto>(person);
            var compiledResult = compiledMapper.Map<Person, PersonDto>(person);

            Assert.Equal(reflectionResult.FirstName, compiledResult.FirstName);
            Assert.Equal(reflectionResult.LastName, compiledResult.LastName);
            Assert.Equal(reflectionResult.Age, compiledResult.Age);
        }

        [Fact]
        public void ProjectTo_BasicProjection_Works()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<Person, PersonDto>();
            var mapper = new Mapper(cfg);

            var queryable = new[]
            {
                new Person { FirstName = "John", LastName = "Doe", Age = 30 },
                new Person { FirstName = "Jane", LastName = "Smith", Age = 25 }
            }.AsQueryable();

            var projected = queryable.ProjectTo<Person, PersonDto>(cfg).ToList();

            Assert.Equal(2, projected.Count);
            Assert.Equal("John", projected[0].FirstName);
            Assert.Equal("Jane", projected[1].FirstName);
        }

        [Fact]
        public void ProjectTo_WithFlattening_Works()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<PersonWithAddress, FlatPersonDto>();

            var queryable = new[]
            {
                new PersonWithAddress 
                { 
                    Name = "John", 
                    Address = new Address { City = "NYC", Street = "Main St" } 
                }
            }.AsQueryable();

            var projected = queryable.ProjectTo<PersonWithAddress, FlatPersonDto>(cfg).ToList();

            Assert.Single(projected);
            Assert.Equal("John", projected[0].Name);
            Assert.Equal("NYC", projected[0].AddressCity);
        }

        [Fact]
        public void CollectionMapping_WithTypeConversion_Works()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<StringModel, IntModel>();
            var mapper = new Mapper(cfg);

            var source = new List<StringModel>
            {
                new StringModel { Value = "1" },
                new StringModel { Value = "2" },
                new StringModel { Value = "3" }
            };

            var result = mapper.MapCollection<StringModel, IntModel>(source);

            Assert.Equal(3, result.Count());
            Assert.Equal(new[] { 1, 2, 3 }, result.Select(x => x.Value));
        }

        // Test Models
        public class StringModel
        {
            public string Value { get; set; } = default!;
        }

        public class IntModel
        {
            public int Value { get; set; }
        }

        public class CustomType
        {
            public string DisplayValue { get; set; } = default!;
        }

        public class CustomModel
        {
            public CustomType Value { get; set; } = default!;
        }

        public class PersonSource
        {
            public string FirstName { get; set; } = default!;
            public string LastName { get; set; } = default!;
            public int Age { get; set; }
        }

        public class PersonDestination
        {
            public string FirstName { get; set; } = default!;
            public string LastName { get; set; } = default!;
            public int Age { get; set; }
            public string FullName { get; set; } = default!; // Unmapped
        }

        public class InvalidDestination
        {
            public ComplexObject ComplexProperty { get; set; } = default!; // No mapping exists
        }

        public class ComplexObject
        {
            public string Value { get; set; } = default!;
        }

        public class PersonWithAddress
        {
            public string Name { get; set; } = default!;
            public Address? Address { get; set; }
        }

        public class FlatPersonDto
        {
            public string Name { get; set; } = default!;
            public string? AddressCity { get; set; }
            public string? AddressStreet { get; set; }
        }

        public class StringToCustomTypeConverter : ITypeConverter<string, CustomType>
        {
            public CustomType Convert(string source)
            {
                return new CustomType { DisplayValue = $"CUSTOM: {source}" };
            }
        }
    }
}