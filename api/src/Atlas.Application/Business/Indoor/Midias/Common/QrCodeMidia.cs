using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Atlas.Application.Business.Common;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Application.Business.Indoor.Midias.Common;

/// <summary>
/// O QR code como vem do formulário: cada tipo usa só os seus campos
/// (link; WhatsApp: telefone e mensagem; Instagram: usuário; telefone; Pix:
/// chave, recebedor, cidade e valor). Os erros voltam com o nome do campo
/// (<c>QrDestino</c>, <c>QrNomeRecebedor</c>…), para o painel mostrar no lugar.
/// </summary>
public interface IQrCodeInformado
{
    TipoQrCode? QrTipo { get; }
    string? QrDestino { get; }
    string? QrMensagem { get; }
    decimal? QrValor { get; }
    string? QrNomeRecebedor { get; }
    string? QrCidadeRecebedor { get; }
    string? QrLegenda { get; }
    PosicaoQrCode? QrPosicao { get; }
    TamanhoQrCode? QrTamanho { get; }
}

public static partial class MontadorQrCode
{
    /// <summary>Legenda quando o anunciante não escreve a dele.</summary>
    public static string LegendaPadrao(TipoQrCode tipo) => tipo switch
    {
        TipoQrCode.WhatsApp => "Fale no WhatsApp",
        TipoQrCode.Instagram => "Siga no Instagram",
        TipoQrCode.Telefone => "Ligue para nós",
        TipoQrCode.Pix => "Pague com Pix",
        _ => "Aponte a câmera",
    };

    /// <summary>Valida o que foi informado e monta o conteúdo; <c>null</c> quando o tipo vem vazio (sem QR code).</summary>
    /// <exception cref="ValidationException">Campo obrigatório ausente ou inválido para o tipo.</exception>
    public static QrCodeDaMidia? Montar(IQrCodeInformado qr)
    {
        if (qr.QrTipo is not { } tipo)
        {
            return null;
        }

        var erros = new Dictionary<string, string[]>();
        var destino = qr.QrDestino?.Trim() ?? string.Empty;
        var mensagem = Limpar(qr.QrMensagem);
        string? nome = null, cidade = null;
        decimal? valor = null;
        string? conteudo = null;

        if (destino.Length == 0)
        {
            erros["QrDestino"] = [tipo switch
            {
                TipoQrCode.Link => "Informe o link.",
                TipoQrCode.Instagram => "Informe o usuário do Instagram.",
                TipoQrCode.Pix => "Informe a chave Pix.",
                _ => "Informe o telefone com DDD.",
            }];
        }
        else
        {
            switch (tipo)
            {
                case TipoQrCode.Link:
                    if (Uri.TryCreate(destino, UriKind.Absolute, out var url) && url.Scheme is "http" or "https" && destino.Length <= 500)
                    {
                        destino = url.AbsoluteUri;
                        conteudo = destino;
                    }
                    else
                    {
                        erros["QrDestino"] = ["Informe o link completo, começando com https://"];
                    }

                    break;

                case TipoQrCode.WhatsApp:
                    if (Telefone(destino) is { } whatsapp)
                    {
                        destino = whatsapp;
                        conteudo = $"https://wa.me/{whatsapp}" + (mensagem is null ? string.Empty : $"?text={Uri.EscapeDataString(mensagem)}");
                    }
                    else
                    {
                        erros["QrDestino"] = ["Telefone inválido. Use DDD e número, ex.: (92) 99999-0000."];
                    }

                    if (mensagem is { Length: > 200 })
                    {
                        erros["QrMensagem"] = ["A mensagem deve ter no máximo 200 caracteres."];
                    }

                    break;

                case TipoQrCode.Instagram:
                    var usuario = UsuarioInstagram().Match(destino) is { Success: true } m ? m.Groups["usuario"].Value : null;
                    if (usuario is not null)
                    {
                        destino = usuario;
                        conteudo = $"https://instagram.com/{usuario}";
                    }
                    else
                    {
                        erros["QrDestino"] = ["Usuário do Instagram inválido. Ex.: @padariacentro"];
                    }

                    break;

                case TipoQrCode.Telefone:
                    if (Telefone(destino) is { } telefone)
                    {
                        destino = telefone;
                        conteudo = $"tel:+{telefone}";
                    }
                    else
                    {
                        erros["QrDestino"] = ["Telefone inválido. Use DDD e número, ex.: (92) 3333-0000."];
                    }

                    break;

                case TipoQrCode.Pix:
                    var chave = PixCopiaECola.NormalizarChave(destino);
                    nome = PixCopiaECola.TextoPermitido(qr.QrNomeRecebedor, 25);
                    cidade = PixCopiaECola.TextoPermitido(qr.QrCidadeRecebedor, 15);
                    valor = qr.QrValor;

                    if (chave is null)
                    {
                        erros["QrDestino"] = ["Chave Pix inválida. Use CPF, CNPJ, e-mail, telefone (+55 DDD número) ou a chave aleatória."];
                    }

                    if (nome is null)
                    {
                        erros["QrNomeRecebedor"] = ["Informe o nome de quem recebe (até 25 letras)."];
                    }

                    if (cidade is null)
                    {
                        erros["QrCidadeRecebedor"] = ["Informe a cidade de quem recebe (até 15 letras)."];
                    }

                    if (valor is <= 0 or >= 1_000_000)
                    {
                        erros["QrValor"] = ["O valor deve ser maior que zero (ou fique em branco para quem paga digitar)."];
                    }

                    if (chave is not null && nome is not null && cidade is not null && !erros.ContainsKey("QrValor"))
                    {
                        destino = chave;
                        conteudo = PixCopiaECola.Montar(chave, nome, cidade, valor);
                    }

                    break;
            }
        }

        var legenda = Limpar(qr.QrLegenda);
        if (legenda is { Length: > Midia.TamanhoMaximoLegendaQrCode })
        {
            erros["QrLegenda"] = [$"A legenda deve ter no máximo {Midia.TamanhoMaximoLegendaQrCode} caracteres."];
        }

        if (conteudo is { Length: > Midia.TamanhoMaximoConteudoQrCode })
        {
            erros["QrDestino"] = ["O conteúdo do QR code ficou grande demais. Encurte o link ou a mensagem."];
        }

        if (erros.Count > 0 || conteudo is null)
        {
            throw new ValidationException(erros.Count > 0 ? erros : new Dictionary<string, string[]> { ["QrDestino"] = ["QR code inválido."] });
        }

        return new QrCodeDaMidia(
            tipo,
            destino,
            tipo == TipoQrCode.WhatsApp ? mensagem : null,
            tipo == TipoQrCode.Pix ? valor : null,
            nome,
            cidade,
            legenda,
            qr.QrPosicao ?? PosicaoQrCode.InferiorDireito,
            qr.QrTamanho ?? TamanhoQrCode.Medio,
            conteudo);
    }

    /// <summary>Só dígitos com o código do país: DDD + número ganha o 55 na frente.</summary>
    private static string? Telefone(string valor)
    {
        var digitos = DocumentosBrasil.SomenteDigitos(valor);
        if (digitos.Length is 10 or 11 && !valor.TrimStart().StartsWith('+'))
        {
            digitos = "55" + digitos;
        }

        return digitos.Length is >= 12 and <= 15 ? digitos : null;
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    [GeneratedRegex(@"^(?:https?://(?:www\.)?instagram\.com/|@)?(?<usuario>[A-Za-z0-9._]{1,30})/?$", RegexOptions.CultureInvariant)]
    private static partial Regex UsuarioInstagram();
}

/// <summary>
/// Pix copia e cola estático (BR Code, padrão EMV do Banco Central): chave,
/// recebedor, cidade e valor opcional, fechado com o CRC16-CCITT.
/// </summary>
public static partial class PixCopiaECola
{
    public static string Montar(string chave, string nome, string cidade, decimal? valor, string identificador = "***")
    {
        var conta = Campo("00", "br.gov.bcb.pix") + Campo("01", chave);
        var texto = new StringBuilder()
            .Append(Campo("00", "01"))
            .Append(Campo("26", conta))
            .Append(Campo("52", "0000"))
            .Append(Campo("53", "986"));

        if (valor is { } v)
        {
            texto.Append(Campo("54", v.ToString("0.00", CultureInfo.InvariantCulture)));
        }

        texto.Append(Campo("58", "BR"))
            .Append(Campo("59", nome))
            .Append(Campo("60", cidade))
            .Append(Campo("62", Campo("05", identificador)))
            .Append("6304");

        return texto.Append(Crc16(texto.ToString()).ToString("X4", CultureInfo.InvariantCulture)).ToString();
    }

    /// <summary>
    /// A chave como o Pix espera: CPF/CNPJ só dígitos, e-mail em minúsculas,
    /// telefone com +55 e a chave aleatória (UUID) em minúsculas. Nulo se não é chave válida.
    /// </summary>
    public static string? NormalizarChave(string valor)
    {
        var texto = valor.Trim();

        if (texto.Contains('@'))
        {
            return texto.Length <= 77 && Email().IsMatch(texto) ? texto.ToLowerInvariant() : null;
        }

        if (Guid.TryParseExact(texto, "D", out var aleatoria))
        {
            return aleatoria.ToString("D");
        }

        var digitos = DocumentosBrasil.SomenteDigitos(texto);
        if (texto.StartsWith('+'))
        {
            return digitos.StartsWith("55", StringComparison.Ordinal) && digitos.Length is 12 or 13 ? "+" + digitos : null;
        }

        // Escrito como documento (000.000.000-00 ou 00.000.000/0000-00): só vale se o documento for válido.
        if (FormatoDocumento().IsMatch(texto))
        {
            return DocumentosBrasil.CpfValido(digitos) || DocumentosBrasil.CnpjValido(digitos) ? digitos : null;
        }

        if (digitos.Length == 11 && DocumentosBrasil.CpfValido(digitos))
        {
            return digitos;
        }

        if (digitos.Length == 14 && DocumentosBrasil.CnpjValido(digitos))
        {
            return digitos;
        }

        // DDD + número sem o +55.
        return digitos.Length is 10 or 11 && !Regex.IsMatch(texto, "[A-Za-z]") ? "+55" + digitos : null;
    }

    /// <summary>Sem acentos e só com letras, números e pontuação simples; nulo se vazio.</summary>
    public static string? TextoPermitido(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var semAcento = new string(valor.Trim().Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        var limpo = Regex.Replace(Regex.Replace(semAcento, @"[^A-Za-z0-9 .,\-/&]", string.Empty), @"\s+", " ").Trim();

        return limpo.Length == 0 ? null : limpo.Length > maximo ? limpo[..maximo].TrimEnd() : limpo;
    }

    private static string Campo(string id, string valor) => id + valor.Length.ToString("00", CultureInfo.InvariantCulture) + valor;

    /// <summary>CRC16-CCITT-FALSE (polinômio 0x1021, início 0xFFFF), como pede o BR Code.</summary>
    public static ushort Crc16(string texto)
    {
        ushort crc = 0xFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(texto))
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
        }

        return crc;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex Email();

    [GeneratedRegex(@"^(\d{3}\.\d{3}\.\d{3}-\d{2}|\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2})$")]
    private static partial Regex FormatoDocumento();
}
