using System;
using System.Linq;
using Xunit;
using HaloMapper.Validation;

namespace HaloMapper.Tests
{
    public class ValidationTests
    {
        [Fact]
        public void ValidationResult_IsValid_WithNoErrors_ReturnsTrue()
        {
            // Arrange
            var result = new ValidationResult();

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ValidationResult_IsValid_WithErrors_ReturnsFalse()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddError("Test error");

            // Assert
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ValidationResult_AddError_AddsErrorToCollection()
        {
            // Arrange
            var result = new ValidationResult();

            // Act
            result.AddError("Test error", typeof(string), typeof(int), "Member");

            // Assert
            Assert.Single(result.Errors);
            Assert.Equal("Test error", result.Errors[0].Message);
            Assert.Equal(typeof(string), result.Errors[0].SourceType);
            Assert.Equal(typeof(int), result.Errors[0].DestinationType);
            Assert.Equal("Member", result.Errors[0].MemberName);
        }

        [Fact]
        public void ValidationResult_AddWarning_AddsWarningToCollection()
        {
            // Arrange
            var result = new ValidationResult();

            // Act
            result.AddWarning("Test warning", typeof(string), typeof(int), "Member");

            // Assert
            Assert.Single(result.Warnings);
            Assert.Equal("Test warning", result.Warnings[0].Message);
            Assert.Equal(typeof(string), result.Warnings[0].SourceType);
            Assert.Equal(typeof(int), result.Warnings[0].DestinationType);
            Assert.Equal("Member", result.Warnings[0].MemberName);
        }

        [Fact]
        public void ValidationResult_ToString_FormatsErrorsAndWarningsCorrectly()
        {
            // Arrange
            var result = new ValidationResult();
            result.AddError("Error message", typeof(Source), typeof(Destination), "Property");
            result.AddWarning("Warning message", typeof(Source), typeof(Destination), "Property");

            // Act
            var output = result.ToString();

            // Assert
            Assert.Contains("ERRORS:", output);
            Assert.Contains("Error message [Source -> Destination.Property]", output);
            Assert.Contains("WARNINGS:", output);
            Assert.Contains("Warning message [Source -> Destination.Property]", output);
        }

        [Fact]
        public void ValidationError_ToString_FormatsCorrectly()
        {
            // Arrange
            var error = new ValidationError("Test message", typeof(Source), typeof(Destination), "Property");

            // Act
            var result = error.ToString();

            // Assert
            Assert.Equal("Test message [Source -> Destination.Property]", result);
        }

        [Fact]
        public void ValidationError_ToString_WithoutMemberName_FormatsCorrectly()
        {
            // Arrange
            var error = new ValidationError("Test message", typeof(Source), typeof(Destination));

            // Act
            var result = error.ToString();

            // Assert
            Assert.Equal("Test message [Source -> Destination]", result);
        }

        [Fact]
        public void ValidationWarning_ToString_FormatsCorrectly()
        {
            // Arrange
            var warning = new ValidationWarning("Test message", typeof(Source), typeof(Destination), "Property");

            // Act
            var result = warning.ToString();

            // Assert
            Assert.Equal("Test message [Source -> Destination.Property]", result);
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithMissingMapping_ReturnsError()
        {
            // Arrange
            var config = new MapperConfiguration();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<Source, Destination>();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Message.Contains("No mapping configuration found"));
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithValidMapping_ReturnsValid()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<ValidSource, ValidDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<ValidSource, ValidDestination>();

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithUnmappedDestinationMembers_ReturnsWarnings()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<PartialSource, PartialDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<PartialSource, PartialDestination>();

            // Assert
            Assert.True(result.IsValid);
            Assert.Contains(result.Warnings, w => w.Message.Contains("Unmapped destination member"));
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithComplexUnmappedType_ReturnsError()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<SimpleSource, ComplexDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<SimpleSource, ComplexDestination>();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Message.Contains("Cannot map complex destination member"));
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithIncompatibleTypes_ReturnsError()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<IncompatibleSource, IncompatibleDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<IncompatibleSource, IncompatibleDestination>();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Message.Contains("Cannot map property"));
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithNullableCompatibility_ReturnsValid()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<NullableSource, NullableDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<NullableSource, NullableDestination>();

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithTypeConverter_ReturnsValid()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.AddTypeConverter<string, int>(new StringToIntTypeConverter());
            config.CreateMap<StringSource, IntDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<StringSource, IntDestination>();

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithNestedMapping_ReturnsValid()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<NestedChild, NestedChildDto>();
            config.CreateMap<NestedParent, NestedParentDto>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<NestedParent, NestedParentDto>();

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithFlattening_DetectsFlattening()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<FlatteningSource, FlatteningDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<FlatteningSource, FlatteningDestination>();

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ConfigurationValidator_ValidateAll_ValidatesAllRegisteredMappings()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<ValidSource, ValidDestination>();
            config.CreateMap<PartialSource, PartialDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateAll();

            // Assert
            Assert.True(result.IsValid);
            Assert.True(result.Warnings.Any()); // Should have warnings from PartialSource mapping
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_SameTypeValidation_DoesNotDuplicate()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<ValidSource, ValidDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result1 = validator.ValidateMapping<ValidSource, ValidDestination>();
            var result2 = validator.ValidateMapping<ValidSource, ValidDestination>();

            // Assert
            Assert.True(result1.IsValid);
            Assert.True(result2.IsValid);
            Assert.Empty(result2.Errors); // Second validation should not duplicate errors
        }

        [Fact]
        public void ConfigurationValidator_ValidateMapping_WithSystemConvertibleTypes_ReturnsValid()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<ConvertibleSource, ConvertibleDestination>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateMapping<ConvertibleSource, ConvertibleDestination>();

            // Assert
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ConfigurationValidator_CheckCircularReferences_DetectsCircularReference()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<CircularA, CircularB>();
            config.CreateMap<CircularB, CircularA>();
            var validator = new ConfigurationValidator(config);

            // Act
            var result = validator.ValidateAll();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Message.Contains("Circular reference detected"));
        }

        // Test classes
        public class Source
        {
            public string? Name { get; set; }
        }

        public class Destination
        {
            public string? Name { get; set; }
        }

        public class ValidSource
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public class ValidDestination
        {
            public string? Name { get; set; }
            public int Age { get; set; }
        }

        public class PartialSource
        {
            public string? Name { get; set; }
        }

        public class PartialDestination
        {
            public string? Name { get; set; }
            public int Age { get; set; } // Unmapped
        }

        public class SimpleSource
        {
            public string? Name { get; set; }
        }

        public class ComplexDestination
        {
            public string? Name { get; set; }
            public ComplexType? Complex { get; set; } // Complex unmapped type
        }

        public class ComplexType
        {
            public string? Value { get; set; }
        }

        public class IncompatibleSource
        {
            public string? Name { get; set; }
        }

        public class IncompatibleDestination
        {
            public DateTime Name { get; set; } // Incompatible type
        }

        public class NullableSource
        {
            public int? Value { get; set; }
        }

        public class NullableDestination
        {
            public int Value { get; set; }
        }

        public class StringSource
        {
            public string? Value { get; set; }
        }

        public class IntDestination
        {
            public int Value { get; set; }
        }

        public class NestedChild
        {
            public string? Name { get; set; }
        }

        public class NestedChildDto
        {
            public string? Name { get; set; }
        }

        public class NestedParent
        {
            public string? Name { get; set; }
            public NestedChild? Child { get; set; }
        }

        public class NestedParentDto
        {
            public string? Name { get; set; }
            public NestedChildDto? Child { get; set; }
        }

        public class FlatteningSource
        {
            public string? Name { get; set; }
            public NestedChild? Child { get; set; }
        }

        public class FlatteningDestination
        {
            public string? Name { get; set; }
            public string? ChildName { get; set; }
        }

        public class ConvertibleSource
        {
            public int Value { get; set; }
        }

        public class ConvertibleDestination
        {
            public long Value { get; set; }
        }

        public class CircularA
        {
            public string? Name { get; set; }
            public CircularB? B { get; set; }
        }

        public class CircularB
        {
            public string? Name { get; set; }
            public CircularA? A { get; set; }
        }

        public class StringToIntTypeConverter : ITypeConverter<string, int>
        {
            public int Convert(string source)
            {
                return int.Parse(source);
            }
        }
    }
}