using System.Text;
using Atlas.Application.Business.Diretorio;
using Atlas.Domain.Business.Indoor.Enums;

namespace Atlas.Infrastructure.Business.Diretorio;

/// <summary>
/// A página que o celular abre ao ler o QR code da TV (<c>/d/{codigo}</c>):
/// HTML pronto, sem framework, com CSS e um script mínimo (copiar o Pix)
/// embutidos — abre num 4G fraco com uma requisição só (ADR-018).
/// </summary>
/// <remarks>
/// Todo texto é codificado (<c>&amp; &lt; &gt; " '</c>, o bastante para texto
/// e atributo entre aspas) e só entram endereços
/// <c>https:</c>, <c>http:</c> e <c>tel:</c>: o conteúdo vem do cadastro dos
/// parceiros. A política de conteúdo (CSP) só libera o CSS e o script desta
/// resposta, pelo <c>nonce</c>.
/// </remarks>
public static class PaginaDiretorio
{
    /// <summary>Quantas cores de avatar (classes <c>c0</c>… no CSS).</summary>
    private const int Cores = 8;

    public static string Politica(string nonce) =>
        $"default-src 'none'; style-src 'nonce-{nonce}'; script-src 'nonce-{nonce}'; img-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    public static string Renderizar(DiretorioPublicoDto diretorio, string nonce)
    {
        var html = new StringBuilder();
        var titulo = diretorio.Estabelecimento is { } local ? $"Anunciantes · {local}" : "Anunciantes desta TV";

        Inicio(html, titulo, nonce);
        html.Append("<header><p class=\"marca\">Mídia Indoor</p><h1>Anunciantes</h1><p class=\"sub\">")
            .Append(diretorio.Estabelecimento is { } nome ? $"Quem aparece na TV do {T(nome)}" : "Quem aparece nesta TV")
            .Append("</p></header><main>");

        if (diretorio.Anunciantes.Count == 0)
        {
            html.Append("<p class=\"vazio\">Nenhum anunciante publicou os contatos aqui ainda. Volte mais tarde.</p>");
        }

        foreach (var anunciante in diretorio.Anunciantes)
        {
            Cartao(html, anunciante);
        }

        html.Append("</main><footer><p>Os contatos são publicados pelos próprios anunciantes.</p></footer>");
        Script(html, nonce);
        html.Append("</body></html>");

        return html.ToString();
    }

    public static string NaoEncontrada(string nonce)
    {
        var html = new StringBuilder();
        Inicio(html, "Página indisponível", nonce);
        html.Append("<header><p class=\"marca\">Mídia Indoor</p><h1>Página indisponível</h1></header><main>")
            .Append("<p class=\"vazio\">Este endereço não existe ou o diretório desta TV foi desligado. ")
            .Append("Aponte a câmera de novo para o QR code da TV.</p></main></body></html>");
        return html.ToString();
    }

    private static void Cartao(StringBuilder html, AnuncianteDiretorioDto anunciante)
    {
        // Soma dos caracteres: a mesma cor para o mesmo nome em qualquer processo.
        var cor = anunciante.Nome.Sum(c => c) % Cores;

        // Cor por classe, não por style="": a CSP bloqueia estilo inline sem nonce.
        html.Append("<article class=\"cartao\"><div class=\"topo\"><span class=\"avatar c").Append(cor)
            .Append("\" aria-hidden=\"true\">").Append(T(Iniciais(anunciante.Nome))).Append("</span><div><h2>")
            .Append(T(anunciante.Nome)).Append("</h2>");

        if (anunciante.Anfitriao || anunciante.Categoria is not null)
        {
            html.Append("<p class=\"etiquetas\">");
            if (anunciante.Anfitriao)
            {
                html.Append("<span class=\"aqui\">Você está aqui</span>");
            }

            if (anunciante.Categoria is { } categoria)
            {
                html.Append("<span>").Append(T(categoria)).Append("</span>");
            }

            html.Append("</p>");
        }

        html.Append("</div></div>");

        if (anunciante.Descricao is { } descricao)
        {
            html.Append("<p class=\"descricao\">").Append(T(descricao)).Append("</p>");
        }

        var botoes = anunciante.Contatos.Where(EnderecoPermitido).ToList();
        if (botoes.Count > 0)
        {
            html.Append("<div class=\"botoes\">");
            foreach (var contato in botoes)
            {
                var classe = "b-" + contato.Tipo.ToString().ToLowerInvariant();
                if (contato.Tipo == TipoQrCode.Pix)
                {
                    html.Append("<button type=\"button\" class=\"botao ").Append(classe).Append("\" data-pix=\"")
                        .Append(T(contato.Valor)).Append("\">").Append(T(contato.Rotulo)).Append("</button>");
                }
                else
                {
                    html.Append("<a class=\"botao ").Append(classe).Append("\" href=\"").Append(T(contato.Valor))
                        .Append("\" rel=\"noopener noreferrer\"")
                        .Append(contato.Tipo == TipoQrCode.Telefone ? string.Empty : " target=\"_blank\"")
                        .Append('>').Append(T(contato.Rotulo)).Append("</a>");
                }
            }

            html.Append("</div>");
        }

        html.Append("</article>");
    }

    /// <summary>Pix é texto; o resto só com esquema conhecido (nada de <c>javascript:</c>).</summary>
    public static bool EnderecoPermitido(ContatoDiretorioDto contato) =>
        contato.Tipo == TipoQrCode.Pix
        || contato.Valor.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || contato.Valor.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || (contato.Tipo == TipoQrCode.Telefone && contato.Valor.StartsWith("tel:+", StringComparison.Ordinal));

    public static string Iniciais(string nome)
    {
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => char.IsLetterOrDigit(p[0]))
            .ToList();

        return partes.Count switch
        {
            0 => "?",
            1 => partes[0][..Math.Min(2, partes[0].Length)].ToUpperInvariant(),
            _ => string.Concat(char.ToUpperInvariant(partes[0][0]), char.ToUpperInvariant(partes[^1][0])),
        };
    }

    /// <summary>
    /// Codificação mínima e segura: o <c>HtmlEncoder</c> padrão também troca
    /// acentos e o <c>+</c> por entidades, o que só aumenta a página.
    /// </summary>
    public static string T(string texto) => texto
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&#39;", StringComparison.Ordinal);

    private static void Inicio(StringBuilder html, string titulo, string nonce)
    {
        html.Append("<!doctype html><html lang=\"pt-BR\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<meta name=\"robots\" content=\"noindex, nofollow\">")
            .Append("<meta name=\"theme-color\" content=\"#0F1F2E\">")
            .Append("<title>").Append(T(titulo)).Append("</title>")
            .Append("<style nonce=\"").Append(nonce).Append("\">").Append(Estilo).Append("</style></head><body>");
    }

    private static void Script(StringBuilder html, string nonce)
    {
        html.Append("<script nonce=\"").Append(nonce).Append("\">").Append("""
            document.querySelectorAll('[data-pix]').forEach(function (b) {
              b.addEventListener('click', function () {
                var texto = b.getAttribute('data-pix'), rotulo = b.textContent;
                function feito() { b.textContent = 'Copiado! Cole no app do banco'; setTimeout(function () { b.textContent = rotulo; }, 4000); }
                if (navigator.clipboard && window.isSecureContext) {
                  navigator.clipboard.writeText(texto).then(feito, function () { window.prompt('Copie o código Pix:', texto); });
                } else { window.prompt('Copie o código Pix:', texto); }
              });
            });
            """).Append("</script>");
    }

    private const string Estilo = """
        :root{--fundo:#F3F5F7;--cartao:#fff;--texto:#0F1F2E;--suave:#5B6B7A;--borda:#E2E8F0;--marca:#0F766E}
        @media (prefers-color-scheme:dark){:root{--fundo:#0B1620;--cartao:#13222F;--texto:#E8EEF3;--suave:#9AAAB8;--borda:#1F3343}}
        *{box-sizing:border-box}
        body{margin:0;background:var(--fundo);color:var(--texto);font:16px/1.45 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
        header{background:#0F1F2E;color:#fff;padding:24px 16px 28px}
        header,main,footer{max-width:560px;margin:0 auto}
        @media (min-width:592px){header{border-radius:0 0 16px 16px}}
        .marca{margin:0;font-size:13px;letter-spacing:.06em;text-transform:uppercase;color:#5EEAD4}
        h1{margin:4px 0 2px;font-size:26px}
        .sub{margin:0;color:#C7D2DC}
        main{padding:16px;display:grid;gap:12px}
        .cartao{background:var(--cartao);border:1px solid var(--borda);border-radius:14px;padding:16px}
        .topo{display:flex;gap:12px;align-items:center}
        .c0{background:#0F766E}.c1{background:#1D4ED8}.c2{background:#7C3AED}.c3{background:#B45309}
        .c4{background:#BE123C}.c5{background:#047857}.c6{background:#4338CA}.c7{background:#A21CAF}
        .avatar{flex:none;width:48px;height:48px;border-radius:50%;display:grid;place-items:center;color:#fff;font-weight:700}
        h2{margin:0;font-size:18px;line-height:1.25}
        .etiquetas{margin:4px 0 0;display:flex;flex-wrap:wrap;gap:6px;font-size:13px;color:var(--suave)}
        .etiquetas span{border:1px solid var(--borda);border-radius:999px;padding:1px 8px}
        .etiquetas .aqui{background:var(--marca);border-color:var(--marca);color:#fff}
        .descricao{margin:10px 0 0;color:var(--suave)}
        .botoes{margin-top:12px;display:grid;grid-template-columns:repeat(auto-fit,minmax(130px,1fr));gap:8px}
        .botao{display:block;width:100%;min-height:44px;padding:10px 12px;border:0;border-radius:10px;font:inherit;font-weight:600;text-align:center;text-decoration:none;color:#fff;background:var(--marca);cursor:pointer}
        .b-whatsapp{background:#128C4A}.b-instagram{background:#C13584}.b-pix{background:#0E7490}.b-telefone{background:#334155}
        .vazio{text-align:center;color:var(--suave);padding:32px 8px}
        footer{padding:8px 16px 32px;text-align:center;font-size:13px;color:var(--suave)}
        """;
}
