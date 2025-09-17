using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Xunit;
using HaloMapper.Extensions;
using HaloMapper.Queryable;

namespace HaloMapper.Tests
{
    public class QueryableExtensionsTests
    {
        [Fact]
        public void ProjectTo_Generic_WithNullSource_ThrowsArgumentNullException()
        {
            // Arrange
            IQueryable<QueryableTestSource> source = null!;
            var config = new MapperConfiguration();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => source.ProjectTo<QueryableTestSource, QueryableTestDestination>(config));
        }

        [Fact]
        public void ProjectTo_Generic_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Arrange
            var source = new[] { new QueryableTestSource { Name = "Test" } }.AsQueryable();
            MapperConfiguration config = null!;

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => source.ProjectTo<QueryableTestSource, QueryableTestDestination>(config));
        }

        [Fact]
        public void ProjectTo_Generic_WithBasicMapping_ProjectsCorrectly()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableTestSource, QueryableTestDestination>();

            var sourceData = new[]
            {
                new QueryableTestSource { Name = "John", Age = 30 },
                new QueryableTestSource { Name = "Jane", Age = 25 }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryableTestSource, QueryableTestDestination>(config).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("John", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.Equal("Jane", result[1].Name);
            Assert.Equal(25, result[1].Age);
        }

        [Fact]
        public void ProjectTo_NonGeneric_WithBasicMapping_ProjectsCorrectly()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableTestSource, QueryableTestDestination>();

            var sourceData = new[]
            {
                new QueryableTestSource { Name = "Test1" },
                new QueryableTestSource { Name = "Test2" }
            }.AsQueryable(); // Non-generic queryable (but maintains type info)

            // Act
            var result = sourceData.ProjectTo<QueryableTestDestination>(config).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Test1", result[0].Name);
            Assert.Equal("Test2", result[1].Name);
        }

        [Fact]
        public void ProjectTo_WithFlattening_FlattensNestedProperties()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableNestedSource, QueryableFlatDestination>();

            var sourceData = new[]
            {
                new QueryableNestedSource
                {
                    Name = "Parent",
                    Child = new QueryableChild { Name = "Child1", Value = 100 }
                },
                new QueryableNestedSource
                {
                    Name = "Parent2",
                    Child = new QueryableChild { Name = "Child2", Value = 200 }
                }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryableNestedSource, QueryableFlatDestination>(config).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Parent", result[0].Name);
            Assert.Equal("Child1", result[0].ChildName);
            Assert.Equal(100, result[0].ChildValue);
            Assert.Equal("Parent2", result[1].Name);
            Assert.Equal("Child2", result[1].ChildName);
            Assert.Equal(200, result[1].ChildValue);
        }

        [Fact]
        public void ProjectTo_WithNullNestedObjects_HandlesGracefully()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableNestedSource, QueryableFlatDestination>();

            var sourceData = new[]
            {
                new QueryableNestedSource { Name = "Parent", Child = null }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryableNestedSource, QueryableFlatDestination>(config).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("Parent", result[0].Name);
            Assert.Null(result[0].ChildName);
            Assert.Equal(0, result[0].ChildValue);
        }

        [Fact]
        public void ProjectTo_WithTypeConversion_ConvertsTypes()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableStringSource, QueryableIntDestination>();

            var sourceData = new[]
            {
                new QueryableStringSource { Value = "123" },
                new QueryableStringSource { Value = "456" }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryableStringSource, QueryableIntDestination>(config).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(123, result[0].Value);
            Assert.Equal(456, result[1].Value);
        }

        [Fact]
        public void ProjectTo_WithCollectionMapping_ProjectsCollections()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableChild, QueryableChildDto>();
            config.CreateMap<QueryableCollectionSource, QueryableCollectionDestination>();

            var sourceData = new[]
            {
                new QueryableCollectionSource
                {
                    Name = "Parent1",
                    Children = new List<QueryableChild>
                    {
                        new QueryableChild { Name = "Child1", Value = 10 },
                        new QueryableChild { Name = "Child2", Value = 20 }
                    }
                }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryableCollectionSource, QueryableCollectionDestination>(config).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("Parent1", result[0].Name);
            Assert.Equal(2, result[0].Children?.Count());
            Assert.Equal("Child1", result[0].Children?.First().Name);
            Assert.Equal(10, result[0].Children?.First().Value);
        }

        [Fact]
        public void ProjectTo_WithMembersToExpand_UsesBasicProjection()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableTestSource, QueryableTestDestination>();

            var sourceData = new[]
            {
                new QueryableTestSource { Name = "Test", Age = 30 }
            }.AsQueryable();

            // Act - Using membersToExpand parameter (should be ignored for now)
            var result = sourceData.ProjectTo<QueryableTestSource, QueryableTestDestination>(
                config,
                dest => dest.Name,
                dest => dest.Age
            ).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("Test", result[0].Name);
            Assert.Equal(30, result[0].Age);
        }

        [Fact]
        public void ProjectTo_WithComplexNestedMapping_ProjectsCorrectly()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableAddress, QueryableAddressDto>();
            config.CreateMap<QueryablePerson, QueryablePersonDto>();

            var sourceData = new[]
            {
                new QueryablePerson
                {
                    Name = "John",
                    Age = 30,
                    Address = new QueryableAddress { Street = "123 Main St", City = "NYC" }
                }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryablePerson, QueryablePersonDto>(config).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("John", result[0].Name);
            Assert.Equal(30, result[0].Age);
            Assert.NotNull(result[0].Address);
            Assert.Equal("123 Main St", result[0].Address.Street);
            Assert.Equal("NYC", result[0].Address.City);
        }

        [Fact]
        public void ProjectTo_WithNullableTypes_HandlesNullables()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableNullableSource, QueryableNullableDestination>();

            var sourceData = new[]
            {
                new QueryableNullableSource { Value = 42 },
                new QueryableNullableSource { Value = null }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryableNullableSource, QueryableNullableDestination>(config).ToList();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(42, result[0].Value);
            Assert.Null(result[1].Value);
        }

        [Fact]
        public void ProjectTo_WithArrayMapping_ProjectsArrays()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableArraySource, QueryableArrayDestination>();

            var sourceData = new[]
            {
                new QueryableArraySource { Values = new[] { 1, 2, 3 } }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryableArraySource, QueryableArrayDestination>(config).ToList();

            // Assert
            Assert.Single(result);
            Assert.NotNull(result[0].Values);
            Assert.Equal(new[] { 1, 2, 3 }, result[0].Values);
        }

        [Fact]
        public void ProjectionExpression_CreateProjectionExpression_CreatesValidExpression()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableTestSource, QueryableTestDestination>();

            // Act
            var expression = ProjectionExpression.CreateProjectionExpression<QueryableTestSource, QueryableTestDestination>(config);

            // Assert
            Assert.NotNull(expression);
            Assert.Equal(typeof(Func<QueryableTestSource, QueryableTestDestination>), expression.Type);
        }

        [Fact]
        public void ProjectTo_WithCustomTypeConverter_UsesConverter()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.AddTypeConverter<string, CustomQueryableType>(new StringToCustomTypeConverter());
            config.CreateMap<QueryableCustomSource, QueryableCustomDestination>();

            var sourceData = new[]
            {
                new QueryableCustomSource { Value = "Test" }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryableCustomSource, QueryableCustomDestination>(config).ToList();

            // Assert
            Assert.Single(result);
            Assert.NotNull(result[0].Value);
            Assert.Equal("CUSTOM: Test", result[0].Value.DisplayValue);
        }

        [Fact]
        public void ProjectTo_WithInheritedProperties_MapsInheritedProperties()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<QueryableDerivedSource, QueryableDerivedDestination>();

            var sourceData = new[]
            {
                new QueryableDerivedSource { BaseProperty = "Base", DerivedProperty = "Derived" }
            }.AsQueryable();

            // Act
            var result = sourceData.ProjectTo<QueryableDerivedSource, QueryableDerivedDestination>(config).ToList();

            // Assert
            Assert.Single(result);
            Assert.Equal("Base", result[0].BaseProperty);
            Assert.Equal("Derived", result[0].DerivedProperty);
        }

        // Test classes
        public class QueryableTestSource
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public class QueryableTestDestination
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public class QueryableChild
        {
            public string? Name { get; set; }
            public int Value { get; set; }
        }

        public class QueryableChildDto
        {
            public string? Name { get; set; }
            public int Value { get; set; }
        }

        public class QueryableNestedSource
        {
            public string? Name { get; set; }
            public QueryableChild? Child { get; set; }
        }

        public class QueryableFlatDestination
        {
            public string? Name { get; set; }
            public string? ChildName { get; set; }
            public int ChildValue { get; set; }
        }

        public class QueryableStringSource
        {
            public string Value { get; set; } = default!;
        }

        public class QueryableIntDestination
        {
            public int Value { get; set; }
        }

        public class QueryableCollectionSource
        {
            public string? Name { get; set; }
            public List<QueryableChild>? Children { get; set; }
        }

        public class QueryableCollectionDestination
        {
            public string? Name { get; set; }
            public IEnumerable<QueryableChildDto>? Children { get; set; }
        }

        public class QueryableAddress
        {
            public string? Street { get; set; }
            public string? City { get; set; }
        }

        public class QueryableAddressDto
        {
            public string? Street { get; set; }
            public string? City { get; set; }
        }

        public class QueryablePerson
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            public QueryableAddress? Address { get; set; }
        }

        public class QueryablePersonDto
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            public QueryableAddressDto? Address { get; set; }
        }

        public class QueryableNullableSource
        {
            public int? Value { get; set; }
        }

        public class QueryableNullableDestination
        {
            public int? Value { get; set; }
        }

        public class QueryableArraySource
        {
            public int[]? Values { get; set; }
        }

        public class QueryableArrayDestination
        {
            public int[]? Values { get; set; }
        }

        public class CustomQueryableType
        {
            public string DisplayValue { get; set; } = default!;
        }

        public class QueryableCustomSource
        {
            public string Value { get; set; } = default!;
        }

        public class QueryableCustomDestination
        {
            public CustomQueryableType Value { get; set; } = default!;
        }

        public class QueryableBaseSource
        {
            public string? BaseProperty { get; set; }
        }

        public class QueryableBaseDestination
        {
            public string? BaseProperty { get; set; }
        }

        public class QueryableDerivedSource : QueryableBaseSource
        {
            public string? DerivedProperty { get; set; }
        }

        public class QueryableDerivedDestination : QueryableBaseDestination
        {
            public string? DerivedProperty { get; set; }
        }

        public class StringToCustomTypeConverter : ITypeConverter<string, CustomQueryableType>
        {
            public CustomQueryableType Convert(string source)
            {
                return new CustomQueryableType { DisplayValue = $"CUSTOM: {source}" };
            }
        }
    }
}