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
/// clientes. A política de conteúdo (CSP) só libera o CSS e o script desta
/// resposta, pelo <c>nonce</c>.
/// </remarks>
public static class PaginaDiretorio
{
    /// <summary>Quantas cores de avatar (classes <c>c0</c>… no CSS).</summary>
    private const int Cores = 8;

    public static string Politica(string nonce) =>
        $"default-src 'none'; style-src 'nonce-{nonce}'; script-src 'nonce-{nonce}'; connect-src 'self'; img-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    public static string Renderizar(DiretorioPublicoDto diretorio, string nonce)
    {
        var html = new StringBuilder();
        var titulo = diretorio.Estabelecimento is { } local ? $"Anunciantes · {local}" : "Anunciantes desta TV";

        Inicio(html, titulo, nonce);
        var quantos = diretorio.Anunciantes.Count;
        html.Append("<header><p class=\"marca\">Mídia Indoor</p><h1>Anunciantes</h1><p class=\"sub\">")
            .Append(diretorio.Estabelecimento is { } nome ? $"Na TV de <strong>{T(nome)}</strong>" : "Quem aparece nesta TV")
            .Append("</p>");
        if (quantos > 0)
        {
            html.Append("<p class=\"dica\">Viu na TV? Fale direto com ")
                .Append(quantos == 1 ? "o anunciante" : $"os {quantos} anunciantes").Append(".</p>");
        }

        html.Append("</header><main>");

        if (diretorio.Anunciantes.Count == 0)
        {
            html.Append("<p class=\"vazio\">Nenhum anunciante publicou os contatos aqui ainda. Volte mais tarde.</p>");
        }

        foreach (var anunciante in diretorio.Anunciantes)
        {
            Cartao(html, anunciante);
        }

        html.Append("</main>");

        if (diretorio.LinkAnuncie is { } anuncie)
        {
            html.Append("<section class=\"anuncie\"><div><p class=\"anuncie-titulo\">Quer anunciar aqui?</p>")
                .Append("<p class=\"anuncie-texto\">Seu negócio nesta TV, visto por quem frequenta o local.</p></div>")
                .Append("<a class=\"anuncie-botao\" data-toque=\"anuncie\" href=\"").Append(T(anuncie))
                .Append("\" target=\"_blank\" rel=\"noopener\">Clique aqui e fale com a gente</a></section>");
        }

        html.Append("<footer><p>Os contatos são publicados pelos próprios anunciantes.</p></footer>");
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

        // A descrição que só repete a categoria não acrescenta nada.
        if (anunciante.Descricao is { } descricao
            && !string.Equals(descricao.Trim(), anunciante.Categoria?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            html.Append("<p class=\"descricao\">").Append(T(descricao)).Append("</p>");
        }

        var contatos = anunciante.Contatos.Where(EnderecoPermitido).ToList();
        if (contatos.Count > 0)
        {
            html.Append("<ul class=\"contatos\">");
            foreach (var contato in contatos)
            {
                Contato(html, contato, anunciante.ClienteId);
            }

            html.Append("</ul>");
        }

        html.Append("</article>");
    }

    /// <summary>
    /// Uma linha por contato: ícone, o que é ("WhatsApp") e para onde vai
    /// ("(92) 99999-0000"). O Pix é botão de copiar; o resto é link.
    /// </summary>
    private static void Contato(StringBuilder html, ContatoDiretorioDto contato, long clienteId)
    {
        var tipo = contato.Tipo.ToString().ToLowerInvariant();
        var detalhe = contato.Tipo == TipoQrCode.Pix ? "Código copia e cola" : contato.Detalhe;
        // Quem é e qual contato, para a contagem de toques (ADR-027).
        var toque = $" data-toque=\"contato\" data-cliente=\"{clienteId}\" data-tipo=\"{contato.Tipo}\"";

        html.Append("<li>");
        if (contato.Tipo == TipoQrCode.Pix)
        {
            html.Append("<button type=\"button\" class=\"contato\"").Append(toque).Append(" data-pix=\"").Append(T(contato.Valor)).Append("\">");
        }
        else
        {
            html.Append("<a class=\"contato\"").Append(toque).Append(" href=\"").Append(T(contato.Valor)).Append("\" rel=\"noopener noreferrer\"")
                .Append(contato.Tipo == TipoQrCode.Telefone ? string.Empty : " target=\"_blank\"").Append('>');
        }

        html.Append("<span class=\"icone i-").Append(tipo).Append("\" aria-hidden=\"true\">").Append(Icone(contato.Tipo)).Append("</span>")
            .Append("<span class=\"textos\"><span class=\"rotulo\">").Append(T(contato.Rotulo)).Append("</span>");
        if (detalhe is not null)
        {
            html.Append("<span class=\"detalhe\">").Append(T(detalhe)).Append("</span>");
        }

        html.Append("</span><svg class=\"seta\" viewBox=\"0 0 24 24\" aria-hidden=\"true\"><path d=\"M9 6l6 6-6 6\"/></svg>")
            .Append(contato.Tipo == TipoQrCode.Pix ? "</button>" : "</a>")
            .Append("</li>");
    }

    /// <summary>Ícones de traço, embutidos (a CSP não deixa buscar nada fora).</summary>
    private static string Icone(TipoQrCode tipo)
    {
        var desenho = tipo switch
        {
            TipoQrCode.WhatsApp => "<path d=\"M20 11.5a8.5 8.5 0 0 1-12.6 7.4L3 20l1.2-4.2A8.5 8.5 0 1 1 20 11.5z\"/><path d=\"M9 9.5c0 3 2.5 5.5 5.5 5.5\"/>",
            TipoQrCode.Instagram => "<rect x=\"3.5\" y=\"3.5\" width=\"17\" height=\"17\" rx=\"5\"/><circle cx=\"12\" cy=\"12\" r=\"4\"/><circle cx=\"17\" cy=\"7\" r=\"0.8\"/>",
            TipoQrCode.Telefone => "<path d=\"M5 4h3.5l2 5-2.5 1.5a11 11 0 0 0 5.5 5.5L15 13.5l5 2V19a2 2 0 0 1-2 2A16 16 0 0 1 3 6a2 2 0 0 1 2-2z\"/>",
            TipoQrCode.Pix => "<path d=\"M12 3l9 9-9 9-9-9z\"/><path d=\"M8.5 12h7\"/>",
            _ => "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M3 12h18M12 3c2.5 2.5 3.5 5.5 3.5 9s-1 6.5-3.5 9M12 3c-2.5 2.5-3.5 5.5-3.5 9s1 6.5 3.5 9\"/>",
        };

        return "<svg viewBox=\"0 0 24 24\">" + desenho + "</svg>";
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
            // Conta o toque (ADR-027) sem segurar o link: o beacon sai mesmo
            // com a página indo embora para o WhatsApp ou o Instagram.
            document.querySelectorAll('[data-toque]').forEach(function (el) {
              el.addEventListener('click', function () {
                if (!navigator.sendBeacon) return;
                var q = 'e=' + encodeURIComponent(el.getAttribute('data-toque'));
                if (el.hasAttribute('data-cliente')) q += '&c=' + encodeURIComponent(el.getAttribute('data-cliente'));
                if (el.hasAttribute('data-tipo')) q += '&t=' + encodeURIComponent(el.getAttribute('data-tipo'));
                try { navigator.sendBeacon(location.pathname.replace(/\/+$/, '') + '/toque?' + q); } catch (e) { }
              });
            });
            document.querySelectorAll('[data-pix]').forEach(function (b) {
              b.addEventListener('click', function () {
                var texto = b.getAttribute('data-pix'), alvo = b.querySelector('.detalhe') || b, antes = alvo.textContent;
                function feito() {
                  b.classList.add('copiado'); alvo.textContent = 'Copiado! Cole no app do banco';
                  setTimeout(function () { b.classList.remove('copiado'); alvo.textContent = antes; }, 4000);
                }
                if (navigator.clipboard && window.isSecureContext) {
                  navigator.clipboard.writeText(texto).then(feito, function () { window.prompt('Copie o código Pix:', texto); });
                } else { window.prompt('Copie o código Pix:', texto); }
              });
            });
            """).Append("</script>");
    }

    private const string Estilo = """
        :root{--fundo:#F3F5F7;--cartao:#fff;--texto:#0F1F2E;--suave:#5B6B7A;--borda:#E2E8F0;--linha:#F7F9FB;--marca:#0F766E}
        @media (prefers-color-scheme:dark){:root{--fundo:#0B1620;--cartao:#13222F;--texto:#E8EEF3;--suave:#9AAAB8;--borda:#22384A;--linha:#182B3A}}
        *{box-sizing:border-box}
        body{margin:0;background:var(--fundo);color:var(--texto);font:16px/1.45 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif}
        header{background:#0F1F2E;color:#fff;padding:24px 20px 28px}
        header,main,footer{max-width:560px;margin:0 auto}
        @media (min-width:592px){header{border-radius:0 0 18px 18px}}
        .marca{margin:0;font-size:12px;letter-spacing:.08em;text-transform:uppercase;color:#5EEAD4}
        h1{margin:4px 0 4px;font-size:28px;line-height:1.15}
        .sub{margin:0;color:#D6DEE5}
        .dica{margin:10px 0 0;font-size:14px;color:#9FB2C2}
        main{padding:16px;display:grid;gap:14px}
        .cartao{background:var(--cartao);border:1px solid var(--borda);border-radius:16px;padding:16px;box-shadow:0 1px 2px rgba(15,31,46,.06)}
        .topo{display:flex;gap:12px;align-items:center}
        .avatar{flex:none;width:48px;height:48px;border-radius:50%;display:grid;place-items:center;color:#fff;font-weight:700;letter-spacing:.02em}
        .c0{background:#0F766E}.c1{background:#1D4ED8}.c2{background:#7C3AED}.c3{background:#B45309}
        .c4{background:#BE123C}.c5{background:#047857}.c6{background:#4338CA}.c7{background:#A21CAF}
        h2{margin:0;font-size:18px;line-height:1.25}
        .etiquetas{margin:6px 0 0;display:flex;flex-wrap:wrap;gap:6px;font-size:12px;color:var(--suave)}
        .etiquetas span{border:1px solid var(--borda);border-radius:999px;padding:1px 9px}
        .etiquetas .aqui{background:var(--marca);border-color:var(--marca);color:#fff;font-weight:600}
        .descricao{margin:12px 0 0;color:var(--suave)}
        .contatos{list-style:none;margin:14px 0 0;padding:0;display:grid;gap:8px}
        .contato{display:flex;align-items:center;gap:12px;width:100%;min-height:56px;padding:8px 12px;border:1px solid var(--borda);border-radius:12px;background:var(--linha);color:var(--texto);font:inherit;text-align:left;text-decoration:none;cursor:pointer}
        .contato:active{transform:scale(.99)}
        .contato:focus-visible{outline:3px solid #5EEAD4;outline-offset:2px}
        .icone{flex:none;width:38px;height:38px;border-radius:10px;display:grid;place-items:center;color:#fff}
        .icone svg,.seta{width:22px;height:22px;fill:none;stroke:currentColor;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round}
        .i-whatsapp{background:#128C4A}.i-instagram{background:#C13584}.i-link{background:#0F766E}.i-telefone{background:#334155}.i-pix{background:#0E7490}
        .textos{flex:1;min-width:0;display:grid}
        .rotulo{font-weight:650}
        .detalhe{font-size:14px;color:var(--suave);overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
        .copiado .detalhe{color:#0E7490;font-weight:600}
        .seta{flex:none;width:18px;height:18px;color:var(--suave)}
        .vazio{text-align:center;color:var(--suave);padding:32px 8px}
        footer{padding:4px 16px 32px;text-align:center;font-size:13px;color:var(--suave)}
        .anuncie{max-width:528px;margin:4px auto 16px;padding:18px;border-radius:18px;color:#F6F8F7;display:grid;gap:14px;border:1px solid rgba(157,234,196,.2);background:radial-gradient(ellipse at 10% 120%,rgba(73,179,147,.25),transparent 50%),linear-gradient(110deg,#132927,#10201F 60%,#182A27)}
        @media (max-width:560px){.anuncie{margin:4px 16px 16px}}
        .anuncie-titulo{margin:0;font-size:20px;font-weight:800;letter-spacing:-.02em}
        .anuncie-texto{margin:4px 0 0;font-size:14px;color:#B4C1C0}
        .anuncie-botao{display:block;text-align:center;padding:13px 16px;border-radius:12px;background:linear-gradient(145deg,#B8F36B,#82D9AA);color:#13221B;font-weight:800;text-decoration:none}
        .anuncie-botao:focus-visible{outline:3px solid #5EEAD4;outline-offset:2px}
        """;
}
