namespace studentsData.Entities
{
    public class LeaveRequest
    {
        public int LeaveRequestID {get;set;}
        public string EmployeeId {get;set;}
        public ApplicationUser Employee {get;set;}

        public string LeaveType {get;set;}
        public DateTime StartDate {get;set;}
        public DateTime EndDate {get;set;}
        public string Reason {get;set;}

        public string Status {get;set;}
        public string AdminRemarks { get;set;}
        public DateTime DateSubmitted { get;set;} = DateTime.UtcNow;

    }
}
