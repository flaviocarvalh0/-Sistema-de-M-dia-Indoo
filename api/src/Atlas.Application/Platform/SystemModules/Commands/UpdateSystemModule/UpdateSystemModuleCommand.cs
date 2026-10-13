namespace Atlas.Application.Platform.SystemModules.Commands.UpdateSystemModule;
public record UpdateSystemModuleCommand(
    long Id,
    string Name,
    string Description,
    string RoutePrefix,
    int SortOrder,
    long? SystemIconId,
    string? HexColor,
    uint Version) : IRequest<Result<uint>>;

public class UpdateSystemModuleCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateSystemModuleCommand, Result<uint>>
{
    public async Task<Result<uint>> Handle(UpdateSystemModuleCommand request, CancellationToken cancellationToken)
    {
        var module = await context.SystemModules
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (module == null)
        {
            return Result<uint>.Failure(["Módulo do sistema não encontrado."]);
        }

        // Configura o xmin enviado para checagem de concorrência
        context.Entry(module).Property<uint>("Version").OriginalValue = request.Version;

        module.Name = request.Name;
        module.Description = request.Description;
        module.RoutePrefix = request.RoutePrefix.StartsWith("/") ? request.RoutePrefix : $"/{request.RoutePrefix}";
        module.SortOrder = request.SortOrder;
        module.SystemIconId = request.SystemIconId;
        module.HexColor = request.HexColor ?? string.Empty;

        await context.SaveChangesAsync(cancellationToken);

        // Captura o xmin sincronizado pós-update
        var newVersion = context.Entry(module).Property<uint>("Version").CurrentValue;

        return Result<uint>.Success(newVersion);
    }
}
