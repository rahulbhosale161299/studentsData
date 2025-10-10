using studentsData.Entities;

namespace studentsData.Services
{
    public interface IAuthService
    {
        Task<string> GenerateJwtToken(ApplicationUser user);
    }
}
