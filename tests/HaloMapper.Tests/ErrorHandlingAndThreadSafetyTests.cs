using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HaloMapper.Tests
{
    public class ErrorHandlingAndThreadSafetyTests
    {
        [Fact]
        public void Mapper_Map_WithNullSource_ThrowsArgumentNullException()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<ErrorTestSource, ErrorTestDestination>();
            var mapper = new Mapper(config);

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => mapper.Map<ErrorTestSource, ErrorTestDestination>(null!));
        }

        [Fact]
        public void Mapper_Map_WithUnregisteredMapping_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new MapperConfiguration();
            var mapper = new Mapper(config);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
                mapper.Map<UnregisteredSource, UnregisteredDestination>(new UnregisteredSource()));
        }

        [Fact]
        public void Mapper_Map_WithCircularReference_HandlesGracefully()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<CircularParent, CircularParentDto>();
            config.CreateMap<CircularChild, CircularChildDto>();
            var mapper = new Mapper(config);

            var parent = new CircularParent { Name = "Parent" };
            var child = new CircularChild { Name = "Child", Parent = parent };
            parent.Child = child;

            // Act - Should handle circular reference without infinite recursion
            var result = mapper.Map<CircularParent, CircularParentDto>(parent);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Parent", result.Name);
            // Child mapping should work up to reasonable depth
        }

        [Fact]
        public void Mapper_Map_WithInvalidTypeConversion_HandlesGracefully()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<InvalidConversionSource, InvalidConversionDestination>();
            var mapper = new Mapper(config);

            var source = new InvalidConversionSource { Value = "NotADateTime" };

            // Act & Assert - Should handle gracefully or throw meaningful exception
            var ex = Assert.ThrowsAny<Exception>(() => mapper.Map<InvalidConversionSource, InvalidConversionDestination>(source));
            Assert.NotNull(ex);
        }

        [Fact]
        public void Mapper_Map_WithLargeObjectGraph_DoesNotStackOverflow()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<DeepNestingSource, DeepNestingDestination>();
            var mapper = new Mapper(config);

            // Create deeply nested object (but not infinite)
            var source = CreateDeepNestedObject(100);

            // Act - Should not cause stack overflow
            var result = mapper.Map<DeepNestingSource, DeepNestingDestination>(source);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Level_0", result.Name);
        }

        [Fact]
        public void Mapper_ThreadSafety_MultipleThreadsMapping_WorksConcurrently()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<ThreadSafetySource, ThreadSafetyDestination>();
            var mapper = new Mapper(config);

            var exceptions = new ConcurrentBag<Exception>();
            var results = new ConcurrentBag<ThreadSafetyDestination>();
            const int threadCount = 10;
            const int itemsPerThread = 100;

            // Act
            var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
                Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < itemsPerThread; i++)
                        {
                            var source = new ThreadSafetySource
                            {
                                Name = $"Thread{threadId}_Item{i}",
                                Value = threadId * 1000 + i
                            };

                            var result = mapper.Map<ThreadSafetySource, ThreadSafetyDestination>(source);
                            results.Add(result);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                })
            ).ToArray();

            Task.WaitAll(tasks);

            // Assert
            Assert.Empty(exceptions);
            Assert.Equal(threadCount * itemsPerThread, results.Count);

            // Verify all results are correct
            foreach (var result in results)
            {
                Assert.NotNull(result.Name);
                Assert.True(result.Name.StartsWith("Thread"));
                Assert.True(result.Value >= 0);
            }
        }

        [Fact]
        public void MapperConfiguration_ThreadSafety_ConcurrentConfigurationAndMapping_WorksSafely()
        {
            // Arrange
            var config = new MapperConfiguration();
            var exceptions = new ConcurrentBag<Exception>();
            const int configThreads = 3;
            const int mapThreads = 5;

            // Act - Configure mappings concurrently while mapping
            var configTasks = Enumerable.Range(0, configThreads).Select(i =>
                Task.Run(() =>
                {
                    try
                    {
                        config.CreateMap<ConcurrentConfigSource, ConcurrentConfigDestination>();
                        config.CreateMap<ThreadSafetySource, ThreadSafetyDestination>();
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                })
            );

            var mapTasks = Enumerable.Range(0, mapThreads).Select(i =>
                Task.Run(async () =>
                {
                    try
                    {
                        // Wait a bit to let some configuration happen first
                        await Task.Delay(10);

                        var mapper = new Mapper(config);
                        var source = new ThreadSafetySource { Name = $"MapTask{i}", Value = i };

                        // This might fail if mapping isn't configured yet, which is expected
                        try
                        {
                            var result = mapper.Map<ThreadSafetySource, ThreadSafetyDestination>(source);
                        }
                        catch (InvalidOperationException)
                        {
                            // Expected if mapping not configured yet
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                })
            );

            Task.WaitAll(configTasks.Concat(mapTasks).ToArray());

            // Assert - Should not have any unexpected exceptions
            var unexpectedExceptions = exceptions.Where(e => !(e is InvalidOperationException)).ToList();
            Assert.Empty(unexpectedExceptions);
        }

        [Fact]
        public void TypeConverter_ThreadSafety_ConcurrentTypeConversions_WorksSafely()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.AddTypeConverter<string, int>(new ThreadSafeStringToIntConverter());
            config.CreateMap<StringValueSource, IntValueDestination>();
            var mapper = new Mapper(config);

            var exceptions = new ConcurrentBag<Exception>();
            var results = new ConcurrentBag<int>();
            const int threadCount = 10;
            const int conversionsPerThread = 100;

            // Act
            var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
                Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < conversionsPerThread; i++)
                        {
                            var source = new StringValueSource { Value = (threadId * 1000 + i).ToString() };
                            var result = mapper.Map<StringValueSource, IntValueDestination>(source);
                            results.Add(result.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                })
            ).ToArray();

            Task.WaitAll(tasks);

            // Assert
            Assert.Empty(exceptions);
            Assert.Equal(threadCount * conversionsPerThread, results.Count);
            Assert.All(results, r => Assert.True(r >= 0));
        }

        [Fact]
        public void Mapper_Map_WithMemoryPressure_DoesNotLeakMemory()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<LargeObjectSource, LargeObjectDestination>();
            var mapper = new Mapper(config);

            var initialMemory = GC.GetTotalMemory(true);

            // Act - Create many large objects to test memory handling
            for (int i = 0; i < 1000; i++)
            {
                var source = new LargeObjectSource
                {
                    Data = new string('x', 1000),
                    Numbers = Enumerable.Range(0, 100).ToArray()
                };

                var result = mapper.Map<LargeObjectSource, LargeObjectDestination>(source);

                // Force garbage collection periodically
                if (i % 100 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            var finalMemory = GC.GetTotalMemory(true);

            // Assert - Memory should not grow excessively (allowing for some variance)
            var memoryIncrease = finalMemory - initialMemory;
            Assert.True(memoryIncrease < 50_000_000, // Allow up to 50MB increase
                $"Memory increased by {memoryIncrease} bytes, which may indicate a memory leak");
        }

        [Fact]
        public void Mapper_Map_WithExceptionInCustomResolver_HandlesGracefully()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<ExceptionTestSource, ExceptionTestDestination>(cfg =>
            {
                cfg.ForMember(dest => dest.Value, opt => opt.MapFrom(src =>
                {
                    if (src.ShouldThrow)
                        throw new CustomMappingException("Custom resolver failed");
                    return src.Value;
                }));
            });
            var mapper = new Mapper(config);

            var source = new ExceptionTestSource { Value = "test", ShouldThrow = true };

            // Act & Assert
            Assert.Throws<CustomMappingException>(() =>
                mapper.Map<ExceptionTestSource, ExceptionTestDestination>(source));
        }

        [Fact]
        public void Mapper_Map_WithDeepInheritanceHierarchy_MapsCorrectly()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<GrandParentSource, GrandParentDestination>();
            config.CreateMap<ParentSource, ParentDestination>();
            config.CreateMap<ChildSource, ChildDestination>();
            var mapper = new Mapper(config);

            var source = new ChildSource
            {
                GrandParentProperty = "GrandParent",
                ParentProperty = "Parent",
                ChildProperty = "Child"
            };

            // Act
            var result = mapper.Map<ChildSource, ChildDestination>(source);

            // Assert
            Assert.Equal("GrandParent", result.GrandParentProperty);
            Assert.Equal("Parent", result.ParentProperty);
            Assert.Equal("Child", result.ChildProperty);
        }

        [Fact]
        public void Mapper_Map_WithCollectionOfComplexObjects_HandlesLargeCollections()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<ComplexItemSource, ComplexItemDestination>();
            config.CreateMap<ComplexCollectionSource, ComplexCollectionDestination>();
            var mapper = new Mapper(config);

            var source = new ComplexCollectionSource
            {
                Items = Enumerable.Range(0, 10000).Select(i => new ComplexItemSource
                {
                    Id = i,
                    Name = $"Item_{i}",
                    Value = i * 2.5,
                    IsActive = i % 2 == 0
                }).ToList()
            };

            // Act
            var result = mapper.Map<ComplexCollectionSource, ComplexCollectionDestination>(source);

            // Assert
            Assert.Equal(10000, result.Items.Count);
            Assert.Equal("Item_5000", result.Items[5000].Name);
            Assert.Equal(12500.0, result.Items[5000].Value);
        }

        // Helper method
        private DeepNestingSource CreateDeepNestedObject(int depth)
        {
            var current = new DeepNestingSource { Name = $"Level_{depth}" };

            if (depth > 0)
            {
                current.Child = CreateDeepNestedObject(depth - 1);
            }

            return current;
        }

        // Test classes
        public class ErrorTestSource
        {
            public string? Name { get; set; }
        }

        public class ErrorTestDestination
        {
            public string? Name { get; set; }
        }

        public class UnregisteredSource
        {
            public string? Name { get; set; }
        }

        public class UnregisteredDestination
        {
            public string? Name { get; set; }
        }

        public class CircularParent
        {
            public string? Name { get; set; }
            public CircularChild? Child { get; set; }
        }

        public class CircularChild
        {
            public string? Name { get; set; }
            public CircularParent? Parent { get; set; }
        }

        public class CircularParentDto
        {
            public string? Name { get; set; }
            public CircularChildDto? Child { get; set; }
        }

        public class CircularChildDto
        {
            public string? Name { get; set; }
            public CircularParentDto? Parent { get; set; }
        }

        public class InvalidConversionSource
        {
            public string? Value { get; set; }
        }

        public class InvalidConversionDestination
        {
            public DateTime Value { get; set; }
        }

        public class DeepNestingSource
        {
            public string? Name { get; set; }
            public DeepNestingSource? Child { get; set; }
        }

        public class DeepNestingDestination
        {
            public string? Name { get; set; }
            public DeepNestingDestination? Child { get; set; }
        }

        public class ThreadSafetySource
        {
            public string? Name { get; set; }
            public int Value { get; set; }
        }

        public class ThreadSafetyDestination
        {
            public string? Name { get; set; }
            public int Value { get; set; }
        }

        public class ConcurrentConfigSource
        {
            public string? Name { get; set; }
        }

        public class ConcurrentConfigDestination
        {
            public string? Name { get; set; }
        }

        public class StringValueSource
        {
            public string Value { get; set; } = default!;
        }

        public class IntValueDestination
        {
            public int Value { get; set; }
        }

        public class LargeObjectSource
        {
            public string Data { get; set; } = default!;
            public int[] Numbers { get; set; } = default!;
        }

        public class LargeObjectDestination
        {
            public string Data { get; set; } = default!;
            public int[] Numbers { get; set; } = default!;
        }

        public class ExceptionTestSource
        {
            public string Value { get; set; } = default!;
            public bool ShouldThrow { get; set; }
        }

        public class ExceptionTestDestination
        {
            public string? Value { get; set; }
        }

        public class GrandParentSource
        {
            public string? GrandParentProperty { get; set; }
        }

        public class GrandParentDestination
        {
            public string? GrandParentProperty { get; set; }
        }

        public class ParentSource : GrandParentSource
        {
            public string? ParentProperty { get; set; }
        }

        public class ParentDestination : GrandParentDestination
        {
            public string? ParentProperty { get; set; }
        }

        public class ChildSource : ParentSource
        {
            public string? ChildProperty { get; set; }
        }

        public class ChildDestination : ParentDestination
        {
            public string? ChildProperty { get; set; }
        }

        public class ComplexItemSource
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public double Value { get; set; }
            public bool IsActive { get; set; }
        }

        public class ComplexItemDestination
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public double Value { get; set; }
            public bool IsActive { get; set; }
        }

        public class ComplexCollectionSource
        {
            public List<ComplexItemSource> Items { get; set; } = new();
        }

        public class ComplexCollectionDestination
        {
            public List<ComplexItemDestination> Items { get; set; } = new();
        }

        public class ThreadSafeStringToIntConverter : ITypeConverter<string, int>
        {
            public int Convert(string source)
            {
                return int.Parse(source);
            }
        }

        public class CustomMappingException : Exception
        {
            public CustomMappingException(string message) : base(message) { }
        }
    }
}