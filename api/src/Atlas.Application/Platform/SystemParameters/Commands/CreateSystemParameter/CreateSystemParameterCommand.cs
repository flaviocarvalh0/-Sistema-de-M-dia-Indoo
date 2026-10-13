using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.SystemParameters.Commands.CreateSystemParameter;

public record CreateSystemParameterResponse(string Key, uint Version);

[Authorize(Policy = PermissionKeys.Parameters.Create)]
public record CreateSystemParameterCommand : IRequest<Result<CreateSystemParameterResponse>>
{
    public string Key { get; init; } = null!;
    public string Value { get; init; } = null!;
    public string Description { get; init; } = null!;
    public string Group { get; init; } = null!;
    public string DataType { get; init; } = null!;
}

public class CreateSystemParameterCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateSystemParameterCommand, Result<CreateSystemParameterResponse>>
{
    public async Task<Result<CreateSystemParameterResponse>> Handle(
        CreateSystemParameterCommand request,
        CancellationToken cancellationToken)
    {
        var formattedKey = request.Key.Trim().ToUpperInvariant().Replace(" ", "_");

        var exists = await context.SystemParameters
            .AnyAsync(p => p.Key == formattedKey, cancellationToken);

        if (exists)
        {
            return Result<CreateSystemParameterResponse>.Failure([$"A chave de parâmetro '{formattedKey}' já está cadastrada."]);
        }

        var parameter = new SystemParameter
        {
            Key = formattedKey,
            Value = request.Value,
            Description = request.Description,
            Group = request.Group.Trim(),
            DataType = request.DataType.Trim()
        };

        context.SystemParameters.Add(parameter);
        await context.SaveChangesAsync(cancellationToken);

        // Captura o xmin gerado na inserção
        var initialVersion = context.Entry(parameter).Property<uint>("Version").CurrentValue;

        return Result<CreateSystemParameterResponse>.Success(
            new CreateSystemParameterResponse(parameter.Key, initialVersion)
        );
    }
}
