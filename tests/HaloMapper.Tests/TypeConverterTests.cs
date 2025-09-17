using System;
using Xunit;
using HaloMapper.TypeConverters;

namespace HaloMapper.Tests
{
    public class TypeConverterTests
    {
        [Fact]
        public void TypeConverterRegistry_AddConverter_Generic_AddsSuccessfully()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            var converter = new CustomStringToIntConverter();

            // Act
            registry.AddConverter<string, int>(converter);

            // Assert
            Assert.True(registry.TryGetConverter(typeof(string), typeof(int), out var retrievedConverter));
            Assert.Same(converter, retrievedConverter);
        }

        [Fact]
        public void TypeConverterRegistry_AddConverter_WithTypes_AddsSuccessfully()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            var converter = new CustomStringToIntConverter();

            // Act
            registry.AddConverter(typeof(string), typeof(int), converter);

            // Assert
            Assert.True(registry.TryGetConverter(typeof(string), typeof(int), out var retrievedConverter));
            Assert.Same(converter, retrievedConverter);
        }

        [Fact]
        public void TypeConverterRegistry_TryGetConverter_WithNonExistentConverter_ReturnsFalse()
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.TryGetConverter(typeof(CustomType), typeof(AnotherCustomType), out var converter);

            // Assert
            Assert.False(result);
            Assert.Null(converter);
        }

        [Fact]
        public void TypeConverterRegistry_Convert_Generic_WithExistingConverter_ConvertsSuccessfully()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            var converter = new CustomStringToIntConverter();
            registry.AddConverter<string, int>(converter);

            // Act
            var result = registry.Convert<string, int>("42");

            // Assert
            Assert.Equal(142, result); // Custom converter adds 100
        }

        [Fact]
        public void TypeConverterRegistry_Convert_Generic_WithoutConverter_ThrowsInvalidOperationException()
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => registry.Convert<CustomType, AnotherCustomType>(new CustomType()));
        }

        [Fact]
        public void TypeConverterRegistry_Convert_Object_WithNullSource_ReturnsNull()
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(null!, typeof(string), typeof(int));

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TypeConverterRegistry_Convert_Object_WithSameType_ReturnsSameObject()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            var source = "test";

            // Act
            var result = registry.Convert(source, typeof(string), typeof(string));

            // Assert
            Assert.Same(source, result);
        }

        [Fact]
        public void TypeConverterRegistry_Convert_Object_WithAssignableType_ReturnsSameObject()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            var source = "test";

            // Act
            var result = registry.Convert(source, typeof(string), typeof(object));

            // Assert
            Assert.Same(source, result);
        }

        [Fact]
        public void TypeConverterRegistry_Convert_Object_WithNullableDestination_HandlesCorrectly()
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert("123", typeof(string), typeof(int?));

            // Assert
            Assert.Equal(123, result);
        }

        [Fact]
        public void TypeConverterRegistry_Convert_Object_WithNullableSource_HandlesCorrectly()
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(123, typeof(int?), typeof(string));

            // Assert
            Assert.Equal("123", result);
        }

        [Theory]
        [InlineData("123", 123)]
        [InlineData("0", 0)]
        [InlineData("-456", -456)]
        public void BuiltInConverter_StringToInt_ConvertsCorrectly(string input, int expected)
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(input, typeof(string), typeof(int));

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("123", 123L)]
        [InlineData("9223372036854775807", 9223372036854775807L)]
        public void BuiltInConverter_StringToLong_ConvertsCorrectly(string input, long expected)
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(input, typeof(string), typeof(long));

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("123.45", 123.45)]
        [InlineData("0.001", 0.001)]
        public void BuiltInConverter_StringToDecimal_ConvertsCorrectly(string input, double expected)
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(input, typeof(string), typeof(decimal));

            // Assert
            Assert.Equal((decimal)expected, result);
        }

        [Theory]
        [InlineData("123.45", 123.45)]
        [InlineData("0.001", 0.001)]
        public void BuiltInConverter_StringToDouble_ConvertsCorrectly(string input, double expected)
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(input, typeof(string), typeof(double));

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("123.45", 123.45f)]
        [InlineData("0.001", 0.001f)]
        public void BuiltInConverter_StringToFloat_ConvertsCorrectly(string input, float expected)
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(input, typeof(string), typeof(float));

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("True", true)]
        [InlineData("False", false)]
        public void BuiltInConverter_StringToBool_ConvertsCorrectly(string input, bool expected)
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(input, typeof(string), typeof(bool));

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BuiltInConverter_StringToDateTime_ConvertsCorrectly()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            var expected = new DateTime(2023, 1, 1);

            // Act
            var result = registry.Convert("2023-01-01", typeof(string), typeof(DateTime));

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BuiltInConverter_ObjectToString_ConvertsCorrectly()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            var source = 123;

            // Act
            var result = registry.Convert(source, typeof(int), typeof(string));

            // Assert
            Assert.Equal("123", result);
        }

        [Fact]
        public void BuiltInConverter_ObjectToString_WithNullObject_ReturnsEmptyString()
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(null!, typeof(object), typeof(string));

            // Assert
            Assert.Null(result); // Should return null for null input
        }

        [Theory]
        [InlineData("Value1", TestEnum.Value1)]
        [InlineData("Value2", TestEnum.Value2)]
        public void BuiltInConverter_StringToEnum_ConvertsCorrectly(string input, TestEnum expected)
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(input, typeof(string), typeof(TestEnum));

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0, TestEnum.Value1)]
        [InlineData(1, TestEnum.Value2)]
        public void BuiltInConverter_IntToEnum_ConvertsCorrectly(int input, TestEnum expected)
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert(input, typeof(int), typeof(TestEnum));

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TypeConverterRegistry_Convert_WithInvalidString_ReturnsNull()
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act
            var result = registry.Convert("invalid", typeof(string), typeof(int));

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void TypeConverterRegistry_Convert_WithCustomConverter_OverridesBuiltIn()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            var customConverter = new CustomStringToIntConverter();
            registry.AddConverter<string, int>(customConverter);

            // Act
            var result = registry.Convert("42", typeof(string), typeof(int));

            // Assert
            Assert.Equal(142, result); // Custom converter adds 100, built-in would return 42
        }

        [Fact]
        public void TypeConverterRegistry_ThreadSafety_MultipleThreadsAddingConverters()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

            // Act
            System.Threading.Tasks.Parallel.For(0, 100, i =>
            {
                try
                {
                    var converter = new CustomStringToIntConverter();
                    registry.AddConverter(typeof(string), typeof(CustomType), converter);
                    registry.TryGetConverter(typeof(string), typeof(CustomType), out _);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            // Assert
            Assert.Empty(exceptions);
        }

        [Fact]
        public void TypeConverterRegistry_Convert_WithComplexChain_WorksCorrectly()
        {
            // Arrange
            var registry = new TypeConverterRegistry();
            registry.AddConverter<CustomType, string>(new CustomTypeToStringConverter());
            registry.AddConverter<string, int>(new CustomStringToIntConverter());

            var source = new CustomType { Value = "10" };

            // Act
            var intermediateResult = registry.Convert(source, typeof(CustomType), typeof(string));
            var finalResult = registry.Convert(intermediateResult!, typeof(string), typeof(int));

            // Assert
            Assert.Equal("CustomType: 10", intermediateResult);
            Assert.Equal(110, finalResult); // Custom converter adds 100
        }

        [Fact]
        public void TypeConverterRegistry_BuiltInConversions_HandlesPrimitiveTypes()
        {
            // Arrange
            var registry = new TypeConverterRegistry();

            // Act & Assert
            Assert.Equal(42L, registry.Convert(42, typeof(int), typeof(long)));
            Assert.Equal(42.0, registry.Convert(42, typeof(int), typeof(double)));
            Assert.Equal(42.0f, registry.Convert(42, typeof(int), typeof(float)));
        }

        // Test classes and converters
        public enum TestEnum
        {
            Value1 = 0,
            Value2 = 1
        }

        public class CustomType
        {
            public string Value { get; set; } = default!;
        }

        public class AnotherCustomType
        {
            public string Data { get; set; } = default!;
        }

        public class CustomStringToIntConverter : ITypeConverter<string, int>
        {
            public int Convert(string source)
            {
                // Handle "CustomType: 10" format by extracting the number
                if (source.StartsWith("CustomType: "))
                {
                    var numberPart = source.Substring("CustomType: ".Length);
                    return int.Parse(numberPart) + 100;
                }
                return int.Parse(source) + 100; // Custom behavior: add 100
            }
        }

        public class CustomTypeToStringConverter : ITypeConverter<CustomType, string>
        {
            public string Convert(CustomType source)
            {
                return $"CustomType: {source.Value}";
            }
        }
    }
}