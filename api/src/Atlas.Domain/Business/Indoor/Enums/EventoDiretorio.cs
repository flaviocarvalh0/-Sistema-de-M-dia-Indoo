namespace Atlas.Domain.Business.Indoor.Enums;

/// <summary>O que aconteceu na página do diretório de uma TV (<c>/d/{codigo}</c>).</summary>
public enum EventoDiretorio
{
    /// <summary>A página foi aberta (o QR code da TV foi lido).</summary>
    Acesso = 1,

    /// <summary>Toque num contato de um anunciante (WhatsApp, Instagram, site, telefone, Pix).</summary>
    Contato = 2,

    /// <summary>Toque em "Quer anunciar aqui?" (WhatsApp da operadora).</summary>
    Anuncie = 3,
}
