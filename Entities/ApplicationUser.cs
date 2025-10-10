using Microsoft.AspNetCore.Identity;

namespace studentsData.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string Name {  get; set; }
        public string? Department { get; set; }
        public string Designation { get; set; }

    }
}
