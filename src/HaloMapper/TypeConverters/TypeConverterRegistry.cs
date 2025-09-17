using System;
using System.Collections.Concurrent;
using System.Globalization;

namespace HaloMapper.TypeConverters
{
    public class TypeConverterRegistry : ITypeConverterRegistry
    {
        private readonly ConcurrentDictionary<(Type Source, Type Destination), object> _converters = new();

        public TypeConverterRegistry()
        {
            RegisterBuiltInConverters();
        }

        public void AddConverter<TSource, TDestination>(ITypeConverter<TSource, TDestination> converter)
        {
            _converters[(typeof(TSource), typeof(TDestination))] = converter;
        }

        public void AddConverter(Type sourceType, Type destinationType, object converter)
        {
            _converters[(sourceType, destinationType)] = converter;
        }

        public bool TryGetConverter(Type sourceType, Type destinationType, out object? converter)
        {
            return _converters.TryGetValue((sourceType, destinationType), out converter);
        }

        public TDestination Convert<TSource, TDestination>(TSource source)
        {
            if (TryGetConverter(typeof(TSource), typeof(TDestination), out var converter))
            {
                var typedConverter = (ITypeConverter<TSource, TDestination>)converter!;
                return typedConverter.Convert(source);
            }

            throw new InvalidOperationException($"No converter found for {typeof(TSource).Name} -> {typeof(TDestination).Name}");
        }

        public object? Convert(object source, Type sourceType, Type destinationType)
        {
            if (source == null) return null;

            // Handle same type
            if (sourceType == destinationType || destinationType.IsAssignableFrom(sourceType))
            {
                return source;
            }

            // Handle nullable types
            var underlyingDestType = Nullable.GetUnderlyingType(destinationType);
            if (underlyingDestType != null)
            {
                if (source == null) return null;
                return Convert(source, sourceType, underlyingDestType);
            }

            var underlyingSourceType = Nullable.GetUnderlyingType(sourceType);
            if (underlyingSourceType != null)
            {
                return Convert(source, underlyingSourceType, destinationType);
            }

            // Try registered converters
            if (TryGetConverter(sourceType, destinationType, out var converter))
            {
                try
                {
                    var convertMethod = converter!.GetType().GetMethod("Convert");
                    return convertMethod?.Invoke(converter, new[] { source });
                }
                catch
                {
                    // If conversion fails, return null
                    return null;
                }
            }

            // Try built-in conversions
            return TryBuiltInConversion(source, sourceType, destinationType);
        }

        private void RegisterBuiltInConverters()
        {
            // String conversions
            AddConverter(typeof(object), typeof(string), new ObjectToStringConverter());
            
            // Numeric conversions
            AddConverter(typeof(string), typeof(int), new StringToIntConverter());
            AddConverter(typeof(string), typeof(long), new StringToLongConverter());
            AddConverter(typeof(string), typeof(decimal), new StringToDecimalConverter());
            AddConverter(typeof(string), typeof(double), new StringToDoubleConverter());
            AddConverter(typeof(string), typeof(float), new StringToFloatConverter());
            
            // DateTime conversions
            AddConverter(typeof(string), typeof(DateTime), new StringToDateTimeConverter());
            
            // Bool conversions
            AddConverter(typeof(string), typeof(bool), new StringToBoolConverter());
            
            // Enum conversions
            AddConverter(typeof(string), typeof(Enum), new StringToEnumConverter());
        }

        private object? TryBuiltInConversion(object source, Type sourceType, Type destinationType)
        {
            try
            {
                // Try System.Convert for basic types
                if (destinationType.IsPrimitive || destinationType == typeof(string) || destinationType == typeof(DateTime) || destinationType == typeof(decimal))
                {
                    return System.Convert.ChangeType(source, destinationType, CultureInfo.InvariantCulture);
                }

                // Handle enums
                if (destinationType.IsEnum)
                {
                    if (sourceType == typeof(string))
                    {
                        return Enum.Parse(destinationType, source.ToString()!);
                    }
                    if (sourceType.IsPrimitive)
                    {
                        return Enum.ToObject(destinationType, source);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    // Built-in converters
    internal class ObjectToStringConverter : ITypeConverter<object, string>
    {
        public string Convert(object source) => source?.ToString() ?? string.Empty;
    }

    internal class StringToIntConverter : ITypeConverter<string, int>
    {
        public int Convert(string source) => int.Parse(source, CultureInfo.InvariantCulture);
    }

    internal class StringToLongConverter : ITypeConverter<string, long>
    {
        public long Convert(string source) => long.Parse(source, CultureInfo.InvariantCulture);
    }

    internal class StringToDecimalConverter : ITypeConverter<string, decimal>
    {
        public decimal Convert(string source) => decimal.Parse(source, CultureInfo.InvariantCulture);
    }

    internal class StringToDoubleConverter : ITypeConverter<string, double>
    {
        public double Convert(string source) => double.Parse(source, CultureInfo.InvariantCulture);
    }

    internal class StringToFloatConverter : ITypeConverter<string, float>
    {
        public float Convert(string source) => float.Parse(source, CultureInfo.InvariantCulture);
    }

    internal class StringToDateTimeConverter : ITypeConverter<string, DateTime>
    {
        public DateTime Convert(string source) => DateTime.Parse(source, CultureInfo.InvariantCulture);
    }

    internal class StringToBoolConverter : ITypeConverter<string, bool>
    {
        public bool Convert(string source) => bool.Parse(source);
    }

    internal class StringToEnumConverter : ITypeConverter<string, Enum>
    {
        public Enum Convert(string source) => throw new NotSupportedException("Use generic enum converter");
    }
}