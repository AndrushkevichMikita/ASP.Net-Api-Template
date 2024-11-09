using ApiTemplate.Application.Models;
using ApiTemplate.Presentation.Web.Models;
using AutoMapper;

namespace ApiTemplate.Presentation.Web.Mappings
{
    public class AccountProfile : Profile
    {
        public AccountProfile()
        {
            // Source => Target
            CreateMap<CreateAccountModel, CreateAccountDTO>();
            CreateMap<LoginAccountModel, LoginAccountDTO>();
            CreateMap<RefreshTokenModel, RefreshTokenDTO>();
            CreateMap<RefreshTokenDTO, RefreshTokenModel>();
            CreateMap<AccountDTO, AccountModel>();
        }
    }
}
