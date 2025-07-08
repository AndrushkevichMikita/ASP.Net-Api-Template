using ApiTemplate.Application.Interfaces;
using ApiTemplate.Application.Models;
using ApiTemplate.Domain.Entities;
using ApiTemplate.Domain.Exceptions;
using ApiTemplate.SharedKernel.ExceptionHandler;
using AutoMapper;
using Microsoft.EntityFrameworkCore;

namespace ApiTemplate.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IRepository<AccountTokenEntity> _userTokenRepo;
        private readonly IEmailTemplateService _emailTemplateService;
        private readonly ApplicationSignInManager _signInManager;
        private readonly IMapper _mapper;

        public AccountService(IEmailTemplateService emailTemplateService,
                              IRepository<AccountTokenEntity> userTokenRepo,
                              ApplicationSignInManager signManager,
                              IMapper mapper)
        {
            _emailTemplateService = emailTemplateService;
            _userTokenRepo = userTokenRepo;
            _signInManager = signManager;
            _mapper = mapper;
        }

        public Task SignOut()
        {
            return _signInManager.SignOutAsync();
        }

        public async Task<AccountDTO> GetAccount(int userId)
        {
            var appUser = await _signInManager.UserManager.FindByIdAsync(userId.ToString())
                ?? throw new MyApplicationException(ErrorStatus.NotFound, "User not found");

            return _mapper.Map<AccountDTO>(appUser);
        }

        public async Task CreateAccount(CreateAccountDTO model)
        {
            await DeleteSameNotConfirmed(model.Email);

            var account = _mapper.Map<Account>(model);

            await account.CreateAsync(_signInManager.UserManager, model.Password);

            await account.AssignRoleAsync(_signInManager.UserManager, model.Role);
        }

        private async Task<Account> DeleteSameNotConfirmed(string email)
        {
            var account = await _signInManager.UserManager.FindByEmailAsync(email);
            if (account == null)
                return null;

            try
            {
                await account.EnsureEmailConfirmedAsync(_signInManager.UserManager);
            }
            catch (DomainException)
            {
                await account.DeleteAsync(_signInManager.UserManager);
                return account;
            }

            throw new MyApplicationException(ErrorStatus.InvalidData, "User already exists");
        }

        public async Task<RefreshTokenDTO> LoginAccount(LoginAccountDTO model)
        {
            var account = await _signInManager.UserManager.FindByEmailAsync(model.Email)
                ?? throw new MyApplicationException(ErrorStatus.NotFound, "User not found");

            await account.EnsureEmailConfirmedAsync(_signInManager.UserManager);

            await account.EnsurePasswordAsync(_signInManager.UserManager, model.Password);

            // this call responsible for cookie authentication
            await _signInManager.SignInAsync(account, model.RememberMe);

            return await CreateNewJwtPair(account);
        }

        public async Task<RefreshTokenDTO> CreateNewJWTPair(RefreshTokenDTO model, int userId)
        {
            var account = await _signInManager.UserManager.FindByIdAsync(userId.ToString());

            account.ValidateRefreshToken(model.RefreshToken);

            return await CreateNewJwtPair(account);
        }

        private async Task<RefreshTokenDTO> CreateNewJwtPair(Account appUser)
        {
            return new RefreshTokenDTO
            {
                Token = await _signInManager.GenerateJwtTokenAsync(appUser),
                RefreshToken = await _signInManager.GenerateRefreshTokenAsync(appUser)
            };
        }

        public async Task SendDigitCodeByEmail(string email)
        {
            var appUser = await _signInManager.UserManager.FindByEmailAsync(email)
                ?? throw new MyApplicationException(ErrorStatus.NotFound, "User not found");

            var asString = TokenEnum.EmailToken.ToString();

            var userEmailTokens = await _userTokenRepo.GetIQueryable()
                                                      .Where(x => x.UserId == appUser.Id && x.LoginProvider == asString)
                                                      .ToListAsync();
            if (userEmailTokens != null)
                await _userTokenRepo.DeleteAsync(userEmailTokens);

            var digitCode = new Random().Next(1111, 9999).ToString("D4");

            var token = await AccountTokenEntity.CreateAsync(appUser, digitCode, asString, _signInManager.UserManager);

            await _userTokenRepo.InsertAsync(token, true);

            await _emailTemplateService.SendDigitCodeAsync(new EmailDTO
            {
                UserEmail = appUser.Email,
                DigitCode = digitCode,
                FirstName = appUser.FirstName,
                LastName = appUser.LastName
            });
        }

        public async Task ConfirmDigitCode(string digitCode)
        {
            var tokens = await _userTokenRepo.GetIQueryable()
                                             .Include(x => x.User)
                                             .Where(x => x.Name == digitCode && x.LoginProvider == TokenEnum.EmailToken.ToString())
                                             .ToListAsync();

            AccountTokenEntity.EnsureNoDuplicateTokensByNameAndProvider(tokens);

            var token = tokens.First();

            await token.User.EnsureConfirmEmailAsync(_signInManager.UserManager, token);

            await _userTokenRepo.DeleteAsync(token, true);
        }

        public async Task DeleteAccount(string password, int accountId)
        {
            var account = await _signInManager.UserManager.FindByIdAsync(accountId.ToString());

            await account.EnsurePasswordAsync(_signInManager.UserManager, password);

            await account.DeleteAsync(_signInManager.UserManager);
        }
    }
}
