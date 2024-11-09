using ApiTemplate.Application.Interfaces;
using ApiTemplate.Application.Models;
using ApiTemplate.SharedKernel.PrimitivesExtensions;
using Microsoft.AspNetCore.Hosting;

namespace ApiTemplate.Application.Services
{
    public class EmailTemplateService : IEmailTemplateService
    {
        private readonly ISMTPService _sptmService;
        private readonly IWebHostEnvironment _hosting;

        public EmailTemplateService(IWebHostEnvironment hosting,
                                    ISMTPService sptmService)
        {
            _hosting = hosting;
            _sptmService = sptmService;
        }

        private string GetTemplateFile { get; set; }

        private async Task<string> GetTemplate(string fileName)
        {
            if (!string.IsNullOrEmpty(GetTemplateFile))
                return GetTemplateFile;

            var path = Path.Combine(_hosting.WebRootPath, "emailTemplates", fileName);
            GetTemplateFile = await File.ReadAllTextAsync(path);
            return GetTemplateFile;
        }

        private async Task<string> GetBaseTemplate(string name, string message, string link, string linkText)
        {
            return (await GetTemplate("baseTemplate.html"))
                   .ReplaceFirst("{name}", name.Trim())
                   .ReplaceFirst("{message}", message)
                   .ReplaceFirst("{link}", link)
                   .ReplaceFirst("{linkText}", linkText)
                   .ReplaceFirst("{linkDisplay}", string.IsNullOrEmpty(linkText) ? "none" : "initial");
        }

        public async Task SendDigitCodeAsync(EmailDTO model)
        {
            var html = await GetBaseTemplate(
                       name: $"{model.FirstName} {model.LastName}",
                       message: $"Please use this code: {model.DigitCode}",
                       link: "",
                       linkText: "");

            await _sptmService.SendAsync(model.UserEmail, "Confirmation code", html);
        }

        public async Task SendDigitCodeParallelAsync(List<EmailDTO> models)
        {
            await Parallel.ForEachAsync(models, async (model, token) => await SendDigitCodeAsync(model));
        }
    }
}
