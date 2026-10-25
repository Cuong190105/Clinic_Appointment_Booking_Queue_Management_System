namespace Clinic.Contracts.Analytics;

public sealed record AppointmentDashboardMetrics(
    int TotalAppointments,
    double TotalAppointmentsChange,
    double CompletionRate,
    double CompletionRateChange);

public sealed record QueueDashboardMetrics(
    double AverageWaitingMinutes,
    double Change);
