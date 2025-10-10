namespace studentsData.Entities
{
    public class LeaveBalance
    {
        public int LeaveBalanceId { get; set; }
        public string EmployeeId { get; set; }
        public ApplicationUser Employee { get; set; }
        public int AnnualLeave { get; set; } = 18;
        public int SickLeave { get; set; } = 10;
        public  int CasualLeave { get; set; } = 6;

    }
}
