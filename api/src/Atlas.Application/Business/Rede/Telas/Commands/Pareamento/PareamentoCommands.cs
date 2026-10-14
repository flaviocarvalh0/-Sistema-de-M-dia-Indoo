using System.Security.Cryptography;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Diretorio;
using Atlas.Application.Business.Indoor.Common;
using Atlas.Application.Business.Indoor.Telas.Dtos;
using Atlas.Application.Business.Rede.Telas.Commands.SalvarTela;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Rede.Telas.Commands.Pareamento;

/// <summary>
/// Gera o código que a pessoa digita na TV para pareá-la com esta tela
/// (válido por 24 h, uso único). Gerar de novo invalida o anterior; a TV já
/// pareada continua funcionando até parear outra.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
public record GerarCodigoPareamentoCommand(long Id, uint Version) : IRequest<CodigoPareamentoDto>;

/// <summary>Tira a TV da tela: o token dela para de valer na próxima sincronização.</summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
public record DesvincularTelaCommand(long Id, uint Version) : IRequest<uint>;

/// <summary>
/// Troca o endereço público do diretório (ex.: o QR foi fotografado e
/// divulgado fora do estabelecimento). O anterior para de funcionar na hora; a
/// TV mostra o QR novo na próxima sincronização.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Telas.Update)]
public record TrocarCodigoDiretorioCommand(long Id, uint Version) : IRequest<CodigoDiretorioDto>;

public class TrocarCodigoDiretorioCommandHandler(IApplicationDbContext context, IConfiguracaoIndoor configuracao)
    : IRequestHandler<TrocarCodigoDiretorioCommand, CodigoDiretorioDto>
{
    public async Task<CodigoDiretorioDto> Handle(TrocarCodigoDiretorioCommand request, CancellationToken cancellationToken)
    {
        var tela = await RegrasTela.CarregarAsync(context, request.Id, cancellationToken);
        var codigo = await EnderecoDiretorio.GerarCodigoUnicoAsync(context, cancellationToken);

        context.ConferirVersao(tela, request.Version);
        tela.DefinirCodigoDiretorio(codigo);

        var version = await context.SalvarAsync(tela, cancellationToken);

        return new CodigoDiretorioDto(codigo, EnderecoDiretorio.Montar(configuracao, codigo), version);
    }
}

public class GerarCodigoPareamentoCommandHandler(IApplicationDbContext context, TimeProvider timeProvider)
    : IRequestHandler<GerarCodigoPareamentoCommand, CodigoPareamentoDto>
{
    public async Task<CodigoPareamentoDto> Handle(GerarCodigoPareamentoCommand request, CancellationToken cancellationToken)
    {
        var tela = await RegrasTela.CarregarAsync(context, request.Id, cancellationToken);

        if (!tela.Ativo)
        {
            throw new ValidationException(nameof(Tela.Ativo), ["Ative a tela antes de parear uma TV."]);
        }

        var codigo = await GeradorCodigoPareamento.GerarUnicoAsync(context, cancellationToken);

        context.ConferirVersao(tela, request.Version);
        tela.GerarCodigoPareamento(codigo, timeProvider.GetUtcNow());

        var version = await context.SalvarAsync(tela, cancellationToken);

        return new CodigoPareamentoDto(codigo, tela.CodigoPareamentoExpiraEm!.Value, version);
    }
}

public class DesvincularTelaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<DesvincularTelaCommand, uint>
{
    public async Task<uint> Handle(DesvincularTelaCommand request, CancellationToken cancellationToken)
    {
        var tela = await RegrasTela.CarregarAsync(context, request.Id, cancellationToken);

        if (!tela.Pareada)
        {
            throw new ValidationException(nameof(Tela.DispositivoId), ["A tela não tem TV pareada."]);
        }

        context.ConferirVersao(tela, request.Version);
        tela.Desvincular();

        return await context.SalvarAsync(tela, cancellationToken);
    }
}

/// <summary>
/// Código de 8 caracteres sem os que se confundem na tela da TV (0/O, 1/I):
/// 32 símbolos, ~10¹² combinações — com validade de 24 h e limite de
/// tentativas na autenticação, adivinhar não é viável.
/// </summary>
public static class GeradorCodigoPareamento
{
    public const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Gerar() =>
        string.Create(Tela.TamanhoCodigoPareamento, 0, (destino, _) =>
        {
            for (var i = 0; i < destino.Length; i++)
            {
                destino[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
            }
        });

    /// <summary>Um código que nenhuma outra tela tem agora (o índice único garante no banco).</summary>
    public static async Task<string> GerarUnicoAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            var codigo = Gerar();

            if (!await context.Telas.AnyAsync(t => t.CodigoPareamento == codigo, cancellationToken))
            {
                return codigo;
            }
        }

        throw new InvalidOperationException("Não foi possível gerar um código de pareamento livre.");
    }
}
