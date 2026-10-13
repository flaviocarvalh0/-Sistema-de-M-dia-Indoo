using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Parceiros.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Indoor.Playlists.Commands.DefinirItensPlaylist;

/// <summary>
/// Grava a grade inteira de uma vez, na ordem da lista (a posição vira a
/// ordem do loop) — é o que a tela de arrastar e soltar envia. Item com id
/// atualiza, sem id entra, ausente sai.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.Update)]
public record DefinirItensPlaylistCommand : IRequest<uint>
{
    public const int MaximoItens = 200;

    public long Id { get; init; }
    public IReadOnlyList<ItemInformado> Itens { get; init; } = [];
    public uint Version { get; init; }

    public sealed record ItemInformado(long? Id, long MidiaId, DiasSemana DiasSemana);
}

public class DefinirItensPlaylistCommandHandler(IApplicationDbContext context, IParceiroAtual parceiroAtual)
    : IRequestHandler<DefinirItensPlaylistCommand, uint>
{
    public async Task<uint> Handle(DefinirItensPlaylistCommand request, CancellationToken cancellationToken)
    {
        var parceiro = await parceiroAtual.ObterAsync(cancellationToken);
        var playlist = await RegistrosDoParceiro.CarregarAsync(
            context.Playlists.Include(p => p.Itens), parceiro.ParceiroId, request.Id, cancellationToken);

        // Só mídias do próprio parceiro: o banco não cruza o parceiro na FK.
        var midias = request.Itens.Select(i => i.MidiaId).Distinct().ToList();
        var encontradas = await context.Midias
            .CountAsync(m => midias.Contains(m.Id) && m.ParceiroId == parceiro.ParceiroId, cancellationToken);

        if (encontradas != midias.Count)
        {
            throw new ValidationException(nameof(request.Itens), ["Uma das mídias não foi encontrada. Recarregue a lista de mídias."]);
        }

        context.ConferirVersao(playlist, request.Version);

        try
        {
            playlist.DefinirItens(request.Itens.Select(i => new ItemPlaylistInformado(i.Id, i.MidiaId, i.DiasSemana)).ToList());
        }
        catch (InvalidOperationException ex)
        {
            throw new ValidationException(nameof(request.Itens), [ex.Message]);
        }

        // Mudar só os itens não altera a linha da playlist; marcá-la como
        // alterada faz a gravação conferir a versão e gerar uma nova.
        context.Entry(playlist).Property(p => p.LastModified).IsModified = true;

        return await context.SalvarAsync(playlist, cancellationToken);
    }
}

public class DefinirItensPlaylistCommandValidator : AbstractValidator<DefinirItensPlaylistCommand>
{
    public DefinirItensPlaylistCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0).WithMessage("O ID da playlist é obrigatório.");

        RuleFor(x => x.Itens)
            .Must(i => i.Count <= DefinirItensPlaylistCommand.MaximoItens)
            .WithMessage($"A playlist pode ter no máximo {DefinirItensPlaylistCommand.MaximoItens} itens.");

        RuleForEach(x => x.Itens).ChildRules(item =>
        {
            item.RuleFor(i => i.MidiaId).GreaterThan(0).WithMessage("Selecione a mídia.");
            item.RuleFor(i => i.DiasSemana)
                .Must(d => (d & DiasSemana.Todos) != DiasSemana.Nenhum && (d & ~DiasSemana.Todos) == 0)
                .WithMessage("Marque pelo menos um dia da semana.");
        });

        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}
