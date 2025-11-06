using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using studentsData.Data;
using studentsData.DTO.Leave;
using studentsData.Entities;

namespace studentsData.Controllers
{
    [ApiController]
    [Route("api/employee")]
    [Authorize(Roles = "Employee")]
    public class EmployeeController : ControllerBase
    {

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        public EmployeeController(ApplicationDbContext db, UserManager<ApplicationUser> um)
        {
            _db = db;
            _userManager = um;
        }

        [HttpGet("leave-balance")]
        public async Task<IActionResult> GetLeaveBalance()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var lb = await _db.LeaveBalances.FirstOrDefaultAsync(x => x.EmployeeId == userId);
            return Ok(lb);
        }

        [HttpPost("apply-leave")]
        public async Task<IActionResult> ApplyLeave([FromBody] ApplyLeaveDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (dto.EndDate < dto.StartDate) return BadRequest("EndDate must be >= StartDate");  


            // Overlap check
            var overlap = await _db.LeaveRequests.AnyAsync(l =>
                l.EmployeeId == userId &&
                !(l.EndDate < dto.StartDate || l.StartDate > dto.EndDate)
            );
            if (overlap) return BadRequest("You already have a leave that overlaps this period.");

            var lr = new LeaveRequest
            {
                EmployeeId = userId,
                LeaveType = dto.LeaveType,
                StartDate = dto.StartDate.Date,
                EndDate = dto.EndDate.Date,
                Reason = dto.Reason,
                Status = "Pending",
                AdminRemarks = string.Empty
            };
            _db.LeaveRequests.Add(lr);
            await _db.SaveChangesAsync();

            // TODO: send notification (email) to admins

            return Ok(lr);
        }

        [HttpGet("leave-requests")]
        public async Task<IActionResult> GetMyRequests()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var list = await _db.LeaveRequests
                .Where(l => l.EmployeeId == userId)
                .OrderByDescending(l => l.DateSubmitted)
                .ToListAsync();
            return Ok(list);
        }
    }
}
