namespace Atlas.Application.Platform.SystemIcons.Commands.DeleteSystemIcon;

public record DeleteSystemIconCommand(long Id, uint Version) : IRequest<Result>;

public class DeleteSystemIconCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DeleteSystemIconCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSystemIconCommand request,
        CancellationToken cancellationToken)
    {
        var icon = await context.SystemIcons
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (icon is null)
        {
            return Result.Failure(["Ícone não encontrado no sistema."]);
        }

        context.Entry(icon).Property<uint>("Version").OriginalValue = request.Version;

        context.SystemIcons.Remove(icon);

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
