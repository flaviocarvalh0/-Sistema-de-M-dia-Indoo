using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.SystemModules.Commands.CreateSystemModule;

public record CreateSystemModuleResponse(long Id, uint Version);
public record CreateSystemModuleCommand(
    string Name,
    string Description,
    string RoutePrefix,
    int SortOrder,
    long? SystemIconId,
    string? HexColor,
    List<long> EntityMetadataIds) : IRequest<Result<CreateSystemModuleResponse>>;

public class CreateSystemModuleCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateSystemModuleCommand, Result<CreateSystemModuleResponse>>
{
    public async Task<Result<CreateSystemModuleResponse>> Handle(
        CreateSystemModuleCommand request,
        CancellationToken cancellationToken)
    {
        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Sanitização do parâmetro de entrada
            var entityIds = request.EntityMetadataIds
                .Distinct()
                .ToList();

            // 2. Busca todas as entidades selecionadas
            var entities = await context.EntityMetadata
                .Where(x => entityIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (entities.Count != entityIds.Count)
            {
                return Result<CreateSystemModuleResponse>.Failure(["Uma ou mais entidades selecionadas não foram encontradas."]);
            }

            // Verifica se alguma das entidades já possui vínculo com outro módulo
            var alreadyBoundEntities = entities.Where(x => x.SystemModuleId.HasValue).ToList();
            if (alreadyBoundEntities.Any())
            {
                var names = string.Join(", ", alreadyBoundEntities.Select(x => x.TableName));
                return Result<CreateSystemModuleResponse>.Failure([$"As seguintes entidades já estão vinculadas a outro módulo: {names}."]);
            }

            // 3. Instancia o novo Módulo
            var module = new SystemModule
            {
                Name = request.Name,
                Description = request.Description,
                RoutePrefix = request.RoutePrefix.StartsWith("/") ? request.RoutePrefix : $"/{request.RoutePrefix}",
                SortOrder = request.SortOrder,
                SystemIconId = request.SystemIconId,
                HexColor = request.HexColor ?? string.Empty
            };

            context.SystemModules.Add(module);
            await context.SaveChangesAsync(cancellationToken); // Gerado o ID e o xmin inicial do módulo

            // 4. Vincula as entidades ao novo Módulo e cria os itens de menu padrão
            int menuSortOrder = 1;

            foreach (var entity in entities)
            {
                entity.SystemModuleId = module.Id;

                var menuItem = new SystemMenuItem
                {
                    DisplayLabel = entity.DisplayLabel,
                    SortOrder = menuSortOrder++,
                    MenuType = SystemMenuType.Entity,
                    SystemModuleId = module.Id,
                    EntityMetadataId = entity.Id,
                    ParentId = null
                };

                context.SystemMenuItems.Add(menuItem);
            }

            // Persiste todas as associações e menus criados
            await context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            // 5. Captura o xmin gerado pelo PostgreSQL para o novo módulo
            var initialVersion = context.Entry(module).Property<uint>("Version").CurrentValue;

            return Result<CreateSystemModuleResponse>.Success(
                new CreateSystemModuleResponse(module.Id, initialVersion)
            );
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
