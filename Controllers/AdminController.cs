using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using studentsData.Data;

namespace studentsData.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AdminController(ApplicationDbContext db) => _db = db;

        [HttpGet("pending-leaves")]
        public async Task<IActionResult> PendingLeaves()
        {
            var pending = await _db.LeaveRequests
                .Where(l => l.Status == "Pending")
                .Include(l => l.Employee)
                .OrderBy(l => l.DateSubmitted)
                .ToListAsync();
            return Ok(pending);
        }

        [HttpPut("approve-leave/{id}")]
        public async Task<IActionResult> Approve(int id, [FromBody] string adminRemarks)
        {
            var lr = await _db.LeaveRequests.FindAsync(id);
            if (lr == null) return NotFound();

            if (lr.Status != "Pending") return BadRequest("Only pending requests can be approved.");

            var days = (lr.EndDate.Date - lr.StartDate.Date).Days + 1;

            var lb = await _db.LeaveBalances.FirstOrDefaultAsync(x => x.EmployeeId == lr.EmployeeId);
            if (lb == null) return BadRequest("Leave balance not found.");

            // Deduct based on leave type - simple mapping
            switch (lr.LeaveType.ToLower())
            {
                case "annual":
                    if (lb.AnnualLeave < days) return BadRequest("Insufficient annual leave balance.");
                    lb.AnnualLeave -= days;
                    break;
                case "sick":
                    if (lb.SickLeave < days) return BadRequest("Insufficient sick leave balance.");
                    lb.SickLeave -= days;
                    break;
                case "casual":
                    if (lb.CasualLeave < days) return BadRequest("Insufficient casual leave balance.");
                    lb.CasualLeave -= days;
                    break;
                default:
                    break;
            }

            lr.Status = "Approved";
            lr.AdminRemarks = adminRemarks;
            await _db.SaveChangesAsync();

            // TODO: notify employee

            return Ok(lr);
        }

        [HttpPut("reject-leave/{id}")]
        public async Task<IActionResult> Reject(int id, [FromBody] string adminRemarks)
        {
            var lr = await _db.LeaveRequests.FindAsync(id);
            if (lr == null) return NotFound();
            if (lr.Status != "Pending") return BadRequest("Only pending requests can be rejected.");

            lr.Status = "Rejected";
            lr.AdminRemarks = adminRemarks;
            await _db.SaveChangesAsync();

            // TODO: notify employee

            return Ok(lr);
        }

        [HttpGet("leave-history")]
        public async Task<IActionResult> LeaveHistory()
        {
            var list = await _db.LeaveRequests
                .Include(l => l.Employee)
                .OrderByDescending(l => l.DateSubmitted)
                .ToListAsync();
            return Ok(list);
        }
    }
}
