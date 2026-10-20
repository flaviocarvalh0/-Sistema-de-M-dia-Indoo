namespace Atlas.Domain.Business.Indoor.Enums;

/// <summary>
/// Situação da tela para o painel. Online/Offline é calculada a partir da
/// última sincronização (<see cref="Entities.EstadoTela"/>), não gravada.
/// </summary>
public enum SituacaoTela
{
    /// <summary>Cadastrada, ainda sem TV pareada.</summary>
    AguardandoPareamento = 1,
    Online = 2,
    Offline = 3,

    /// <summary>Sincroniza, mas não está exibindo há mais que a tolerância (heartbeat, ADR-024).</summary>
    ForaDeExibicao = 4,
}
