using ApiTemplate.Application.Models;
using ApiTemplate.Presentation.Web.Mappings;
using ApiTemplate.Presentation.Web.Models;
using AutoMapper;

namespace ApiTemplate.Presentation.Web.Tests
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
        [InlineData(typeof(CreateAccountModel), typeof(CreateAccountDto))]
        [InlineData(typeof(LoginAccountModel), typeof(LoginAccountDto))]
        [InlineData(typeof(RefreshTokenModel), typeof(RefreshTokenDto))]
        [InlineData(typeof(RefreshTokenDto), typeof(RefreshTokenModel))]
        [InlineData(typeof(AccountDto), typeof(AccountModel))]
        public void AccountProfile_MappingIsValid(Type source, Type destination)
        {
            // Arrange & Act
            var config = new MapperConfiguration(cfg => cfg.AddProfile<AccountProfile>());
            var mapper = config.CreateMapper();

            // Assert
            mapper.ConfigurationProvider.AssertConfigurationIsValid();
            var instance = Activator.CreateInstance(source);
            var t = mapper.Map(instance, source, destination);
        }
    }
}