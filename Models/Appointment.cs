using System;
namespace PatientAppointmentManager.Models
{
    public class Appointment
    {
        public long Id { get; set; }
        public string PatientCode { get; set; }
        public string PatientName { get; set; }
        public string ExamCode { get; set; }
        public string ServiceName { get; set; }
        public DateTime AppointmentDate { get; set; }
        public string Shift { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReturnedAt { get; set; }
    }
}
