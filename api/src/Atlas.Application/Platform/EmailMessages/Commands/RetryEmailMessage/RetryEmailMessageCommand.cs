namespace Atlas.Application.Platform.EmailMessages.Commands.RetryEmailMessage;

public record RetryEmailMessageCommand(long Id) : IRequest<Unit>;

public class RetryEmailMessageCommandHandler
    : IRequestHandler<RetryEmailMessageCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public RetryEmailMessageCommandHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(
        RetryEmailMessageCommand request,
        CancellationToken cancellationToken)
    {
        var message = await _context.EmailMessages
            .FirstOrDefaultAsync(
                item => item.Id == request.Id,
                cancellationToken);

        if (message is null)
        {
            throw new KeyNotFoundException(
                $"Mensagem de e-mail identificada por '{request.Id}' não foi encontrada.");
        }

        message.Retry();

        await _context.SaveChangesAsync(
            cancellationToken);

        return Unit.Value;
    }
}
