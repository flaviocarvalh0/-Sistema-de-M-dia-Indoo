using System.Net;

namespace Atlas.Application.Business.Common;

/// <summary>
/// Layout dos e-mails da Mídia Indoor: cartão branco sobre fundo claro, a marca em
/// texto (sem imagem — a maioria dos clientes bloqueia imagem remota por
/// padrão), um botão de ação e rodapé. HTML de e-mail: tabelas e estilo
/// inline, que é o que Gmail e Outlook respeitam.
/// </summary>
/// <remarks>
/// Os blocos recebem texto puro e codificam o HTML aqui. Só
/// <see cref="ParagrafoHtml"/> recebe HTML pronto — quem chama codifica os
/// valores (use <see cref="E"/>).
/// </remarks>
public static class ModeloEmail
{
    private const string CorDestaque = "#0F766E";
    private const string Marinho = "#1B2A41";
    private const string Texto = "#3D4451";
    private const string Suave = "#6B7280";
    private const string Fundo = "#F4F1EE";
    private const string Fonte = "'Plus Jakarta Sans','Segoe UI',Roboto,Helvetica,Arial,sans-serif";

    private static readonly TimeZoneInfo Brasilia =
        TimeZoneInfo.TryFindSystemTimeZoneById("America/Sao_Paulo", out var fuso) ? fuso : TimeZoneInfo.Utc;

    /// <summary>"12/10/2026 às 14:05 (horário de Brasília)" — quem lê o e-mail não pensa em UTC.</summary>
    public static string DataHora(DateTimeOffset instante)
    {
        var local = TimeZoneInfo.ConvertTime(instante, Brasilia);
        var fuso = Brasilia == TimeZoneInfo.Utc ? "UTC" : "horário de Brasília";
        return $"{local:dd/MM/yyyy} às {local:HH:mm} ({fuso})";
    }

    /// <summary>Codifica um valor para entrar num <see cref="ParagrafoHtml"/>.</summary>
    public static string E(string? valor) => WebUtility.HtmlEncode(valor ?? string.Empty);

    /// <summary>
    /// O e-mail inteiro. <paramref name="previa"/> é o texto que a caixa de
    /// entrada mostra ao lado do assunto; <paramref name="motivo"/> explica no
    /// rodapé por que a pessoa recebeu.
    /// </summary>
    public static string Montar(string previa, string titulo, string motivo, params string[] blocos) => $"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="color-scheme" content="light">
        <meta name="supported-color-schemes" content="light">
        <title>{E(titulo)}</title>
        </head>
        <body style="margin:0;padding:0;background:{Fundo};">
        <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:{Fundo};">{E(previa)}</div>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background:{Fundo};">
          <tr><td align="center" style="padding:32px 16px;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:560px;">
              <tr><td style="padding:0 8px 20px;font-family:{Fonte};">
                <span style="display:inline-block;width:32px;height:32px;line-height:32px;border-radius:9px;background:{CorDestaque};color:#ffffff;font-weight:800;font-size:17px;text-align:center;vertical-align:middle;">M</span>
                <span style="font-weight:800;font-size:20px;color:{Marinho};vertical-align:middle;margin-left:8px;letter-spacing:-0.3px;">Mídia<span style="color:{CorDestaque};"> Indoor</span></span>
              </td></tr>
              <tr><td style="background:#ffffff;border-radius:16px;padding:36px 32px;font-family:{Fonte};border-top:4px solid {CorDestaque};">
                <h1 style="margin:0 0 16px;font-size:22px;line-height:1.3;color:{Marinho};font-weight:800;">{E(titulo)}</h1>
                {string.Concat(blocos)}
              </td></tr>
              <tr><td style="padding:20px 8px 0;font-family:{Fonte};font-size:12px;line-height:1.6;color:{Suave};text-align:center;">
                {E(motivo)}<br>
                <strong style="color:{Marinho};">Mídia Indoor</strong> · suas TVs sempre no ar
              </td></tr>
            </table>
          </td></tr>
        </table>
        </body>
        </html>
        """;

    public static string Paragrafo(string texto) => ParagrafoHtml(E(texto));

    /// <summary>Parágrafo com HTML pronto (negrito, por exemplo). Codifique os valores com <see cref="E"/>.</summary>
    public static string ParagrafoHtml(string html) =>
        $"""<p style="margin:0 0 14px;font-size:15px;line-height:1.6;color:{Texto};">{html}</p>""";

    /// <summary>
    /// Botão que funciona no Outlook (célula com fundo, não só o link) e, logo
    /// abaixo, o endereço por extenso para quem não consegue clicar.
    /// </summary>
    public static string Botao(string rotulo, string url) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:24px 0 8px;">
          <tr><td align="center" bgcolor="{CorDestaque}" style="border-radius:10px;">
            <a href="{E(url)}" target="_blank" style="display:inline-block;padding:14px 28px;font-family:{Fonte};font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:10px;">{E(rotulo)}</a>
          </td></tr>
        </table>
        <p style="margin:0 0 20px;font-size:12px;line-height:1.5;color:{Suave};word-break:break-all;">Se o botão não abrir, copie e cole no navegador:<br><a href="{E(url)}" style="color:{CorDestaque};">{E(url)}</a></p>
        """;

    /// <summary>Quadro de destaque com pares rótulo/valor (usuário, senha, data...).</summary>
    public static string Destaque(params (string Rotulo, string Valor)[] linhas)
    {
        var celulas = string.Concat(linhas.Select(l => $"""
            <tr>
              <td style="padding:6px 0;font-size:13px;color:{Suave};white-space:nowrap;vertical-align:top;">{E(l.Rotulo)}</td>
              <td style="padding:6px 0 6px 16px;font-size:15px;color:{Marinho};font-weight:700;word-break:break-all;">{E(l.Valor)}</td>
            </tr>
            """));

        return $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:8px 0 20px;background:#ECFDF5;border-left:4px solid {CorDestaque};border-radius:8px;">
              <tr><td style="padding:14px 18px;font-family:{Fonte};">
                <table role="presentation" cellpadding="0" cellspacing="0" border="0">{celulas}</table>
              </td></tr>
            </table>
            """;
    }

    /// <summary>Observação em letra menor, depois do conteúdo principal.</summary>
    public static string Nota(string texto) =>
        $"""<p style="margin:16px 0 0;padding-top:16px;border-top:1px solid #EEE;font-size:13px;line-height:1.6;color:{Suave};">{E(texto)}</p>""";
}
