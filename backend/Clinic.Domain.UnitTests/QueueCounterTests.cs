using Clinic.Domain.Entities;

namespace Clinic.Domain.UnitTests
{
    public class QueueCounterTests
    {
        [Fact]
        public void CreateQueueCounter_ShouldInitializeWithDoctorAndDate()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var date = DateOnly.FromDateTime(DateTime.UtcNow);

            // Act
            var counter = new QueueCounter
            {
                DoctorId = doctorId,
                Date = date,
                CurrentNumber = 1
            };

            // Assert
            Assert.Equal(doctorId, counter.DoctorId);
            Assert.Equal(date, counter.Date);
            Assert.Equal(1, counter.CurrentNumber);
        }

        [Fact]
        public void IncrementQueueCounter_ShouldAdvanceSequence()
        {
            // Arrange
            var counter = new QueueCounter
            {
                DoctorId = Guid.NewGuid(),
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                CurrentNumber = 5
            };

            // Act
            counter.CurrentNumber++;

            // Assert
            Assert.Equal(6, counter.CurrentNumber);
        }
    }
}
