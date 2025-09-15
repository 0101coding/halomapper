using System;
using System.Collections.Generic;
using Xunit;

namespace HaloMapper.Tests
{
    public class CompiledMapPlanTests
    {
        [Fact]
        public void Map_WithValidSourceDestination_ReturnsCorrectMapping()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>
            {
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Name",
                    SourceGetter = source => ((TestSource)source).Name,
                    DestSetter = (dest, value) => ((TestDestination)dest).Name = (string?)value,
                    Ignore = false
                },
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Age",
                    SourceGetter = source => ((TestSource)source).Age,
                    DestSetter = (dest, value) => ((TestDestination)dest).Age = (int)value!,
                    Ignore = false
                }
            };

            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, null, null, null);
            var source = new TestSource { Name = "John", Age = 30 };

            // Act
            var result = (TestDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.NotNull(result);
            Assert.Equal("John", result.Name);
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public void Map_WithNullSource_ThrowsArgumentNullException()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>();
            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, null, null, null);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => plan.Map(null!, null, CreateMockMapper()));
        }

        [Fact]
        public void Map_WithExistingDestination_PopulatesExistingObject()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>
            {
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Name",
                    SourceGetter = source => ((TestSource)source).Name,
                    DestSetter = (dest, value) => ((TestDestination)dest).Name = (string?)value,
                    Ignore = false
                }
            };

            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, null, null, null);
            var source = new TestSource { Name = "Jane" };
            var destination = new TestDestination { Name = "Original", Age = 25 };

            // Act
            var result = (TestDestination)plan.Map(source, destination, CreateMockMapper());

            // Assert
            Assert.Same(destination, result);
            Assert.Equal("Jane", result.Name);
            Assert.Equal(25, result.Age); // Should preserve existing values not being mapped
        }

        [Fact]
        public void Map_WithCustomConstructor_UsesConstructor()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>();
            Func<object> constructor = () => new TestDestination { Name = "Constructed", Age = 99 };

            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, constructor, null, null);
            var source = new TestSource { Name = "John", Age = 30 };

            // Act
            var result = (TestDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("Constructed", result.Name);
            Assert.Equal(99, result.Age);
        }

        [Fact]
        public void Map_WithBeforeMapAction_ExecutesBeforeMapping()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>
            {
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Name",
                    SourceGetter = source => ((TestSource)source).Name,
                    DestSetter = (dest, value) => ((TestDestination)dest).Name = (string?)value,
                    Ignore = false
                }
            };

            Action<object, object> beforeMap = (source, dest) =>
                ((TestDestination)dest).Name = "BeforeMap";

            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, null, beforeMap, null);
            var source = new TestSource { Name = "Original" };

            // Act
            var result = (TestDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("Original", result.Name); // Member mapping should override beforeMap
        }

        [Fact]
        public void Map_WithAfterMapAction_ExecutesAfterMapping()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>
            {
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Name",
                    SourceGetter = source => ((TestSource)source).Name,
                    DestSetter = (dest, value) => ((TestDestination)dest).Name = (string?)value,
                    Ignore = false
                }
            };

            Action<object, object> afterMap = (source, dest) =>
                ((TestDestination)dest).Name = "AfterMap";

            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, null, null, afterMap);
            var source = new TestSource { Name = "Original" };

            // Act
            var result = (TestDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("AfterMap", result.Name); // AfterMap should override member mapping
        }

        [Fact]
        public void Map_WithIgnoredMembers_SkipsIgnoredMembers()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>
            {
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Name",
                    SourceGetter = source => ((TestSource)source).Name,
                    DestSetter = (dest, value) => ((TestDestination)dest).Name = (string?)value,
                    Ignore = true
                },
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Age",
                    SourceGetter = source => ((TestSource)source).Age,
                    DestSetter = (dest, value) => ((TestDestination)dest).Age = (int)value!,
                    Ignore = false
                }
            };

            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, null, null, null);
            var source = new TestSource { Name = "John", Age = 30 };

            // Act
            var result = (TestDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Null(result.Name); // Should be null/default since ignored
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public void Map_WithCustomResolver_UsesResolver()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>
            {
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Name",
                    Resolver = (source, dest) => $"Resolved: {((TestSource)source).Name}",
                    DestSetter = (dest, value) => ((TestDestination)dest).Name = (string?)value,
                    Ignore = false
                }
            };

            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, null, null, null);
            var source = new TestSource { Name = "John" };

            // Act
            var result = (TestDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("Resolved: John", result.Name);
        }

        [Fact]
        public void Map_WithCondition_OnlyMapsWhenConditionIsTrue()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>
            {
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Name",
                    SourceGetter = source => ((TestSource)source).Name,
                    DestSetter = (dest, value) => ((TestDestination)dest).Name = (string?)value,
                    Condition = (source, dest) => ((TestSource)source).Age > 18,
                    Ignore = false
                }
            };

            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, null, null, null);

            // Test with condition = true
            var adultSource = new TestSource { Name = "Adult", Age = 25 };
            var adultResult = (TestDestination)plan.Map(adultSource, null, CreateMockMapper());
            Assert.Equal("Adult", adultResult.Name);

            // Test with condition = false
            var childSource = new TestSource { Name = "Child", Age = 10 };
            var childResult = (TestDestination)plan.Map(childSource, null, CreateMockMapper());
            Assert.Null(childResult.Name);
        }

        [Fact]
        public void Map_WithMemberPlanHavingNullDestSetter_SkipsMember()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>
            {
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Name",
                    SourceGetter = source => ((TestSource)source).Name,
                    DestSetter = null, // Null setter
                    Ignore = false
                },
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "Age",
                    SourceGetter = source => ((TestSource)source).Age,
                    DestSetter = (dest, value) => ((TestDestination)dest).Age = (int)value!,
                    Ignore = false
                }
            };

            var plan = new CompiledMapPlan<TestSource, TestDestination>(memberPlans, null, null, null);
            var source = new TestSource { Name = "John", Age = 30 };

            // Act
            var result = (TestDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Null(result.Name); // Should be null since setter was null
            Assert.Equal(30, result.Age);
        }

        [Fact]
        public void Map_WithComplexNestedMapping_WorksCorrectly()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>
            {
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "PersonName",
                    SourceGetter = source => ((ComplexSource)source).Person?.Name,
                    DestSetter = (dest, value) => ((ComplexDestination)dest).PersonName = (string?)value,
                    Ignore = false
                },
                new ReflectionMapPlan.MemberPlan
                {
                    DestinationName = "AddressCity",
                    SourceGetter = source => ((ComplexSource)source).Address?.City,
                    DestSetter = (dest, value) => ((ComplexDestination)dest).AddressCity = (string?)value,
                    Ignore = false
                }
            };

            var plan = new CompiledMapPlan<ComplexSource, ComplexDestination>(memberPlans, null, null, null);
            var source = new ComplexSource
            {
                Person = new TestSource { Name = "John" },
                Address = new Address { City = "New York" }
            };

            // Act
            var result = (ComplexDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("John", result.PersonName);
            Assert.Equal("New York", result.AddressCity);
        }

        [Fact]
        public void CompiledMapPlanFactory_CreateCompiledPlan_CreatesCorrectPlanType()
        {
            // Arrange
            var memberPlans = new List<ReflectionMapPlan.MemberPlan>();

            // Act
            var plan = CompiledMapPlanFactory.CreateCompiledPlan(
                typeof(TestSource),
                typeof(TestDestination),
                memberPlans,
                null,
                null,
                null);

            // Assert
            Assert.NotNull(plan);
            Assert.IsAssignableFrom<IMapPlan>(plan);
        }

        // Test classes
        public class TestSource
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public class TestDestination
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public class ComplexSource
        {
            public TestSource? Person { get; set; }
            public Address? Address { get; set; }
        }

        public class ComplexDestination
        {
            public string? PersonName { get; set; }
            public string? AddressCity { get; set; }
        }

        public class Address
        {
            public string? City { get; set; }
        }

        private static Mapper CreateMockMapper()
        {
            var config = new MapperConfiguration();
            return new Mapper(config);
        }
    }
}