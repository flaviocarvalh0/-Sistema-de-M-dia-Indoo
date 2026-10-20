namespace Atlas.Domain.Business.Indoor.Enums;

/// <summary>O que a TV tem na tela (heartbeat de reprodução, ADR-024).</summary>
public enum ModoReproducao
{
    /// <summary>Um anúncio (vídeo ou imagem).</summary>
    Anuncio = 1,

    /// <summary>Notícia ou cartão (tempo, cotações).</summary>
    Informativo = 2,

    /// <summary>Tela de espera (nada para tocar).</summary>
    Espera = 3,
}

/// <summary>Por que a TV, mesmo sincronizando, não está exibindo.</summary>
public enum MotivoForaDeExibicao
{
    /// <summary>O app não está em primeiro plano (alguém abriu outro app, a TV voltou ao início).</summary>
    AppForaDaTela = 1,

    /// <summary>O item na tela passou muito do tempo dele (vídeo travado, loop parado).</summary>
    ReproducaoTravada = 2,

    /// <summary>Tela de espera com anúncios para hoje (os arquivos não abrem ou não baixaram).</summary>
    EsperaComAnuncios = 3,
}
