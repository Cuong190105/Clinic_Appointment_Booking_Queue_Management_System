using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Transactions;

namespace Clinic.Application.Contracts
{
    public class DashboardDataDto
    {
        public IValueWithChange TotalAppointments { get; set; }
        public IValueWithChange TotalPatients { get; set; }
        public IValueWithChange TotalDoctors { get; set; }
        public IValueWithChange ActiveDoctor { get; set; }
        public IValueWithChange CompletionRate { get; set; }
        public IValueWithChange AverageWaitingMinute { get; set; }
    }

    public interface IValueWithChange
    {
        object Value { get; }
        double Change { get; }
    }

    public class ValueWithChange<T> : IValueWithChange
    {
        public T Value { get; set; } = default!;
        public double Change { get; set; } = 0;

        object IValueWithChange.Value => Value!;
    }
}
