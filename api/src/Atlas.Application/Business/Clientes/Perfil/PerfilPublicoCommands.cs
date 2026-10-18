using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Midias.Common;
using Atlas.Application.Business.Clientes.Common;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Clientes.Enums;

namespace Atlas.Application.Business.Clientes.Perfil;

/// <summary>
/// O perfil público do cliente do usuário logado: o que aparece no diretório
/// das TVs (ADR-018) e o aceite para aparecer nele.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.View)]
public sealed record GetPerfilPublicoQuery : IRequest<PerfilPublicoDto>;

/// <summary>
/// Salva o perfil público. Aparecer no diretório é decisão do dono do
/// cliente (consentimento): a equipe e a operadora não mudam o aceite.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Midias.Update)]
public sealed record AtualizarPerfilPublicoCommand : IRequest<uint>
{
    public bool ExibirNoDiretorioPublico { get; init; }
    public string? Categoria { get; init; }
    public string? DescricaoPublica { get; init; }

    /// <summary>Telefone com DDD (o 55 entra sozinho).</summary>
    public string? WhatsappPublico { get; init; }

    /// <summary><c>@usuario</c> ou o link do perfil.</summary>
    public string? InstagramPublico { get; init; }

    public string? SitePublico { get; init; }
    public uint Version { get; init; }
}

public sealed record PerfilPublicoDto
{
    public string Nome { get; init; } = null!;
    public bool ExibirNoDiretorioPublico { get; init; }
    public DateTimeOffset? DiretorioAceitoEm { get; init; }
    public string? Categoria { get; init; }
    public string? DescricaoPublica { get; init; }
    public string? WhatsappPublico { get; init; }
    public string? InstagramPublico { get; init; }
    public string? SitePublico { get; init; }

    /// <summary>Só o dono muda o perfil.</summary>
    public bool PodeEditar { get; init; }

    public uint Version { get; init; }
}

public class GetPerfilPublicoQueryHandler(IApplicationDbContext context, IClienteAtual clienteAtual)
    : IRequestHandler<GetPerfilPublicoQuery, PerfilPublicoDto>
{
    public async Task<PerfilPublicoDto> Handle(GetPerfilPublicoQuery request, CancellationToken cancellationToken)
    {
        var atual = await clienteAtual.ObterAsync(cancellationToken);

        return await context.Clientes
            .AsNoTracking()
            .Where(p => p.Id == atual.ClienteId)
            .Select(p => new PerfilPublicoDto
            {
                Nome = p.Nome,
                ExibirNoDiretorioPublico = p.ExibirNoDiretorioPublico,
                DiretorioAceitoEm = p.DiretorioAceitoEm,
                Categoria = p.Categoria,
                DescricaoPublica = p.DescricaoPublica,
                WhatsappPublico = p.WhatsappPublico,
                InstagramPublico = p.InstagramPublico,
                SitePublico = p.SitePublico,
                PodeEditar = atual.Papel == PapelCliente.Proprietario,
                Version = EF.Property<uint>(p, "Version"),
            })
            .SingleAsync(cancellationToken);
    }
}

public class AtualizarPerfilPublicoCommandHandler(
    IApplicationDbContext context,
    IClienteAtual clienteAtual,
    TimeProvider timeProvider)
    : IRequestHandler<AtualizarPerfilPublicoCommand, uint>
{
    public async Task<uint> Handle(AtualizarPerfilPublicoCommand request, CancellationToken cancellationToken)
    {
        var atual = await clienteAtual.ObterAsync(cancellationToken);

        if (atual.Papel != PapelCliente.Proprietario)
        {
            throw new ForbiddenAccessException();
        }

        var erros = new Dictionary<string, string[]>();
        string? whatsapp = null, instagram = null, site = null;

        if (RegistrosDoCliente.Limpar(request.WhatsappPublico) is { } telefone
            && (whatsapp = MontadorQrCode.NormalizarTelefone(telefone)) is null)
        {
            erros[nameof(request.WhatsappPublico)] = ["WhatsApp inválido. Use DDD e número, ex.: (92) 99999-0000."];
        }

        if (RegistrosDoCliente.Limpar(request.InstagramPublico) is { } usuario
            && (instagram = MontadorQrCode.NormalizarInstagram(usuario)) is null)
        {
            erros[nameof(request.InstagramPublico)] = ["Usuário do Instagram inválido. Ex.: @padariacentro"];
        }

        if (RegistrosDoCliente.Limpar(request.SitePublico) is { } endereco
            && (site = MontadorQrCode.NormalizarLink(endereco, 300)) is null)
        {
            erros[nameof(request.SitePublico)] = ["Informe o site completo, começando com https://"];
        }

        if (erros.Count > 0)
        {
            throw new ValidationException(erros);
        }

        var cliente = await context.Clientes.SingleAsync(p => p.Id == atual.ClienteId, cancellationToken);

        context.ConferirVersao(cliente, request.Version);
        cliente.Categoria = RegistrosDoCliente.Limpar(request.Categoria);
        cliente.DescricaoPublica = RegistrosDoCliente.Limpar(request.DescricaoPublica);
        cliente.WhatsappPublico = whatsapp;
        cliente.InstagramPublico = instagram;
        cliente.SitePublico = site;
        cliente.DefinirExibicaoNoDiretorio(request.ExibirNoDiretorioPublico, timeProvider.GetUtcNow());

        return await context.SalvarAsync(cliente, cancellationToken);
    }
}

public class AtualizarPerfilPublicoCommandValidator : AbstractValidator<AtualizarPerfilPublicoCommand>
{
    public AtualizarPerfilPublicoCommandValidator()
    {
        RuleFor(x => x.Categoria)
            .MaximumLength(Cliente.TamanhoMaximoCategoria)
            .WithMessage($"A categoria deve ter no máximo {Cliente.TamanhoMaximoCategoria} caracteres.");

        RuleFor(x => x.DescricaoPublica)
            .MaximumLength(Cliente.TamanhoMaximoDescricaoPublica)
            .WithMessage($"A descrição deve ter no máximo {Cliente.TamanhoMaximoDescricaoPublica} caracteres.");

        RuleFor(x => x.WhatsappPublico).MaximumLength(30).WithMessage("WhatsApp inválido.");
        RuleFor(x => x.InstagramPublico).MaximumLength(100).WithMessage("Usuário do Instagram inválido.");
        RuleFor(x => x.SitePublico).MaximumLength(300).WithMessage("O site deve ter no máximo 300 caracteres.");
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}
