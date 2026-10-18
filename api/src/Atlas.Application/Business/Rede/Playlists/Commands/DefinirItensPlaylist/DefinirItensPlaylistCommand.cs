using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Rede.Playlists.Commands.SalvarPlaylist;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Rede.Playlists.Commands.DefinirItensPlaylist;

/// <summary>
/// Grava a grade inteira de uma vez, na ordem da lista (a posição vira a
/// ordem do loop) — é o que a tela de arrastar e soltar envia. Item com id
/// atualiza, sem id entra, ausente sai. A operadora monta a grade com mídias
/// de qualquer cliente; a mesma mídia pode entrar mais de uma vez (cota
/// dupla).
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Playlists.Update)]
public record DefinirItensPlaylistCommand : IRequest<uint>
{
    public const int MaximoItens = 200;

    public long Id { get; init; }
    public IReadOnlyList<ItemInformado> Itens { get; init; } = [];
    public uint Version { get; init; }

    /// <summary>Item da grade; <c>PosicaoFixa</c> fica no lugar quando a playlist embaralha os anúncios.</summary>
    public sealed record ItemInformado(long? Id, long MidiaId, DiasSemana DiasSemana, bool PosicaoFixa = false);
}

public class DefinirItensPlaylistCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DefinirItensPlaylistCommand, uint>
{
    public async Task<uint> Handle(DefinirItensPlaylistCommand request, CancellationToken cancellationToken)
    {
        var playlist = await RegrasPlaylist.CarregarAsync(context.Playlists.Include(p => p.Itens), request.Id, cancellationToken);

        var midias = request.Itens.Select(i => i.MidiaId).Distinct().ToList();
        var ativas = await context.Midias
            .Where(m => midias.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.Ativo, cancellationToken);

        if (ativas.Count != midias.Count)
        {
            throw new ValidationException(nameof(request.Itens), ["Uma das mídias não foi encontrada. Recarregue a lista de mídias."]);
        }

        // Só entra na grade mídia que o cliente ativou (é o que o plano dele
        // paga). Itens que já estavam ficam, mesmo se a mídia foi inativada
        // depois: ela só não toca até ser ativada de novo.
        var jaNaGrade = playlist.Itens.Select(i => (i.Id, i.MidiaId)).ToHashSet();
        if (request.Itens.Any(i => !ativas[i.MidiaId] && !(i.Id is { } id && jaNaGrade.Contains((id, i.MidiaId)))))
        {
            throw new ValidationException(nameof(request.Itens), ["Só mídias ativas podem entrar na grade."]);
        }

        context.ConferirVersao(playlist, request.Version);

        try
        {
            playlist.DefinirItens(request.Itens.Select(i => new ItemPlaylistInformado(i.Id, i.MidiaId, i.DiasSemana, i.PosicaoFixa)).ToList());
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
