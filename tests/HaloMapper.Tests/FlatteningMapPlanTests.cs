using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace HaloMapper.Tests
{
    public class FlatteningMapPlanTests
    {
        [Fact]
        public void Map_WithBasicFlattening_FlattensCorrectly()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>
            {
                new FlatteningMapPlan.FlatteningMemberPlan
                {
                    DestinationName = "PersonName",
                    DestinationType = typeof(string),
                    SourcePath = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name"),
                    DestSetter = (dest, val) => ((FlatteningDestination)dest).PersonName = (string?)val
                },
                new FlatteningMapPlan.FlatteningMemberPlan
                {
                    DestinationName = "PersonAge",
                    DestinationType = typeof(int),
                    SourcePath = PropertyPath.Parse(typeof(FlatteningSource), "Person.Age"),
                    DestSetter = (dest, val) => ((FlatteningDestination)dest).PersonAge = (int)val!
                }
            };

            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, null, null, null);
            var source = new FlatteningSource
            {
                Person = new Person { Name = "John", Age = 30 }
            };

            // Act
            var result = (FlatteningDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("John", result.PersonName);
            Assert.Equal(30, result.PersonAge);
        }

        [Fact]
        public void Map_WithNullNestedObject_HandlesGracefully()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>
            {
                new FlatteningMapPlan.FlatteningMemberPlan
                {
                    DestinationName = "PersonName",
                    DestinationType = typeof(string),
                    SourcePath = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name"),
                    DestSetter = (dest, val) => ((FlatteningDestination)dest).PersonName = (string?)val
                }
            };

            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, null, null, null);
            var source = new FlatteningSource { Person = null };

            // Act
            var result = (FlatteningDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Null(result.PersonName);
        }

        [Fact]
        public void Map_WithNullSource_ThrowsArgumentNullException()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>();
            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, null, null, null);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => plan.Map(null!, null, CreateMockMapper()));
        }

        [Fact]
        public void Map_WithCustomConstructor_UsesConstructor()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>();
            Func<object> constructor = () => new FlatteningDestination { PersonName = "Constructed" };

            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, constructor, null, null);
            var source = new FlatteningSource();

            // Act
            var result = (FlatteningDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("Constructed", result.PersonName);
        }

        [Fact]
        public void Map_WithBeforeMapAction_ExecutesBeforeMapping()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>();
            bool beforeMapExecuted = false;
            Action<object, object> beforeMap = (source, dest) => beforeMapExecuted = true;

            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, null, beforeMap, null);
            var source = new FlatteningSource();

            // Act
            plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.True(beforeMapExecuted);
        }

        [Fact]
        public void Map_WithAfterMapAction_ExecutesAfterMapping()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>();
            bool afterMapExecuted = false;
            Action<object, object> afterMap = (source, dest) => afterMapExecuted = true;

            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, null, null, afterMap);
            var source = new FlatteningSource();

            // Act
            plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.True(afterMapExecuted);
        }

        [Fact]
        public void Map_WithIgnoredMember_SkipsIgnoredMember()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>
            {
                new FlatteningMapPlan.FlatteningMemberPlan
                {
                    DestinationName = "PersonName",
                    Ignore = true,
                    SourcePath = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name"),
                    DestSetter = (dest, val) => ((FlatteningDestination)dest).PersonName = (string?)val
                }
            };

            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, null, null, null);
            var source = new FlatteningSource { Person = new Person { Name = "John" } };

            // Act
            var result = (FlatteningDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Null(result.PersonName);
        }

        [Fact]
        public void Map_WithCondition_OnlyMapsWhenConditionIsTrue()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>
            {
                new FlatteningMapPlan.FlatteningMemberPlan
                {
                    DestinationName = "PersonName",
                    DestinationType = typeof(string),
                    SourcePath = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name"),
                    DestSetter = (dest, val) => ((FlatteningDestination)dest).PersonName = (string?)val,
                    Condition = (source, dest) => ((FlatteningSource)source).Person?.Age > 18
                }
            };

            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, null, null, null);

            // Test with condition = true
            var adultSource = new FlatteningSource { Person = new Person { Name = "Adult", Age = 25 } };
            var adultResult = (FlatteningDestination)plan.Map(adultSource, null, CreateMockMapper());
            Assert.Equal("Adult", adultResult.PersonName);

            // Test with condition = false
            var childSource = new FlatteningSource { Person = new Person { Name = "Child", Age = 10 } };
            var childResult = (FlatteningDestination)plan.Map(childSource, null, CreateMockMapper());
            Assert.Null(childResult.PersonName);
        }

        [Fact]
        public void Map_WithNullSubstitute_UsesSubstituteForNull()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>
            {
                new FlatteningMapPlan.FlatteningMemberPlan
                {
                    DestinationName = "PersonName",
                    DestinationType = typeof(string),
                    SourcePath = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name"),
                    DestSetter = (dest, val) => ((FlatteningDestination)dest).PersonName = (string?)val,
                    NullSubstitute = "Default Name"
                }
            };

            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, null, null, null);
            var source = new FlatteningSource { Person = null };

            // Act
            var result = (FlatteningDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("Default Name", result.PersonName);
        }

        [Fact]
        public void Map_WithCustomResolver_UsesResolver()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>
            {
                new FlatteningMapPlan.FlatteningMemberPlan
                {
                    DestinationName = "PersonName",
                    DestinationType = typeof(string),
                    DestSetter = (dest, val) => ((FlatteningDestination)dest).PersonName = (string?)val,
                    Resolver = (source, dest) => $"Resolved: {((FlatteningSource)source).Person?.Name}"
                }
            };

            var plan = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), memberPlans, null, null, null);
            var source = new FlatteningSource { Person = new Person { Name = "John" } };

            // Act
            var result = (FlatteningDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("Resolved: John", result.PersonName);
        }

        [Fact]
        public void Map_WithSourceGetter_UsesSourceGetter()
        {
            // Arrange
            var memberPlans = new List<FlatteningMapPlan.FlatteningMemberPlan>
            {
                new FlatteningMapPlan.FlatteningMemberPlan
                {
                    DestinationName = "PersonName",
                    DestinationType = typeof(string),
                    SourceGetter = (source) => $"Getter: {((FlatteningSource)source).Person?.Name}",
                    DestSetter = (dest, val) => ((FlatteningDestination)dest).PersonName = (string?)val
                }
            };

            var plan = new FlatteningMapPlan.FlatteningMemberPlan();
            plan.DestinationName = "PersonName";
            plan.DestinationType = typeof(string);
            plan.SourceGetter = (source) => $"Getter: {((FlatteningSource)source).Person?.Name}";
            plan.DestSetter = (dest, val) => ((FlatteningDestination)dest).PersonName = (string?)val;

            var planInstance = new FlatteningMapPlan(typeof(FlatteningSource), typeof(FlatteningDestination), new List<FlatteningMapPlan.FlatteningMemberPlan> { plan }, null, null, null);
            var source = new FlatteningSource { Person = new Person { Name = "John" } };

            // Act
            var result = (FlatteningDestination)planInstance.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("Getter: John", result.PersonName);
        }

        [Fact]
        public void PropertyPath_Parse_CreatesCorrectPath()
        {
            // Act
            var path = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name");

            // Assert
            Assert.Equal(2, path.Properties.Count);
            Assert.Equal("Person", path.Properties[0].Name);
            Assert.Equal("Name", path.Properties[1].Name);
            Assert.Equal(typeof(string), path.FinalType);
        }

        [Fact]
        public void PropertyPath_Parse_WithInvalidProperty_ThrowsArgumentException()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => PropertyPath.Parse(typeof(FlatteningSource), "Person.InvalidProperty"));
        }

        [Fact]
        public void PropertyPath_GetValue_ReturnsCorrectValue()
        {
            // Arrange
            var path = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name");
            var source = new FlatteningSource { Person = new Person { Name = "John" } };

            // Act
            var value = path.GetValue(source, CreateMockMapper());

            // Assert
            Assert.Equal("John", value);
        }

        [Fact]
        public void PropertyPath_GetValue_WithNullIntermediateObject_ReturnsNull()
        {
            // Arrange
            var path = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name");
            var source = new FlatteningSource { Person = null };

            // Act
            var value = path.GetValue(source, CreateMockMapper());

            // Assert
            Assert.Null(value);
        }

        [Fact]
        public void PropertyPath_ToString_ReturnsCorrectString()
        {
            // Arrange
            var path = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name");

            // Act
            var pathString = path.ToString();

            // Assert
            Assert.Equal("Person.Name", pathString);
        }

        [Fact]
        public void FlatteningMapPlanFactory_CreateFlatteningPlan_CreatesBasicPlan()
        {
            // Act
            var plan = FlatteningMapPlanFactory.CreateFlatteningPlan(
                typeof(FlatteningSource),
                typeof(SimpleDestination),
                null,
                null,
                null);

            // Assert
            Assert.NotNull(plan);
        }

        [Fact]
        public void FlatteningMapPlanFactory_FindsDirectPropertyMatch()
        {
            // Act
            var plan = FlatteningMapPlanFactory.CreateFlatteningPlan(
                typeof(DirectMatchSource),
                typeof(DirectMatchDestination),
                null,
                null,
                null);

            var source = new DirectMatchSource { Name = "Direct" };
            var result = (DirectMatchDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("Direct", result.Name);
        }

        [Fact]
        public void FlatteningMapPlanFactory_FindsFlatteningMatch()
        {
            // Act
            var plan = FlatteningMapPlanFactory.CreateFlatteningPlan(
                typeof(FlatteningSource),
                typeof(AutoFlatteningDestination),
                null,
                null,
                null);

            var source = new FlatteningSource { Person = new Person { Name = "Auto" } };
            var result = (AutoFlatteningDestination)plan.Map(source, null, CreateMockMapper());

            // Assert
            Assert.Equal("Auto", result.PersonName);
        }

        [Fact]
        public void FlatteningMemberPlan_GetSourceValue_ReturnsValueFromPath()
        {
            // Arrange
            var memberPlan = new FlatteningMapPlan.FlatteningMemberPlan
            {
                SourcePath = PropertyPath.Parse(typeof(FlatteningSource), "Person.Name")
            };
            var source = new FlatteningSource { Person = new Person { Name = "PathValue" } };

            // Act
            var value = memberPlan.GetSourceValue(source, CreateMockMapper());

            // Assert
            Assert.Equal("PathValue", value);
        }

        [Fact]
        public void FlatteningMemberPlan_SetDestinationValue_HandlesTypeConversion()
        {
            // Arrange
            var destination = new TypeConversionDestination();
            var memberPlan = new FlatteningMapPlan.FlatteningMemberPlan
            {
                DestinationType = typeof(int),
                DestSetter = (dest, val) => ((TypeConversionDestination)dest).IntValue = (int)val!
            };

            // Act
            memberPlan.SetDestinationValue(destination, "123", CreateMapperWithTypeConverter());

            // Assert
            Assert.Equal(123, destination.IntValue);
        }

        // Test classes
        public class FlatteningSource
        {
            public Person? Person { get; set; }
        }

        public class Person
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public class FlatteningDestination
        {
            public string? PersonName { get; set; }
            public int PersonAge { get; set; }
        }

        public class SimpleDestination
        {
            public string? Name { get; set; }
        }

        public class DirectMatchSource
        {
            public string? Name { get; set; }
        }

        public class DirectMatchDestination
        {
            public string? Name { get; set; }
        }

        public class AutoFlatteningDestination
        {
            public string? PersonName { get; set; }
        }

        public class TypeConversionDestination
        {
            public int IntValue { get; set; }
        }

        private static Mapper CreateMockMapper()
        {
            var config = new MapperConfiguration();
            return new Mapper(config);
        }

        private static Mapper CreateMapperWithTypeConverter()
        {
            var config = new MapperConfiguration();
            // Add built-in string to int converter
            return new Mapper(config);
        }
    }
}