namespace Atlas.Application.Platform.SystemParameters.Commands.UpdateSystemParameter;

[Authorize(Policy = PermissionKeys.Parameters.Update)]
public record UpdateSystemParameterCommand : IRequest<Result<uint>>
{
    public long Id { get; init; }
    public string Key { get; init; } = null!;
    public string Value { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string Group { get; init; } = null!;
    public string DataType { get; init; } = null!;
    public uint Version { get; init; }
}

public class UpdateSystemParameterCommandHandler(
    IApplicationDbContext context,
    ISystemParameterProvider systemParameterProvider)
    : IRequestHandler<UpdateSystemParameterCommand, Result<uint>>
{
    public async Task<Result<uint>> Handle(
        UpdateSystemParameterCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Busca pelo ID técnico
        var parameter = await context.SystemParameters
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (parameter == null)
        {
            return Result<uint>.Failure([$"O parâmetro com ID '{request.Id}' não foi localizado no sistema."]);
        }

        var formattedKey = request.Key.Trim().ToUpperInvariant().Replace(" ", "_");
        var oldKey = parameter.Key;

        // 2. Se mudou a chave, valida se a nova chave já não pertence a OUTRO parâmetro
        if (oldKey != formattedKey)
        {
            var keyExists = await context.SystemParameters
                .AnyAsync(p => p.Key == formattedKey && p.Id != request.Id, cancellationToken);

            if (keyExists)
            {
                return Result<uint>.Failure([$"A chave de parâmetro '{formattedKey}' já está em uso por outro registro."]);
            }
        }

        // 3. Controle de Concorrência
        context.Entry(parameter).Property<uint>("Version").OriginalValue = request.Version;

        // 4. Atualiza os dados do parâmetro
        parameter.Key = formattedKey;
        parameter.Value = request.Value;
        parameter.Description = request.Description;
        parameter.Group = request.Group.Trim();
        parameter.DataType = request.DataType.Trim();

        await context.SaveChangesAsync(cancellationToken);

        // 5. Invalida o cache da chave antiga (se mudou) E da nova chave
        systemParameterProvider.InvalidateCache(oldKey);
        if (oldKey != formattedKey)
        {
            systemParameterProvider.InvalidateCache(formattedKey);
        }

        var newVersion = context.Entry(parameter).Property<uint>("Version").CurrentValue;
        return Result<uint>.Success(newVersion);
    }
}
