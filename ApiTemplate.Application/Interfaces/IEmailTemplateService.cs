using ApiTemplate.Application.Models;

namespace ApiTemplate.Application.Interfaces
{
    public interface IEmailTemplateService
    {
        Task SendDigitCodeAsync(EmailDTO model);

        Task SendDigitCodeParallelAsync(List<EmailDTO> models);
    }
}
