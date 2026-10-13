namespace Atlas.Application.Business.Common;

/// <summary>Configurações do produto que não são segredo (seção <c>Indoor</c>).</summary>
public interface IConfiguracaoIndoor
{
    /// <summary>Endereço do painel (pasta <c>web/</c>), usado nos e-mails.</summary>
    string UrlPainel { get; }
}
