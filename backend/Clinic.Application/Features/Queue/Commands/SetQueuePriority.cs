using Clinic.Application.Common.Exceptions;
using Clinic.Application.Interfaces;
using MediatR;

namespace Clinic.Application.Features.Queue.Commands
{
    public record SetQueuePriorityCommand(Guid QueueTicketId, bool Priority) : IRequest;

    public class SetQueuePriorityHandler : IRequestHandler<SetQueuePriorityCommand>
    {
        private readonly IQueueTicketRepository _queueTicketRepository;
        private readonly IUnitOfWork _unitOfWork;

        public SetQueuePriorityHandler(
            IQueueTicketRepository queueTicketRepository,
            IUnitOfWork unitOfWork)
        {
            _queueTicketRepository = queueTicketRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(SetQueuePriorityCommand command, CancellationToken cancellationToken)
        {
            var queueTicket = await _queueTicketRepository.GetByIdAsync(command.QueueTicketId)
                ?? throw new NotFoundException($"Queue ticket '{command.QueueTicketId}' not found.");

            queueTicket.SetPriority(command.Priority);

            await _queueTicketRepository.UpdateAsync(queueTicket);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}