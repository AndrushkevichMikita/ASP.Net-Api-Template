using ApiTemplate.Application.Models;

namespace ApiTemplate.Application.Interfaces
{
    public interface IAccountService
    {
        Task CreateAccount(CreateAccountDTO model);

        Task<RefreshTokenDTO> LoginAccount(LoginAccountDTO model);

        Task<RefreshTokenDTO> CreateNewJWTPair(RefreshTokenDTO model, int userId);

        Task SignOut();

        Task ConfirmDigitCode(string digitCode);

        Task SendDigitCodeByEmail(string email);

        Task DeleteAccount(string password, int accountId);

        Task<AccountDTO> GetAccount(int userId);
    }
}
