using ApiTemplate.Application.Models;

namespace ApiTemplate.Application.Interfaces
{
    public interface IAccountService
    {
        Task CreateAccount(CreateAccountDTO model);

        Task<RefreshTokenDTO> LoginAccount(LoginAccountDTO model);

        Task<RefreshTokenDTO> CreateNewJwtPair(RefreshTokenDTO model, int userId);

        Task SignOut();

        Task ConfirmDigitCode(string digitCode);

        Task SendDigitCodeByEmail(string email);

        Task Delete(string password, int accountId);

        Task<AccountDTO> GetCurrent(int userId);
    }
}
