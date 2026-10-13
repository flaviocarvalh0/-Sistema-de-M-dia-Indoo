namespace Atlas.Application.Common.Interfaces;

/// <summary> 
/// Define o contrato para componentes responsáveis por higienizar dados em fluxos de observabilidade.
/// Remove ou mascara informações sensíveis (PII, credenciais) de objetos e coleções antes de serem persistidas ou logadas.
/// </summary>
public interface ILogSanitizer
{
    /// <summary>
    /// Varre recursivamente o objeto fornecido e substitui propriedades decoradas com atributos de sensibilidade por máscaras de segurança.
    /// </summary>
    /// <param name="value">O objeto, DTO, comando ou coleção a ser sanitizado.</param>
    /// <returns>
    /// Um novo objeto estruturado (geralmente um IDictionary de strings e objetos) contendo apenas os dados seguros, 
    /// ou o próprio valor caso seja um tipo primitivo/simples. Retorna <see langword="null"/> se o valor de entrada for nulo.
    /// </returns>
    object? Sanitize(object? value);
}
