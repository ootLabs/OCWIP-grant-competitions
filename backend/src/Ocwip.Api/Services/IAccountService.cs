using Microsoft.AspNetCore.Identity;
using Ocwip.Api.Contracts;



namespace Ocwip.Api.Services
{
    public interface IAccountService
    {
        Task<IdentityResult> RegisterAsync(RegisterRequest request);
    }
}
