namespace Atlas.Application.Business.Common;

/// <summary>Configurações do produto que não são segredo (seção <c>Indoor</c>).</summary>
public interface IConfiguracaoIndoor
{
    /// <summary>Endereço do painel (pasta <c>web/</c>), usado nos e-mails.</summary>
    string UrlPainel { get; }

    /// <summary>De quanto em quanto tempo a TV consulta a grade (<c>Indoor:Dispositivo:IntervaloSincronizacaoSegundos</c>, padrão 300).</summary>
    TimeSpan IntervaloSincronizacao { get; }

    /// <summary>Validade do token da TV (<c>Indoor:Dispositivo:ValidadeTokenDias</c>, padrão 30). Renovado sozinho na sincronização.</summary>
    TimeSpan ValidadeTokenDispositivo { get; }

    /// <summary>Validade das URLs de download entregues às TVs (<c>Indoor:Dispositivo:ValidadeUrlDownloadHoras</c>, padrão 24).</summary>
    TimeSpan ValidadeUrlDownload { get; }
}
