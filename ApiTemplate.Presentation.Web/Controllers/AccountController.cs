using ApiTemplate.Application.Interfaces;
using ApiTemplate.Application.Models;
using ApiTemplate.Domain.Entities;
using ApiTemplate.Presentation.Web.Models;
using ApiTemplate.SharedKernel;
using ApiTemplate.SharedKernel.FiltersAndAttributes;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiTemplate.Presentation.Web.Controllers
{
    public class AccountController : BaseController<RoleEnum>
    {
        private readonly IMapper _mapper;
        private readonly IAccountService _account;

        public AccountController(IAccountService account,
                                 IMapper mapper)
        {
            _mapper = mapper;
            _account = account;
        }

        [Authorize(AuthenticationSchemes = PresentationDependencyInjection.JWTWithNoExpirationSchema)]
        [HttpPost("refreshToken")]
        public async Task<RefreshTokenModel> RefreshToken([FromBody] RefreshTokenModel model)
        {
            var dto = _mapper.Map<RefreshTokenDTO>(model);
            return _mapper.Map<RefreshTokenModel>(await _account.CreateNewJwtPair(dto, CurrentUser.Id));
        }

        /// <summary>
        /// Get current authenticated user
        /// </summary>
        /// <returns></returns>
        [HttpGet()]
        public async Task<AccountModel> GetCurrent()
        {
            var dto = await _account.GetCurrent(CurrentUser.Id);
            return _mapper.Map<AccountModel>(dto);
        }

        [AllowAnonymous]
        [HttpPost("signUp")]
        public async Task CreateAccount(CreateAccountModel model)
        {
            var dto = _mapper.Map<CreateAccountDTO>(model);
            await _account.CreateAccount(dto);
        }

        [AllowAnonymous]
        [HttpPost("signIn")]
        public async Task<RefreshTokenModel> LoginAccount(LoginAccountModel model)
        {
            var dto = _mapper.Map<LoginAccountDTO>(model);
            return _mapper.Map<RefreshTokenModel>(await _account.LoginAccount(dto));
        }

        [AllowAnonymous]
        [HttpPost("digitCode")]
        public async Task SendDigitCodeByEmail([FromBody] string email)
            => await _account.SendDigitCodeByEmail(email);

        [AllowAnonymous]
        [HttpPut("digitCode")]
        public async Task ConfirmDigitCode([FromBody] string code)
            => await _account.ConfirmDigitCode(code);

        /// <summary>
        /// Signs the current user out of the application.
        /// </summary>
        /// <returns></returns>
        [HttpPost("signOut")]
        public new async Task<SignOutResult> SignOut()
        {
            base.SignOut();
            await _account.SignOut();
            return new SignOutResult();
        }

        [AuthorizeRoles(RoleEnum.SuperAdmin)]
        [HttpGet("onlyForSupAdmin")]
        public IActionResult AllowOnlyForSupAdmin()
            => Ok();

        [HttpGet("authorize")]
        public IActionResult CheckAuthorization()
        {
            var r = User.IsInRole(RoleEnum.SuperAdmin.ToString());
            return Ok(User.Claims.Select(c => c.Value).ToList());
        }

        /// <summary>
        /// Delete user if password verification is successful
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        [HttpDelete()]
        public async Task Delete([FromBody] string password)
            => await _account.Delete(password, CurrentUser.Id);
    }
}
