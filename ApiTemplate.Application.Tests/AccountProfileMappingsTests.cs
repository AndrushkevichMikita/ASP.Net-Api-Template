using ApiTemplate.Application.Mappings;
using ApiTemplate.Application.Models;
using ApiTemplate.Domain.Entities;
using AutoMapper;

namespace ApiTemplate.Application.Tests
{
    public class AccountProfileMappingsTests
    {
        [Fact]
        public void AccountProfile_ConfigurationIsValid()
        {
            // Arrange & Act
            var config = new MapperConfiguration(cfg => cfg.AddProfile<AccountProfile>());

            // Assert
            config.AssertConfigurationIsValid();
        }

        [Theory]
        [InlineData(typeof(AccountEntity), typeof(AccountDto))]
        [InlineData(typeof(CreateAccountDto), typeof(AccountEntity))]
        public void AccountProfile_MappingIsValid(Type source, Type destination)
        {
            // Arrange & Act
            var config = new MapperConfiguration(cfg => cfg.AddProfile<AccountProfile>());
            var mapper = config.CreateMapper();

            // Assert
            mapper.ConfigurationProvider.AssertConfigurationIsValid();
            var instance = Activator.CreateInstance(source);
            mapper.Map(instance, source, destination);
        }
    }
}