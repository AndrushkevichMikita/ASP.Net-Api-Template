using ApiTemplate.Application.Interfaces;
using ApiTemplate.Domain.Entities;
using ApiTemplate.Presentation.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ApiTemplate.Presentation.Web.Tests.Integration.Account
{
    /// <inheritdoc />
    public class AccountTests : BaseIntegrationTest
    {
        public AccountTests(TestsWebApplicationFactory factory) : base(factory) { }

        private async Task<(HttpResponseMessage createAccount, ProblemDetails model)> CreateAccount(CreateAccountModel model)
        {
            var create = await HTTPClient.PostAsJsonAsync("api/account/signUp", model);
            if (!create.IsSuccessStatusCode)
            {
                return (create, JsonSerializer.Deserialize<ProblemDetails>(await create.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
            }
            return (create, null);
        }

        private async Task<(HttpResponseMessage loginAccount, RefreshTokenModel model)> LoginAccount(LoginAccountModel model)
        {
            var response = await HTTPClient.PostAsJsonAsync("api/account/signIn", model);
            return (response, JsonSerializer.Deserialize<RefreshTokenModel>(await response.Content.ReadAsStringAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
        }

        private async Task<HttpResponseMessage> SignOutAccount()
             => await HTTPClient.PostAsync("api/account/signOut", null);

        private async Task<HttpResponseMessage> ProtectedBySuperAdminAccount()
             => await HTTPClient.GetAsync("api/account/onlyForSupAdmin");

        private async Task<HttpResponseMessage> SendDigitCode(string email)
             => await HTTPClient.PostAsJsonAsync("api/account/digitCode", email);

        private async Task<HttpResponseMessage> ConfirmUserEmail(string code)
             => await HTTPClient.PutAsJsonAsync("api/account/digitCode", code);

        private IRepository<AccountEntity> AccountRepo
             => ServicesScope.ServiceProvider.GetRequiredService<IRepository<AccountEntity>>();

        private IRepository<AccountTokenEntity> TokensRepo
             => ServicesScope.ServiceProvider.GetRequiredService<IRepository<AccountTokenEntity>>();

        private async Task<(HttpResponseMessage createAccount,
                            HttpResponseMessage sendDigitCode,
                            HttpResponseMessage confirmUserEmail,
                            bool confirmedAccount)> CreateAndConfirmAccount(CreateAccountModel createModel)
        {
            var createAccount = await CreateAccount(createModel);
            var sendDigitCode = await SendDigitCode(createModel.Email);

            var lastToken = await TokensRepo.GetIQueryable()
                                            .FirstAsync(x => x.LoginProvider == TokenEnum.EmailToken.ToString());

            var confirmUserEmail = await ConfirmUserEmail(lastToken.Name);

            var confirmedAccount = await AccountRepo.GetIQueryable()
                                                    .Where(c => c.Id == lastToken.UserId)
                                                    .FirstAsync();

            return (createAccount.createAccount, sendDigitCode, confirmUserEmail, confirmedAccount.EmailConfirmed);
        }

        private static CreateAccountModel CreateAccountModel() =>
            new()
            {
                Role = RoleEnum.SuperAdmin,
                FirstName = $"TestUp{Guid.NewGuid()}",
                LastName = $"TestUp{Guid.NewGuid()}",
                Password = $"Vdvdrvrd58w!",
                Email = $"test{Guid.NewGuid()}@gmail.com"
            };

        [Fact]
        public async Task Should_Create_User_Successfully()
        {
            // Act
            var createAccount = await CreateAccount(CreateAccountModel());

            // Assert
            Assert.True(createAccount.createAccount.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Should_Confirm_User_Email_Successfully()
        {
            // Act
            var (createAccount, sendDigitCode, confirmUserEmail, isEmailConfirmed) = await CreateAndConfirmAccount(CreateAccountModel());

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(sendDigitCode.IsSuccessStatusCode);
            Assert.True(confirmUserEmail.IsSuccessStatusCode);
            Assert.True(isEmailConfirmed);
        }

        [Fact]
        public async Task Should_Delete_Same_Unconfirmed_User()
        {
            // Arrange
            var createModel = CreateAccountModel();

            // Act
            var createAccount1 = await CreateAccount(createModel);
            var createAccount2 = await CreateAccount(createModel);

            // Assert
            Assert.True(createAccount1.createAccount.IsSuccessStatusCode);
            Assert.True(createAccount2.createAccount.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Should_Login_User_Successfully()
        {
            // Arrange
            var createModel = CreateAccountModel();

            // Act
            var (createAccount, sendDigitCode, confirmUserEmail, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            });

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(sendDigitCode.IsSuccessStatusCode);
            Assert.True(confirmUserEmail.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Should_Login_User_Failed()
        {
            // Arrange
            var createModel = CreateAccountModel();

            var loginModel = new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            };

            // Act
            var createAccount = await CreateAccount(createModel);
            var (loginAccount, _) = await LoginAccount(loginModel);

            // Assert
            Assert.True(createAccount.createAccount.IsSuccessStatusCode);
            Assert.True(!loginAccount.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Should_SignOut_User()
        {
            // Arrange
            var createModel = CreateAccountModel();

            // Act
            var (createAccount, sendDigitCode, confirmUserEmail, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            });
            var signOutRes1 = await SignOutAccount();
            var signOutRes2 = await SignOutAccount();

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(sendDigitCode.IsSuccessStatusCode);
            Assert.True(confirmUserEmail.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
            Assert.True(signOutRes1.IsSuccessStatusCode);
            Assert.True(!signOutRes2.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Should_Not_Reach_Secure_Endpoint_Besides_SuperAdmin()
        {
            // Arrange
            var createModel = CreateAccountModel();
            createModel.Role = RoleEnum.Admin;

            // Act
            var (createAccount, _, _, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            });

            var protectedByAuth = await ProtectedBySuperAdminAccount();

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
            Assert.True(!protectedByAuth.IsSuccessStatusCode && protectedByAuth.StatusCode == HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Should_Reach_Secure_Endpoint_By_SuperAdmin()
        {
            // Arrange
            var createModel = CreateAccountModel();

            // Act
            var (createAccount, _, _, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            });

            var protectedByAuth = await ProtectedBySuperAdminAccount();

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
            Assert.True(protectedByAuth.IsSuccessStatusCode);
        }

        [Fact]
        public async Task Should_Delete_User()
        {
            // Arrange
            var createModel = CreateAccountModel();
            var loginModel = new LoginAccountModel()
            {
                Password = createModel.Password,
                Email = createModel.Email
            };

            // Act
            var (createAccount, _, _, _) = await CreateAndConfirmAccount(createModel);
            var (loginAccount, _) = await LoginAccount(loginModel);
            using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, "api/account");
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(loginModel.Password), Encoding.UTF8, "application/json");
            var deleteR = await HTTPClient.SendAsync(requestMessage);

            // Assert
            Assert.True(createAccount.IsSuccessStatusCode);
            Assert.True(loginAccount.IsSuccessStatusCode);
            Assert.True(deleteR.IsSuccessStatusCode);
        }
    }
}