using System.Security.Cryptography;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Telas.Commands.Pareamento;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Veiculacao;

/// <summary>
/// QR code rastreado (ADR-023): a TV mostra <c>{Indoor:UrlPublica}/q/{codigo}</c>;
/// a API conta a leitura e redireciona para o destino da mídia.
/// </summary>
public static class LinkQr
{
    /// <summary>
    /// Só os tipos que abrem um link podem passar pelo redirecionamento.
    /// Telefone (<c>tel:</c>) e Pix (copia e cola lido pelo app do banco) vão direto.
    /// </summary>
    public static bool Rastreavel(TipoQrCode? tipo) =>
        tipo is TipoQrCode.Link or TipoQrCode.WhatsApp or TipoQrCode.Instagram;

    /// <summary>Nulo sem código ou sem <c>Indoor:UrlPublica</c> (aí a TV mostra o destino direto).</summary>
    public static Uri? Montar(IConfiguracaoIndoor configuracao, string? codigo)
    {
        if (codigo is null || configuracao.UrlPublica is not { } baseUrl)
        {
            return null;
        }

        var texto = baseUrl.AbsoluteUri;
        return new Uri(new Uri(texto.EndsWith('/') ? texto : texto + "/"), $"q/{codigo}");
    }

    /// <summary>
    /// Depois de definir ou remover o QR code: tipo rastreável ganha um código
    /// (e mantém o que já tinha); os outros ficam sem.
    /// </summary>
    public static async Task AtualizarCodigoAsync(IApplicationDbContext context, Midia midia, CancellationToken cancellationToken)
    {
        if (!Rastreavel(midia.QrTipo))
        {
            midia.DefinirCodigoQr(null);
            return;
        }

        if (midia.CodigoQr is null)
        {
            midia.DefinirCodigoQr(await GerarCodigoUnicoAsync(context, cancellationToken));
        }
    }

    /// <summary>8 caracteres do alfabeto do pareamento (sem 0/O, 1/I): 32⁸ ≈ 10¹² combinações.</summary>
    public static async Task<string> GerarCodigoUnicoAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        while (true)
        {
            var codigo = string.Create(Midia.TamanhoCodigoQr, 0, (destino, _) =>
            {
                for (var i = 0; i < destino.Length; i++)
                {
                    destino[i] = GeradorCodigoPareamento.Alfabeto[RandomNumberGenerator.GetInt32(GeradorCodigoPareamento.Alfabeto.Length)];
                }
            });

            if (!await context.Midias.AnyAsync(m => m.CodigoQr == codigo, cancellationToken))
            {
                return codigo;
            }
        }
    }
}
