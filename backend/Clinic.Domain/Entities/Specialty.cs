using Clinic.Domain.Common;
using System.Collections.Generic;

namespace Clinic.Domain.Entities
{
    public class Specialty : BaseEntity
    {
        /// <summary>UNIQUE.</summary>
        public string Name { get; protected set; } = string.Empty;

        public string? Description { get; protected set; }

        public DateOnly EstablishedDate { get; protected set; }

        public ICollection<WorkHistory> WorkHistories { get; protected set; } = new List<WorkHistory>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Specialty"/> class with the specified name and description.
        /// </summary>
        /// <exception cref="ArgumentException"></exception>
        public Specialty(string name, string? description, DateOnly establishedDate)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Specialty name cannot be null or empty.", nameof(name));
            if (establishedDate > new TimeConverter().Today)
                throw new ArgumentException("Established date cannot be in the future.", nameof(establishedDate));
            Name = name;
            Description = description;
            EstablishedDate = establishedDate;
        }

        /// <summary>
        /// Reconstruct a <see cref="Specialty"/> instance from the database with the specified parameters.
        /// </summary>
        public Specialty(Guid id, string name, string? description, DateOnly establishedDate, DateTime createdAt, DateTime? updatedAt, bool isDeleted)
            : base(id, createdAt, updatedAt, isDeleted)
        {
            Name = name;
            Description = description;
            EstablishedDate = establishedDate;
        }

        public void Update(string name, string? description, DateOnly establishedDate)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Specialty name cannot be null or empty.", nameof(name));
            if (establishedDate > new TimeConverter().Today)
                throw new ArgumentException("Established date cannot be in the future.", nameof(establishedDate));
            Name = name;
            Description = description;
            EstablishedDate = establishedDate;
            MarkUpdated();
        }

        public void Delete()
        {
            IsDeleted = true;
            MarkUpdated();
        }
    }
}
