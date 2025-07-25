using ApiTemplate.Application.Interfaces;
using ApiTemplate.Application.Models;
using ApiTemplate.Application.Services;
using ApiTemplate.Domain.Entities;
using ApiTemplate.SharedKernel.ExceptionHandler;
using ApiTemplate.SharedKernel.Extensions;
using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ApiTemplate.Application.Tests
{
    public class AccountServiceTests
    {
        private readonly Mock<IRepository<AccountTokenEntity>> _userTokenRepoMock;
        private readonly Mock<IEmailTemplateService> _emailTemplateServiceMock;
        private readonly Mock<ApplicationSignInManager> _signInManager;
        private readonly Mock<UserManager<Account>> _userManagerMock;
        private readonly AccountService _accountService;
        private readonly Mock<IMapper> _mapperMock;

        public AccountServiceTests()
        {
            _mapperMock = new Mock<IMapper>();
            _userTokenRepoMock = new Mock<IRepository<AccountTokenEntity>>();
            _emailTemplateServiceMock = new Mock<IEmailTemplateService>();

            _userManagerMock = new Mock<UserManager<Account>>(
                Mock.Of<IUserStore<Account>>(),
                Mock.Of<IOptions<IdentityOptions>>(),
                Mock.Of<IPasswordHasher<Account>>(),
                Array.Empty<IUserValidator<Account>>(),
                Array.Empty<IPasswordValidator<Account>>(),
                Mock.Of<ILookupNormalizer>(),
                Mock.Of<IdentityErrorDescriber>(),
                Mock.Of<IServiceProvider>(),
                Mock.Of<ILogger<UserManager<Account>>>()
            );

            var optionsAccessorMock = Options.Create(new IdentityOptions());

            _signInManager = new Mock<ApplicationSignInManager>(
                _userManagerMock.Object,
                new Mock<IHttpContextAccessor>().Object,
                new Mock<IUserClaimsPrincipalFactory<Account>>().Object,
                new Mock<IConfiguration>().Object,
                optionsAccessorMock,
                new Mock<ILogger<SignInManager<Account>>>().Object,
                new Mock<IAuthenticationSchemeProvider>().Object,
                new Mock<IUserConfirmation<Account>>().Object,
                new ApplicationUserClaimsPrincipalFactory
                (
                    _userManagerMock.Object,
                    new Mock<RoleManager<IdentityRole<int>>>(
                        Mock.Of<IRoleStore<IdentityRole<int>>>(),
                        Array.Empty<IRoleValidator<IdentityRole<int>>>(),
                        Mock.Of<ILookupNormalizer>(),
                        Mock.Of<IdentityErrorDescriber>(),
                        Mock.Of<ILogger<RoleManager<IdentityRole<int>>>>()
                    ).Object,
                    optionsAccessorMock
                )
            );

            _accountService = new AccountService(
                _emailTemplateServiceMock.Object,
                _userTokenRepoMock.Object,
                _signInManager.Object,
                _mapperMock.Object);
        }

        [Fact]
        public async Task SignOut_ShouldCallSignOutAsync()
        {
            // Act
            await _accountService.SignOut();

            // Assert
            _signInManager.Verify(x => x.SignOutAsync(), Times.Once);
        }

        [Fact]
        public async Task LoginAccount_UserNotFound_ThrowsException()
        {
            // Arrange
            var accountDto = new LoginAccountDTO { Email = "test@example.com", Password = "password123", RememberMe = true };

            _userManagerMock.Setup(x => x.FindByEmailAsync(accountDto.Email))
                            .ReturnsAsync((Account)null);
            // Act & Assert
            await Assert.ThrowsAsync<MyApplicationException>(() => _accountService.LoginAccount(accountDto));
        }

        [Fact]
        public async Task LoginAccount_ShouldReturnRefreshTokenDto_WhenCredentialsAreValid()
        {
            // Arrange
            var accountDto = new LoginAccountDTO { Email = "test@example.com", Password = "Password123", RememberMe = true };
            var accountEntity = new Account { Email = "test@example.com" };

            _userManagerMock.Setup(x => x.FindByEmailAsync(accountDto.Email))
                            .ReturnsAsync(accountEntity);

            _userManagerMock.Setup(x => x.IsEmailConfirmedAsync(accountEntity))
                            .ReturnsAsync(true);

            _userManagerMock.Setup(x => x.CheckPasswordAsync(accountEntity, accountDto.Password))
                            .ReturnsAsync(true);

            _signInManager.Setup(x => x.SignInAsync(accountEntity, accountDto.RememberMe, null))
                          .Returns(Task.CompletedTask);

            _signInManager.Setup(x => x.GenerateJwtTokenAsync(accountEntity))
                          .ReturnsAsync("jwt_token");

            _signInManager.Setup(x => x.GenerateRefreshTokenAsync(accountEntity))
                          .ReturnsAsync("refresh_token");
            // Act
            var result = await _accountService.LoginAccount(accountDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("jwt_token", result.Token);
            Assert.Equal("refresh_token", result.RefreshToken);
        }

        [Fact]
        public async Task CreateAccount_ShouldCreateNewAccount_WhenModelIsValid()
        {
            // Arrange
            var accountDto = new CreateAccountDTO { Email = "test@example.com", Password = "password123", Role = RoleEnum.Admin };
            var accountEntity = new Account { Email = accountDto.Email, Role = accountDto.Role };
            _mapperMock.Setup(x => x.Map<Account>(accountDto)).Returns(accountEntity);

            _userManagerMock.Setup(x => x.CreateAsync(accountEntity, accountDto.Password))
                            .ReturnsAsync(IdentityResult.Success);

            _userManagerMock.Setup(x => x.AddToRoleAsync(accountEntity, accountDto.Role.ToString()))
                            .ReturnsAsync(IdentityResult.Success);
            // Act
            await _accountService.CreateAccount(accountDto);

            // Assert
            _mapperMock.Verify(x => x.Map<Account>(accountDto), Times.Once);
            _userManagerMock.Verify(x => x.CreateAsync(accountEntity, accountDto.Password), Times.Once);
            _userManagerMock.Verify(x => x.AddToRoleAsync(accountEntity, accountDto.Role.ToString()), Times.Once);
        }

        [Fact]
        public async Task SendDigitCodeByEmail_UserNotFound_ThrowsException()
        {
            // Arrange
            var email = "test@example.com";

            _userManagerMock.Setup(x => x.FindByEmailAsync(email))
                            .ReturnsAsync((Account)null);
            // Act & Assert
            await Assert.ThrowsAsync<MyApplicationException>(() => _accountService.SendDigitCodeByEmail(email));
        }

        [Fact]
        public async Task SendDigitCodeByEmail_EmailSentSuccessfully()
        {
            // Arrange
            var email = "test@example.com";
            var accountEntity = new Account { Id = 1, Email = email, FirstName = "John", LastName = "Doe" };

            var accountTokenEntity = AccountTokenEntity.Create(1, "", TokenEnum.EmailToken.ToString(), "token_value");

            _userTokenRepoMock.Setup(x => x.GetIQueryable(false))
                              .Returns(IQueryableExtension.AsAsyncQueryable(new List<AccountTokenEntity>() { accountTokenEntity }));

            _userManagerMock.Setup(x => x.FindByEmailAsync(email))
                            .ReturnsAsync(accountEntity);

            _userManagerMock.Setup(x => x.GenerateEmailConfirmationTokenAsync(accountEntity))
                            .ReturnsAsync("confirmation-token");
            // Act
            await _accountService.SendDigitCodeByEmail(email);

            // Assert
            _emailTemplateServiceMock.Verify(x => x.SendDigitCodeAsync(It.Is<EmailDTO>(dto =>
                dto.UserEmail == email &&
                dto.DigitCode.Length == 4 &&
                dto.FirstName == "John" &&
                dto.LastName == "Doe")), Times.Once);
        }

        [Fact]
        public async Task GetCurrent_ShouldReturnAccountDto_WhenUserExists()
        {
            // Arrange
            var userId = 1;
            var accountEntity = new Account { Id = userId, Email = "test@example.com" };
            var accountDto = new AccountDTO { Email = "test@example.com" };

            _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString()))
                            .ReturnsAsync(accountEntity);

            _mapperMock.Setup(x => x.Map<AccountDTO>(accountEntity))
                       .Returns(accountDto);
            // Act
            var result = await _accountService.GetAccount(userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test@example.com", result.Email);
        }

        [Fact]
        public async Task SendDigitCodeByEmail_ShouldSendCode_WhenUserExists()
        {
            // Arrange
            var email = "test@example.com";
            var accountEntity = new Account { Id = 1, Email = email, FirstName = "First", LastName = "Last" };

            var accountTokenEntity = AccountTokenEntity.Create(1, "", TokenEnum.EmailToken.ToString(), "token_value");

            _userTokenRepoMock.Setup(x => x.GetIQueryable(false))
                             .Returns(IQueryableExtension.AsAsyncQueryable(new List<AccountTokenEntity>() { accountTokenEntity }));

            _userManagerMock.Setup(x => x.FindByEmailAsync(email))
                            .ReturnsAsync(accountEntity);

            _userManagerMock.Setup(x => x.GenerateEmailConfirmationTokenAsync(accountEntity))
                            .ReturnsAsync("email_confirmation_token");
            // Act
            await _accountService.SendDigitCodeByEmail(email);

            // Assert
            _emailTemplateServiceMock.Verify(x => x.SendDigitCodeAsync(It.IsAny<EmailDTO>()), Times.Once);
            _userTokenRepoMock.Verify(x => x.InsertAsync(It.IsAny<AccountTokenEntity>(), true, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task ConfirmDigitCode_ShouldConfirmCode_WhenCodeIsValid()
        {
            // Arrange
            var digitCode = "1234";

            var accountEntity = new Account { Id = 1, Email = "test@example.com" };

            _userManagerMock.Setup(x => x.GenerateEmailConfirmationTokenAsync(accountEntity))
                         .ReturnsAsync("email_confirmation_token");

            var accountTokenEntity = await AccountTokenEntity.CreateAsync(accountEntity, digitCode, TokenEnum.EmailToken.ToString(), _userManagerMock.Object);

            _userTokenRepoMock.Setup(x => x.GetIQueryable(false))
                              .Returns(IQueryableExtension.AsAsyncQueryable(new List<AccountTokenEntity> { accountTokenEntity }));

            _userManagerMock.Setup(x => x.ConfirmEmailAsync(accountEntity, accountTokenEntity.Value))
                            .ReturnsAsync(IdentityResult.Success);
            // Act
            await _accountService.ConfirmDigitCode(digitCode);

            // Assert
            _userManagerMock.Verify(x => x.ConfirmEmailAsync(accountEntity, accountTokenEntity.Value), Times.Once);
            _userTokenRepoMock.Verify(x => x.DeleteAsync(accountTokenEntity, true, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task Delete_ShouldDeleteAccount_WhenPasswordIsValid()
        {
            // Arrange
            var password = "Password123";
            var accountId = 1;
            var accountEntity = new Account { Id = accountId, Email = "test@example.com" };

            _userManagerMock.Setup(x => x.FindByIdAsync(accountId.ToString()))
                            .ReturnsAsync(accountEntity);

            _userManagerMock.Setup(x => x.DeleteAsync(accountEntity))
                            .ReturnsAsync(IdentityResult.Success);

            _userManagerMock.Setup(x => x.CheckPasswordAsync(accountEntity, password))
                            .ReturnsAsync(true);
            // Act
            await _accountService.DeleteAccount(password, accountId);

            // Assert
            _userManagerMock.Verify(x => x.DeleteAsync(accountEntity), Times.Once);

        }

        [Fact]
        public async Task CreateNewJwtPair_ShouldReturnNewTokens_WhenRefreshTokenIsValid()
        {
            // Arrange
            var userId = 1;
            var refreshTokenDto = new RefreshTokenDTO { RefreshToken = "valid_refresh_token" };
            var accountEntity = new Account { Id = userId, RefreshToken = "valid_refresh_token", RefreshTokenExpiryTime = DateTime.UtcNow.AddMinutes(5) };

            _userManagerMock.Setup(x => x.FindByIdAsync(userId.ToString()))
                            .ReturnsAsync(accountEntity);

            _signInManager.Setup(x => x.GenerateJwtTokenAsync(accountEntity))
                          .ReturnsAsync("new_jwt_token");

            _signInManager.Setup(x => x.GenerateRefreshTokenAsync(accountEntity))
                          .ReturnsAsync("new_refresh_token");
            // Act
            var result = await _accountService.CreateNewJWTPair(refreshTokenDto, userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("new_jwt_token", result.Token);
            Assert.Equal("new_refresh_token", result.RefreshToken);
        }
    }
}