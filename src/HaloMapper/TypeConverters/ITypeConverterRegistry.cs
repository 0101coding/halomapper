using System;

namespace HaloMapper.TypeConverters
{
    public interface ITypeConverterRegistry
    {
        void AddConverter<TSource, TDestination>(ITypeConverter<TSource, TDestination> converter);
        void AddConverter(Type sourceType, Type destinationType, object converter);
        bool TryGetConverter(Type sourceType, Type destinationType, out object? converter);
        TDestination Convert<TSource, TDestination>(TSource source);
        object? Convert(object source, Type sourceType, Type destinationType);
    }
}