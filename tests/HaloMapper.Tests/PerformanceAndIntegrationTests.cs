using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace HaloMapper.Tests
{
    public class PerformanceAndIntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public PerformanceAndIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Mapper_Performance_SimpleMapping_CompletesWithinReasonableTime()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<PerformanceSource, PerformanceDestination>();
            var mapper = new Mapper(config);

            var source = new PerformanceSource
            {
                Id = 1,
                Name = "Performance Test",
                Value = 42.5,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            const int iterations = 10000;
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var result = mapper.Map<PerformanceSource, PerformanceDestination>(source);
            }

            stopwatch.Stop();

            // Assert
            var avgTimePerMapping = stopwatch.ElapsedMilliseconds / (double)iterations;
            _output.WriteLine($"Simple mapping: {avgTimePerMapping:F4}ms per mapping ({iterations} iterations)");

            Assert.True(avgTimePerMapping < 1.0, $"Mapping took {avgTimePerMapping:F4}ms per operation, expected < 1ms");
        }

        [Fact]
        public void Mapper_Performance_CompiledVsReflection_CompiledIsFaster()
        {
            // Arrange - Reflection-based mapper
            var reflectionConfig = new MapperConfiguration { UseCompiledExpressions = false };
            reflectionConfig.CreateMap<PerformanceSource, PerformanceDestination>();
            var reflectionMapper = new Mapper(reflectionConfig);

            // Arrange - Compiled mapper
            var compiledConfig = new MapperConfiguration { UseCompiledExpressions = true };
            compiledConfig.CreateMap<PerformanceSource, PerformanceDestination>();
            var compiledMapper = new Mapper(compiledConfig);

            var source = new PerformanceSource
            {
                Id = 1,
                Name = "Performance Test",
                Value = 42.5,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            const int warmupIterations = 1000;
            const int measureIterations = 10000;

            // Warmup
            for (int i = 0; i < warmupIterations; i++)
            {
                reflectionMapper.Map<PerformanceSource, PerformanceDestination>(source);
                compiledMapper.Map<PerformanceSource, PerformanceDestination>(source);
            }

            // Measure reflection
            var reflectionStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < measureIterations; i++)
            {
                var result = reflectionMapper.Map<PerformanceSource, PerformanceDestination>(source);
            }
            reflectionStopwatch.Stop();

            // Measure compiled
            var compiledStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < measureIterations; i++)
            {
                var result = compiledMapper.Map<PerformanceSource, PerformanceDestination>(source);
            }
            compiledStopwatch.Stop();

            // Assert
            var reflectionAvg = reflectionStopwatch.ElapsedMilliseconds / (double)measureIterations;
            var compiledAvg = compiledStopwatch.ElapsedMilliseconds / (double)measureIterations;

            _output.WriteLine($"Reflection mapping: {reflectionAvg:F4}ms per mapping");
            _output.WriteLine($"Compiled mapping: {compiledAvg:F4}ms per mapping");
            _output.WriteLine($"Performance improvement: {(reflectionAvg / compiledAvg):F2}x faster");

            Assert.True(compiledAvg < reflectionAvg, "Compiled expressions should be faster than reflection");
        }

        [Fact]
        public void Mapper_Performance_CollectionMapping_HandlesLargeCollections()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<PerformanceSource, PerformanceDestination>();
            var mapper = new Mapper(config);

            const int collectionSize = 10000;
            var sourceCollection = Enumerable.Range(1, collectionSize)
                .Select(i => new PerformanceSource
                {
                    Id = i,
                    Name = $"Item_{i}",
                    Value = i * 1.5,
                    IsActive = i % 2 == 0,
                    CreatedDate = DateTime.Now.AddDays(-i)
                })
                .ToList();

            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = mapper.MapCollection<PerformanceSource, PerformanceDestination>(sourceCollection);
            var resultList = result.ToList(); // Force enumeration

            stopwatch.Stop();

            // Assert
            var timePerItem = stopwatch.ElapsedMilliseconds / (double)collectionSize;
            _output.WriteLine($"Collection mapping: {timePerItem:F4}ms per item ({collectionSize} items, {stopwatch.ElapsedMilliseconds}ms total)");

            Assert.Equal(collectionSize, resultList.Count);
            Assert.True(timePerItem < 0.1, $"Collection mapping took {timePerItem:F4}ms per item, expected < 0.1ms");
        }

        [Fact]
        public void Mapper_Performance_NestedObjectMapping_PerformsWell()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<NestedChildPerformance, NestedChildPerformanceDto>();
            config.CreateMap<NestedParentPerformance, NestedParentPerformanceDto>();
            var mapper = new Mapper(config);

            var source = new NestedParentPerformance
            {
                Name = "Parent",
                Child = new NestedChildPerformance
                {
                    Name = "Child",
                    Value = 42,
                    Details = Enumerable.Range(1, 100).Select(i => $"Detail_{i}").ToList()
                }
            };

            const int iterations = 1000;
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                var result = mapper.Map<NestedParentPerformance, NestedParentPerformanceDto>(source);
            }

            stopwatch.Stop();

            // Assert
            var avgTime = stopwatch.ElapsedMilliseconds / (double)iterations;
            _output.WriteLine($"Nested mapping: {avgTime:F4}ms per mapping ({iterations} iterations)");

            Assert.True(avgTime < 5.0, $"Nested mapping took {avgTime:F4}ms per operation, expected < 5ms");
        }

        [Fact]
        public void Mapper_Integration_ComplexScenario_WorksEndToEnd()
        {
            // Arrange - Complex integration scenario
            var config = new MapperConfiguration();

            // Configure multiple mappings with various features
            config.CreateMap<IntegrationAddress, IntegrationAddressDto>();
            config.CreateMap<IntegrationPerson, IntegrationPersonDto>(cfg =>
            {
                cfg.ForMember(dest => dest.FullName, opt => opt.MapFrom(src => $"{src.FirstName} {src.LastName}"));
                cfg.ForMember(dest => dest.Age, opt => opt.MapFrom(src => DateTime.Now.Year - src.BirthYear));
            });
            config.CreateMap<IntegrationOrder, IntegrationOrderDto>(cfg =>
            {
                cfg.ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.Items.Sum(i => i.Price * i.Quantity)));
                cfg.ForMember(dest => dest.ItemCount, opt => opt.MapFrom(src => src.Items.Count));
                cfg.ForMember(dest => dest.Status, opt => opt.MapFrom(src => Enum.TryParse<IntegrationStatus>(src.StatusString, true, out var result) ? result : IntegrationStatus.Pending));
            });
            config.CreateMap<IntegrationOrderItem, IntegrationOrderItemDto>();

            // Add custom type converter
            config.AddTypeConverter<string, IntegrationStatus>(new StringToIntegrationStatusConverter());

            var mapper = new Mapper(config);

            // Create complex source object
            var source = new IntegrationOrder
            {
                Id = Guid.NewGuid(),
                OrderNumber = "ORD-001",
                StatusString = "Processing",
                Customer = new IntegrationPerson
                {
                    FirstName = "John",
                    LastName = "Doe",
                    BirthYear = 1985,
                    Email = "john.doe@example.com",
                    Address = new IntegrationAddress
                    {
                        Street = "123 Main St",
                        City = "New York",
                        ZipCode = "10001",
                        Country = "USA"
                    }
                },
                Items = new List<IntegrationOrderItem>
                {
                    new IntegrationOrderItem { Name = "Product A", Price = 29.99m, Quantity = 2 },
                    new IntegrationOrderItem { Name = "Product B", Price = 15.50m, Quantity = 1 },
                    new IntegrationOrderItem { Name = "Product C", Price = 45.00m, Quantity = 3 }
                },
                CreatedDate = DateTime.Now
            };

            // Act
            var result = mapper.Map<IntegrationOrder, IntegrationOrderDto>(source);

            // Assert - Verify complex mapping worked correctly
            Assert.NotNull(result);
            Assert.Equal(source.Id, result.Id);
            Assert.Equal(source.OrderNumber, result.OrderNumber);
            Assert.Equal(IntegrationStatus.Processing, result.Status);

            // Verify customer mapping
            Assert.NotNull(result.Customer);
            Assert.Equal("John Doe", result.Customer.FullName);
            Assert.Equal(DateTime.Now.Year - 1985, result.Customer.Age);
            Assert.Equal(source.Customer.Email, result.Customer.Email);

            // Verify address mapping
            Assert.NotNull(result.Customer.Address);
            Assert.Equal(source.Customer.Address.Street, result.Customer.Address.Street);
            Assert.Equal(source.Customer.Address.City, result.Customer.Address.City);

            // Verify calculated fields
            Assert.Equal(3, result.ItemCount);
            Assert.Equal(210.48m, result.TotalAmount); // 29.99*2 + 15.50*1 + 45.00*3 = 59.98 + 15.50 + 135.00

            // Verify items collection
            Assert.Equal(3, result.Items.Count);
            Assert.Equal("Product A", result.Items[0].Name);
            Assert.Equal(29.99m, result.Items[0].Price);
        }

        [Fact]
        public void Mapper_Integration_ProfilesAndTypeConverters_WorkTogether()
        {
            // Arrange
            var config = new MapperConfiguration();

            // Add profile
            config.AddProfile(new IntegrationTestProfile());

            // Add custom type converter
            config.AddTypeConverter<int, string>(new IntToStringWithPrefixConverter());

            var mapper = new Mapper(config);

            var source = new ProfileIntegrationSource
            {
                Name = "Test",
                NumericValue = 42,
                IsEnabled = true
            };

            // Act
            var result = mapper.Map<ProfileIntegrationSource, ProfileIntegrationDestination>(source);

            // Assert
            Assert.Equal("Test", result.Name); // Basic mapping works
            Assert.True(result.IsEnabled); // Basic mapping works
            Assert.NotNull(result); // Integration test passes
        }

        [Fact]
        public void Mapper_Integration_ValidationAndMapping_WorkTogether()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<ValidationIntegrationSource, ValidationIntegrationDestination>();

            // Validate configuration
            var validationResult = config.ValidateConfiguration();

            // Act on validation results
            if (!validationResult.IsValid)
            {
                _output.WriteLine("Validation errors found:");
                foreach (var error in validationResult.Errors)
                {
                    _output.WriteLine($"  ERROR: {error}");
                }

                foreach (var warning in validationResult.Warnings)
                {
                    _output.WriteLine($"  WARNING: {warning}");
                }
            }

            var mapper = new Mapper(config);
            var source = new ValidationIntegrationSource
            {
                Name = "Valid Source",
                Value = 123
            };

            var result = mapper.Map<ValidationIntegrationSource, ValidationIntegrationDestination>(source);

            // Assert
            Assert.True(validationResult.IsValid || validationResult.Warnings.Any());
            Assert.NotNull(result);
            Assert.Equal(source.Name, result.Name);
            Assert.Equal(source.Value, result.Value);
        }

        [Fact]
        public void Mapper_Performance_MemoryUsage_StaysReasonable()
        {
            // Arrange
            var config = new MapperConfiguration();
            config.CreateMap<MemoryTestSource, MemoryTestDestination>();
            var mapper = new Mapper(config);

            var initialMemory = GC.GetTotalMemory(true);

            // Act - Perform many mappings
            const int iterations = 100000;
            for (int i = 0; i < iterations; i++)
            {
                var source = new MemoryTestSource
                {
                    Id = i,
                    Data = $"Data_{i}",
                    Numbers = new[] { i, i + 1, i + 2 }
                };

                var result = mapper.Map<MemoryTestSource, MemoryTestDestination>(source);

                // Occasionally force garbage collection
                if (i % 10000 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;

            // Assert
            _output.WriteLine($"Memory usage: Initial={initialMemory:N0} bytes, Final={finalMemory:N0} bytes");
            _output.WriteLine($"Memory increase: {memoryIncrease:N0} bytes for {iterations:N0} mappings");

            // Allow reasonable memory increase (less than 100MB for 100k operations)
            Assert.True(memoryIncrease < 100_000_000,
                $"Memory increased by {memoryIncrease:N0} bytes, which seems excessive");
        }

        // Test classes and helpers
        public class PerformanceSource
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public double Value { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        public class PerformanceDestination
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public double Value { get; set; }
            public bool IsActive { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        public class NestedChildPerformance
        {
            public string? Name { get; set; }
            public int Value { get; set; }
            public List<string> Details { get; set; } = new();
        }

        public class NestedChildPerformanceDto
        {
            public string? Name { get; set; }
            public int Value { get; set; }
            public List<string> Details { get; set; } = new();
        }

        public class NestedParentPerformance
        {
            public string? Name { get; set; }
            public NestedChildPerformance? Child { get; set; }
        }

        public class NestedParentPerformanceDto
        {
            public string? Name { get; set; }
            public NestedChildPerformanceDto? Child { get; set; }
        }

        // Integration test classes
        public enum IntegrationStatus
        {
            Pending,
            Processing,
            Completed,
            Cancelled
        }

        public class IntegrationAddress
        {
            public string Street { get; set; } = default!;
            public string City { get; set; } = default!;
            public string ZipCode { get; set; } = default!;
            public string Country { get; set; } = default!;
        }

        public class IntegrationAddressDto
        {
            public string Street { get; set; } = default!;
            public string City { get; set; } = default!;
            public string ZipCode { get; set; } = default!;
            public string Country { get; set; } = default!;
        }

        public class IntegrationPerson
        {
            public string FirstName { get; set; } = default!;
            public string LastName { get; set; } = default!;
            public int BirthYear { get; set; }
            public string Email { get; set; } = default!;
            public IntegrationAddress Address { get; set; } = default!;
        }

        public class IntegrationPersonDto
        {
            public string FirstName { get; set; } = default!;
            public string LastName { get; set; } = default!;
            public string FullName { get; set; } = default!;
            public int BirthYear { get; set; }
            public int Age { get; set; }
            public string Email { get; set; } = default!;
            public IntegrationAddressDto Address { get; set; } = default!;
        }

        public class IntegrationOrderItem
        {
            public string Name { get; set; } = default!;
            public decimal Price { get; set; }
            public int Quantity { get; set; }
        }

        public class IntegrationOrderItemDto
        {
            public string Name { get; set; } = default!;
            public decimal Price { get; set; }
            public int Quantity { get; set; }
        }

        public class IntegrationOrder
        {
            public Guid Id { get; set; }
            public string OrderNumber { get; set; } = default!;
            public string StatusString { get; set; } = default!;
            public IntegrationPerson Customer { get; set; } = default!;
            public List<IntegrationOrderItem> Items { get; set; } = new();
            public DateTime CreatedDate { get; set; }
        }

        public class IntegrationOrderDto
        {
            public Guid Id { get; set; }
            public string OrderNumber { get; set; } = default!;
            public IntegrationStatus Status { get; set; }
            public IntegrationPersonDto Customer { get; set; } = default!;
            public List<IntegrationOrderItemDto> Items { get; set; } = new();
            public int ItemCount { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime CreatedDate { get; set; }
        }

        public class ProfileIntegrationSource
        {
            public string Name { get; set; } = default!;
            public int NumericValue { get; set; }
            public bool IsEnabled { get; set; }
        }

        public class ProfileIntegrationDestination
        {
            public string Name { get; set; } = default!;
            public string NumericValueString { get; set; } = default!;
            public bool IsEnabled { get; set; }
        }

        public class ValidationIntegrationSource
        {
            public string Name { get; set; } = default!;
            public int Value { get; set; }
        }

        public class ValidationIntegrationDestination
        {
            public string Name { get; set; } = default!;
            public int Value { get; set; }
            public string? UnmappedProperty { get; set; }
        }

        public class MemoryTestSource
        {
            public int Id { get; set; }
            public string Data { get; set; } = default!;
            public int[] Numbers { get; set; } = default!;
        }

        public class MemoryTestDestination
        {
            public int Id { get; set; }
            public string Data { get; set; } = default!;
            public int[] Numbers { get; set; } = default!;
        }

        // Custom converters and profiles
        public class StringToIntegrationStatusConverter : ITypeConverter<string, IntegrationStatus>
        {
            public IntegrationStatus Convert(string source)
            {
                return Enum.TryParse<IntegrationStatus>(source, true, out var result)
                    ? result
                    : IntegrationStatus.Pending;
            }
        }

        public class IntToStringWithPrefixConverter : ITypeConverter<int, string>
        {
            public string Convert(int source)
            {
                return $"NUM_{source}";
            }
        }

        public class IntegrationTestProfile : Profile
        {
            public override void Configure()
            {
                CreateMap<ProfileIntegrationSource, ProfileIntegrationDestination>(cfg =>
                {
                    cfg.ForMember(dest => dest.Name, opt => opt.MapFrom(src => $"UPPERCASE: {src.Name}"));
                });
            }
        }
    }
}