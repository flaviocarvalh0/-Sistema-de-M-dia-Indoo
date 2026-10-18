using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Atlas.Application.Business.Indoor.Armazenamento;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;
using Microsoft.Extensions.Logging;

namespace Atlas.Application.Business.Rede.AppTv;

/// <summary>
/// Atualização automática do app das TVs (ADR-021): a operadora envia o APK,
/// escolhe para quem vai (TVs de teste ou todas) e as TVs com versão mais
/// antiga baixam e instalam sozinhas.
/// </summary>
public static partial class ArquivoApk
{
    public const string TipoConteudo = TiposArquivoMidia.Apk;

    public static string NovaChave() => $"app/{Guid.NewGuid():N}.apk";

    public static bool ChaveValida(string? chave) => chave is not null && Formato().IsMatch(chave);

    [GeneratedRegex("^app/[0-9a-f]{32}\\.apk$")]
    private static partial Regex Formato();
}

/// <summary>Onde o navegador envia o APK (URL assinada, direto ao storage).</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public sealed record PrepararEnvioApkCommand(long TamanhoBytes) : IRequest<EnvioApkDto>;

public sealed record EnvioApkDto(string Chave, Uri UrlEnvio, DateTimeOffset ExpiraEm, string TipoConteudo);

/// <summary>Registra a versão depois do envio: a API confere o arquivo (ZIP/APK, tamanho e SHA-256).</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public sealed record CriarVersaoAppCommand : IRequest<long>
{
    /// <summary>O <c>versionCode</c> (o número depois do <c>+</c> no pubspec.yaml).</summary>
    public int Codigo { get; init; }

    /// <summary>O <c>versionName</c> (ex.: 1.3.0).</summary>
    public string Nome { get; init; } = string.Empty;

    public string? Notas { get; init; }
    public string Chave { get; init; } = string.Empty;

    /// <summary>SHA-256 calculado no navegador (opcional, conferido).</summary>
    public string? Hash { get; init; }
}

/// <summary>Suspensa (nenhuma TV), Teste (só as TVs de teste) ou Todas.</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public sealed record DefinirDistribuicaoVersaoCommand(long Id, DistribuicaoVersao Distribuicao, uint Version) : IRequest<uint>;

/// <summary>Só versão suspensa pode sair (o APK sai do storage).</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public sealed record ExcluirVersaoAppCommand(long Id) : IRequest;

[Authorize(Policy = BusinessPermissionKeys.Telas.View)]
[Authorize(Policy = BusinessPermissionKeys.Parceiros.View)]
public sealed record GetVersoesAppQuery : IRequest<VersoesAppDto>;

public sealed record VersoesAppDto
{
    public IReadOnlyList<VersaoAppDto> Versoes { get; init; } = [];

    /// <summary>TVs pareadas (ativas).</summary>
    public int TelasPareadas { get; init; }

    /// <summary>TVs com app antigo, que não informa a versão (precisam de uma instalação manual).</summary>
    public int TelasSemAtualizacaoAutomatica { get; init; }
}

public sealed record VersaoAppDto
{
    public long Id { get; init; }
    public int Codigo { get; init; }
    public string Nome { get; init; } = null!;
    public string? Notas { get; init; }
    public long TamanhoBytes { get; init; }
    public DistribuicaoVersao Distribuicao { get; init; }

    /// <summary>TVs pareadas que já rodam esta versão.</summary>
    public int TelasNestaVersao { get; init; }

    public DateTimeOffset Created { get; init; }
    public uint Version { get; init; }
}

public class PrepararEnvioApkCommandHandler(IArmazenamentoMidias armazenamento)
    : IRequestHandler<PrepararEnvioApkCommand, EnvioApkDto>
{
    public async Task<EnvioApkDto> Handle(PrepararEnvioApkCommand request, CancellationToken cancellationToken)
    {
        var chave = ArquivoApk.NovaChave();
        var envio = await armazenamento.PrepararEnvioAsync(chave, cancellationToken);
        return new EnvioApkDto(chave, envio.Url, envio.ExpiraEm, ArquivoApk.TipoConteudo);
    }
}

public class PrepararEnvioApkCommandValidator : AbstractValidator<PrepararEnvioApkCommand>
{
    public PrepararEnvioApkCommandValidator() =>
        RuleFor(x => x.TamanhoBytes)
            .InclusiveBetween(1, VersaoApp.TamanhoMaximoBytes)
            .WithMessage($"O APK deve ter até {VersaoApp.TamanhoMaximoBytes / (1024 * 1024)} MB. Gere com --split-per-abi.");
}

public class CriarVersaoAppCommandHandler(
    IApplicationDbContext context,
    IArmazenamentoMidias armazenamento,
    ILogger<CriarVersaoAppCommandHandler> logger)
    : IRequestHandler<CriarVersaoAppCommand, long>
{
    public async Task<long> Handle(CriarVersaoAppCommand request, CancellationToken cancellationToken)
    {
        if (await context.VersoesApp.AnyAsync(v => v.Codigo == request.Codigo, cancellationToken))
        {
            throw new ValidationException(nameof(request.Codigo), [$"Já existe uma versão com o código {request.Codigo}."]);
        }

        var (tamanho, hash) = await ConferirAsync(request, cancellationToken);

        var versao = new VersaoApp
        {
            Codigo = request.Codigo,
            Nome = request.Nome.Trim(),
            Notas = RegistrosDoCliente.Limpar(request.Notas),
            ChaveArmazenamento = request.Chave,
            TamanhoBytes = tamanho,
            Hash = hash,
        };
        context.VersoesApp.Add(versao);
        await context.SaveChangesAsync(cancellationToken);
        return versao.Id;
    }

    /// <summary>O objeto existe, cabe no limite, é um ZIP (todo APK é) e bate com o hash do navegador.</summary>
    private async Task<(long Tamanho, string Hash)> ConferirAsync(CriarVersaoAppCommand request, CancellationToken cancellationToken)
    {
        var objeto = await armazenamento.ObterAsync(request.Chave, cancellationToken)
            ?? throw new ValidationException(nameof(request.Chave), ["O APK não chegou ao storage. Envie de novo."]);

        string? erro = objeto.TamanhoBytes > VersaoApp.TamanhoMaximoBytes ? "O APK passa do limite de 50 MB." : null;
        string hash = string.Empty;

        if (erro is null)
        {
            await using var leitura = await armazenamento.AbrirLeituraAsync(request.Chave, cancellationToken)
                ?? throw new ValidationException(nameof(request.Chave), ["O APK não chegou ao storage. Envie de novo."]);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            var inicio = new List<byte>(4);
            int lidos;
            while ((lidos = await leitura.ReadAsync(buffer, cancellationToken)) > 0)
            {
                sha.AppendData(buffer, 0, lidos);
                for (var i = 0; i < lidos && inicio.Count < 4; i++)
                {
                    inicio.Add(buffer[i]);
                }
            }

            hash = Convert.ToHexStringLower(sha.GetHashAndReset());
            if (!inicio.SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 }))
            {
                erro = "O arquivo não é um APK.";
            }
            else if (request.Hash is { } informado && !string.Equals(informado, hash, StringComparison.OrdinalIgnoreCase))
            {
                erro = "O APK chegou diferente do enviado (hash não confere). Envie de novo.";
            }
        }

        if (erro is not null)
        {
            try
            {
                await armazenamento.RemoverAsync(request.Chave, CancellationToken.None);
            }
            catch (ArmazenamentoIndisponivelException ex)
            {
                logger.LogWarning(ex, "APK recusado {Chave} ficou no storage.", request.Chave);
            }

            throw new ValidationException(nameof(request.Chave), [erro]);
        }

        return (objeto.TamanhoBytes, hash);
    }
}

public class CriarVersaoAppCommandValidator : AbstractValidator<CriarVersaoAppCommand>
{
    public CriarVersaoAppCommandValidator()
    {
        RuleFor(x => x.Codigo).GreaterThan(0).WithMessage("Informe o código da versão (o número depois do + no pubspec.yaml).");
        RuleFor(x => x.Nome)
            .NotEmpty().WithMessage("Informe o nome da versão (ex.: 1.3.0).")
            .MaximumLength(30).WithMessage("O nome deve ter no máximo 30 caracteres.");
        RuleFor(x => x.Notas).MaximumLength(500).WithMessage("As notas devem ter no máximo 500 caracteres.");
        RuleFor(x => x.Chave).Must(ArquivoApk.ChaveValida).WithMessage("Envie o APK antes de salvar.");
        RuleFor(x => x.Hash)
            .Matches("^[0-9a-fA-F]{64}$").WithMessage("O hash deve ser um SHA-256 em hexadecimal.")
            .When(x => x.Hash is not null);
    }
}

public class DefinirDistribuicaoVersaoCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DefinirDistribuicaoVersaoCommand, uint>
{
    public async Task<uint> Handle(DefinirDistribuicaoVersaoCommand request, CancellationToken cancellationToken)
    {
        var versao = await context.VersoesApp.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
        Guard.Against.NotFound(request.Id, versao);

        context.ConferirVersao(versao, request.Version);
        versao.Distribuicao = request.Distribuicao;
        return await context.SalvarAsync(versao, cancellationToken);
    }
}

public class DefinirDistribuicaoVersaoCommandValidator : AbstractValidator<DefinirDistribuicaoVersaoCommand>
{
    public DefinirDistribuicaoVersaoCommandValidator()
    {
        RuleFor(x => x.Distribuicao).IsInEnum().WithMessage("Distribuição inválida.");
        RuleFor(x => x.Version).GreaterThan(0u).WithMessage("A versão do registro é obrigatória.");
    }
}

public class ExcluirVersaoAppCommandHandler(
    IApplicationDbContext context,
    IArmazenamentoMidias armazenamento,
    ILogger<ExcluirVersaoAppCommandHandler> logger)
    : IRequestHandler<ExcluirVersaoAppCommand>
{
    public async Task Handle(ExcluirVersaoAppCommand request, CancellationToken cancellationToken)
    {
        var versao = await context.VersoesApp.FirstOrDefaultAsync(v => v.Id == request.Id, cancellationToken);
        Guard.Against.NotFound(request.Id, versao);

        if (versao.Distribuicao != DistribuicaoVersao.Suspensa)
        {
            throw new ValidationException(nameof(versao.Distribuicao), ["Suspenda a versão antes de excluir."]);
        }

        context.VersoesApp.Remove(versao);
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            await armazenamento.RemoverAsync(versao.ChaveArmazenamento, cancellationToken);
        }
        catch (ArmazenamentoIndisponivelException ex)
        {
            logger.LogWarning(ex, "Versão {Codigo} excluída, mas o APK {Chave} ficou no storage.", versao.Codigo, versao.ChaveArmazenamento);
        }
    }
}

public class GetVersoesAppQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetVersoesAppQuery, VersoesAppDto>
{
    public async Task<VersoesAppDto> Handle(GetVersoesAppQuery request, CancellationToken cancellationToken)
    {
        var pareadas = context.Telas.Where(t => t.Ativo && t.DispositivoId != null && t.Estado != null);

        var versoes = await context.VersoesApp
            .AsNoTracking()
            .OrderByDescending(v => v.Codigo)
            .Select(v => new VersaoAppDto
            {
                Id = v.Id,
                Codigo = v.Codigo,
                Nome = v.Nome,
                Notas = v.Notas,
                TamanhoBytes = v.TamanhoBytes,
                Distribuicao = v.Distribuicao,
                TelasNestaVersao = pareadas.Count(t => t.Estado!.VersaoCodigoApp == v.Codigo),
                Created = v.Created,
                Version = EF.Property<uint>(v, "Version"),
            })
            .ToListAsync(cancellationToken);

        return new VersoesAppDto
        {
            Versoes = versoes,
            TelasPareadas = await pareadas.CountAsync(cancellationToken),
            TelasSemAtualizacaoAutomatica = await pareadas.CountAsync(t => t.Estado!.VersaoCodigoApp == null, cancellationToken),
        };
    }
}
