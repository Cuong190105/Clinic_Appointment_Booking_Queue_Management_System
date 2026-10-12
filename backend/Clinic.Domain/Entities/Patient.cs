using Clinic.Domain.Common;
using System;
using System.Collections.Generic;

namespace Clinic.Domain.Entities
{
    public class Patient : BaseEntity
    {
        /// <summary>FK, UNIQUE. Cho phép Person chưa từng có User (khách vãng lai).</summary>
        public Guid PersonId { get; protected set; }
        public Person Person { get; protected set; } = null!;

        public string? InsuranceNumber { get; protected set; }

        public string? EmergencyContact { get; protected set; }

        public ICollection<Appointment> Appointments { get; protected set; } = new List<Appointment>();

        public Patient(Person person, string? insuranceNumber, string? emergencyContact)
        {
            PersonId = person.Id;
            Person = person;
            InsuranceNumber = insuranceNumber;
            EmergencyContact = emergencyContact;
        }

        /// <summary>
        /// Reconstruct a <see cref="Patient"/> instance from the database with the specified parameters.
        /// </summary>
        public Patient(Guid id, Guid personId, string? insuranceNumber, string? emergencyContact, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            PersonId = personId;
            InsuranceNumber = insuranceNumber;
            EmergencyContact = emergencyContact;
        }

        private Patient() { } // EF Core

        public void Update(string? insuranceNumber, string? emergencyContact)
        {
            InsuranceNumber = insuranceNumber;
            EmergencyContact = emergencyContact;
            MarkUpdated();
        }
    }
}
