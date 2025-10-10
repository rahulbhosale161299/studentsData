using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using studentsData.Data;
using studentsData.DTO.Auth;
using studentsData.Entities;
using studentsData.Services;

namespace studentsData.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _db;

        public AuthController(UserManager<ApplicationUser> userManager, IAuthService authService, ApplicationDbContext db)
        {
            _userManager = userManager;
            _authService = authService;
            _db = db;
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                Name = dto.Name,
                Department = dto.Department,
                Designation = dto.Designation,
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, "Employee");

            var lb = new LeaveBalance { EmployeeId = user.Id };
            _db.LeaveBalances.Add(lb);
            await _db.SaveChangesAsync();

            var token = await _authService.GenerateJwtToken(user);
            return Ok(new { token });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user == null) return Unauthorized("Invalid Credentials");

            var valid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!valid) return Unauthorized("Invalid Credentials");

            var token = await _authService.GenerateJwtToken(user);
            return Ok(new { token });
        }
    }
}
