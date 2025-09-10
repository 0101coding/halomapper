using System.Collections.Generic;
using Xunit;

namespace HaloMapper.Tests
{
    public class MappingTests
    {
        [Fact]
        public void Maps_simple_properties_by_name()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<Person, PersonDto>();
            var mapper = new Mapper(cfg);

            var p = new Person { FirstName = "Jane", LastName = "Doe", Age = 28 };
            var dto = mapper.Map<Person, PersonDto>(p);

            Assert.Equal("Jane", dto.FirstName);
            Assert.Equal("Doe", dto.LastName);
            Assert.Equal(28, dto.Age);
        }

        [Fact]
        public void MapFrom_custom_fullname()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<Person, PersonDto>().ForMember(d => d.FullName, o => o.MapFrom(s => s.FirstName + " " + s.LastName));
            var mapper = new Mapper(cfg);

            var p = new Person { FirstName = "Joe", LastName = "Blogs" };
            var dto = mapper.Map<Person, PersonDto>(p);

            Assert.Equal("Joe Blogs", dto.FullName);
        }

        [Fact]
        public void Ignore_member()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<Person, PersonDto>()
                .ForMember(d => d.Age, o => o.Ignore());
            var mapper = new Mapper(cfg);

            var p = new Person { Age = 40 };
            var dto = mapper.Map<Person, PersonDto>(p);
            Assert.Equal(0, dto.Age);
        }

        [Fact]
        public void Map_collections()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<Person, PersonDto>();
            var mapper = new Mapper(cfg);

            var people = new List<Person> { new Person { FirstName = "A" }, new Person { FirstName = "B" } };
            var dtos = mapper.MapCollection<Person, PersonDto>(people);
            Assert.Collection(dtos, d => Assert.Equal("A", d.FirstName), d => Assert.Equal("B", d.FirstName));
        }

        [Fact]
        public void Nested_mapping()
        {
            var cfg = new MapperConfiguration();
            cfg.CreateMap<Address, AddressDto>();
            cfg.CreateMap<Person, PersonWithAddressDto>();
            var mapper = new Mapper(cfg);

            var p = new Person { FirstName = "X", Address = new Address { City = "Lagos" } };
            var dto = mapper.Map<Person, PersonWithAddressDto>(p);
            Assert.Equal("Lagos", dto.Address.City);
        }

        [Fact]
        public void ConstructUsing_and_AfterBeforeMap()
        {
            var cfg = new MapperConfiguration(); 
            cfg.CreateMap<Person, PersonDto>()
                .ConstructUsing(src => new PersonDto { FullName = "constructed" })
                .BeforeMap((s, d) => d.FullName = "before")
                .AfterMap((s, d) => d.FullName = s.FirstName + "-after");

            var mapper = new Mapper(cfg);
            var dto = mapper.Map<Person, PersonDto>(new Person { FirstName = "Z" });
            Assert.Equal("Z-after", dto.FullName);
        }
    }

    // test types
    public class Person
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int Age { get; set; }
        public Address? Address { get; set; }
    }

    public class Address
    {
        public string? City { get; set; }
        public string? Street { get; set; }
    }

    public class PersonDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int Age { get; set; }
        public string? FullName { get; set; }
    }

    public class AddressDto
    {
        public string? City { get; set; }
    }

    public class PersonWithAddressDto
    {
        public string? FirstName { get; set; }
        public AddressDto? Address { get; set; }
    }
}