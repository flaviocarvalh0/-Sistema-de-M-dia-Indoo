namespace Atlas.Domain.Business.Indoor.Enums;

/// <summary>O que o QR code do anúncio abre no celular de quem lê.</summary>
public enum TipoQrCode
{
    /// <summary>Endereço de site (https).</summary>
    Link = 1,

    /// <summary>Conversa no WhatsApp (wa.me), com mensagem inicial opcional.</summary>
    WhatsApp = 2,

    /// <summary>Perfil do Instagram.</summary>
    Instagram = 3,

    /// <summary>Ligação telefônica (tel:).</summary>
    Telefone = 4,

    /// <summary>Pagamento Pix (copia e cola, padrão BR Code do Banco Central).</summary>
    Pix = 5,
}

/// <summary>Canto da tela onde o QR code aparece, por cima da mídia.</summary>
public enum PosicaoQrCode
{
    InferiorDireito = 1,
    InferiorEsquerdo = 2,
    SuperiorDireito = 3,
    SuperiorEsquerdo = 4,
}

/// <summary>Tamanho do QR code na tela (fração da menor dimensão da TV).</summary>
public enum TamanhoQrCode
{
    Pequeno = 1,
    Medio = 2,
    Grande = 3,
}
