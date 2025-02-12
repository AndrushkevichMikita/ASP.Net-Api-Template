using ApiTemplate.Application.Interfaces;
using ApiTemplate.Application.Models;
using ApiTemplate.Domain.Entities;
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
            var appUser = await _signInManager.UserManager.FindByIdAsync(userId.ToString()) ?? throw new MyApplicationException(ErrorStatus.NotFound, "User not found");
            return _mapper.Map<AccountDTO>(appUser);
        }

        public async Task CreateAccount(CreateAccountDTO model)
        {
            await DeleteSameNotConfirmed(model.Email);

            var toInsert = _mapper.Map<AccountEntity>(model);

            var res = await _signInManager.UserManager.CreateAsync(toInsert, model.Password);
            if (!res.Succeeded) throw new MyApplicationException(ErrorStatus.InvalidData, string.Join(" ", res.Errors.Select(c => c.Description)));

            await _signInManager.UserManager.AddToRoleAsync(toInsert, model.Role.ToString());
        }

        private async Task<AccountEntity> DeleteSameNotConfirmed(string email)
        {
            var existingUser = await _signInManager.UserManager.FindByEmailAsync(email);
            if (existingUser == null) return null;

            if (existingUser.EmailConfirmed)
                throw new MyApplicationException(ErrorStatus.InvalidData, "User already exists");

            await _signInManager.UserManager.DeleteAsync(existingUser);
            return existingUser;
        }

        public async Task<RefreshTokenDTO> LoginAccount(LoginAccountDTO model)
        {
            var appUser = await _signInManager.UserManager.FindByEmailAsync(model.Email) ?? throw new MyApplicationException(ErrorStatus.NotFound, "User not found");
            if (!await _signInManager.UserManager.IsEmailConfirmedAsync(appUser)) throw new MyApplicationException(ErrorStatus.InvalidData, "Email unconfirmed");

            var res = await _signInManager.PasswordSignInAsync(appUser, model.Password, model.RememberMe, false);
            if (!res.Succeeded) throw new MyApplicationException(ErrorStatus.InvalidData, "Password or user invalid");

            return await CreateNewJwtPair(appUser);
        }

        public async Task<RefreshTokenDTO> CreateNewJwtPair(RefreshTokenDTO model, int userId)
        {
            var appUser = await _signInManager.UserManager.FindByIdAsync(userId.ToString());

            if (appUser == null || appUser.RefreshToken != model.RefreshToken || appUser.RefreshTokenExpiryTime <= DateTime.UtcNow)
                throw new MyApplicationException(ErrorStatus.Unauthorized);

            return await CreateNewJwtPair(appUser);
        }

        private async Task<RefreshTokenDTO> CreateNewJwtPair(AccountEntity appUser)
        {
            return new RefreshTokenDTO
            {
                Token = await _signInManager.GenerateJwtTokenAsync(appUser),
                RefreshToken = await _signInManager.GenerateRefreshTokenAsync(appUser)
            };
        }

        public async Task SendDigitCodeByEmail(string email)
        {
            var appUser = await _signInManager.UserManager.FindByEmailAsync(email) ?? throw new MyApplicationException(ErrorStatus.NotFound, "User not found");
            var asString = TokenEnum.EmailToken.ToString();

            var userEmailTokens = await _userTokenRepo.GetIQueryable()
                                                      .Where(x => x.UserId == appUser.Id && x.LoginProvider == asString)
                                                      .ToListAsync();
            if (userEmailTokens != null)
                await _userTokenRepo.DeleteAsync(userEmailTokens);

            var digitCode = new Random().Next(1111, 9999).ToString("D4");

            await _userTokenRepo.InsertAsync(new AccountTokenEntity
            {
                UserId = appUser.Id,
                Name = digitCode,
                LoginProvider = asString,
                Value = await _signInManager.UserManager.GenerateEmailConfirmationTokenAsync(appUser)
            }, true);

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

            if (tokens.Count > 1 || tokens.Count == 0)
                throw new MyApplicationException(ErrorStatus.InvalidData, "This code is invalid, please request a new one");

            var token = tokens.First();
            var result = await _signInManager.UserManager.ConfirmEmailAsync(token.User, token.Value);
            if (!result.Succeeded)
                throw new MyApplicationException(ErrorStatus.InvalidData, result.Errors.FirstOrDefault()!.Description);

            await _userTokenRepo.DeleteAsync(token, true);
        }

        public async Task DeleteAccount(string password, int accountId)
        {
            var account = await _signInManager.UserManager.FindByIdAsync(accountId.ToString());

            var res = await _signInManager.UserManager.CheckPasswordAsync(account, password);
            if (!res) throw new MyApplicationException(ErrorStatus.InvalidData, "Password invalid");

            await _signInManager.UserManager.DeleteAsync(account);
        }
    }
}
