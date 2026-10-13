namespace Atlas.Application.Platform.SystemParameters.Commands.DeleteSystemParameter;

[Authorize(Policy = PermissionKeys.Parameters.Delete)]
public record DeleteSystemParameterCommand(string Key, uint Version) : IRequest<Result<Unit>>;

public class DeleteSystemParameterCommandHandler(
    IApplicationDbContext context,
    ISystemParameterProvider cacheProvider)
    : IRequestHandler<DeleteSystemParameterCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(
        DeleteSystemParameterCommand request,
        CancellationToken cancellationToken)
    {
        var parameter = await context.SystemParameters
            .FirstOrDefaultAsync(p => p.Key == request.Key, cancellationToken);

        if (parameter == null)
        {
            return Result<Unit>.Failure([$"O parâmetro '{request.Key}' não foi localizado."]);
        }

        context.Entry(parameter).Property<uint>("Version").OriginalValue = request.Version;
        context.SystemParameters.Remove(parameter);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<Unit>.Failure([$"O parâmetro '{request.Key}' foi modificado por outro usuário antes de ser excluído. Recarregue a página e tente novamente."]);
        }

        cacheProvider.InvalidateCache(request.Key);

        return Result<Unit>.Success(Unit.Value);
    }
}
