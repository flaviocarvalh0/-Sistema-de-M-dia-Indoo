using System.Security.Cryptography;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Telas.Commands.Pareamento;
using Atlas.Domain.Business.Indoor.Entities;

namespace Atlas.Application.Business.Diretorio;

/// <summary>Endereço público do diretório de uma tela: <c>{Indoor:UrlPublica}/d/{codigo}</c>.</summary>
public static class EnderecoDiretorio
{
    /// <summary>Nulo sem código ou sem <c>Indoor:UrlPublica</c> configurada.</summary>
    public static Uri? Montar(IConfiguracaoIndoor configuracao, string? codigo)
    {
        if (codigo is null || configuracao.UrlPublica is not { } baseUrl)
        {
            return null;
        }

        var texto = baseUrl.AbsoluteUri;
        return new Uri(new Uri(texto.EndsWith('/') ? texto : texto + "/"), $"d/{codigo}");
    }

    /// <summary>
    /// 10 caracteres do mesmo alfabeto do pareamento (sem 0/O, 1/I): 32¹⁰ ≈
    /// 10¹⁵ combinações, inviável de varrer com o limite de acessos por IP.
    /// </summary>
    public static async Task<string> GerarCodigoUnicoAsync(IApplicationDbContext context, CancellationToken cancellationToken)
    {
        while (true)
        {
            var codigo = string.Create(Tela.TamanhoCodigoDiretorio, 0, (destino, _) =>
            {
                for (var i = 0; i < destino.Length; i++)
                {
                    destino[i] = GeradorCodigoPareamento.Alfabeto[RandomNumberGenerator.GetInt32(GeradorCodigoPareamento.Alfabeto.Length)];
                }
            });

            if (!await context.Telas.AnyAsync(t => t.CodigoDiretorio == codigo, cancellationToken))
            {
                return codigo;
            }
        }
    }
}
