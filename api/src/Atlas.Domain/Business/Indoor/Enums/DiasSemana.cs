namespace Atlas.Domain.Business.Indoor.Enums;

/// <summary>
/// Dias em que um item da playlist é exibido. Flags: a combinação vai num
/// inteiro só (a spec usava texto "1,2,3"), filtrável no SQL com
/// <c>dias_semana &amp; dia &lt;&gt; 0</c>. Os bits seguem <see cref="DayOfWeek"/>
/// (domingo = bit 0), para a conversão ser um deslocamento.
/// </summary>
[Flags]
public enum DiasSemana
{
    Nenhum = 0,
    Domingo = 1 << 0,
    Segunda = 1 << 1,
    Terca = 1 << 2,
    Quarta = 1 << 3,
    Quinta = 1 << 4,
    Sexta = 1 << 5,
    Sabado = 1 << 6,

    DiasUteis = Segunda | Terca | Quarta | Quinta | Sexta,
    FimDeSemana = Sabado | Domingo,
    Todos = DiasUteis | FimDeSemana
}

public static class DiasSemanaExtensions
{
    public static DiasSemana ParaDiasSemana(this DayOfWeek dia) => (DiasSemana)(1 << (int)dia);

    public static bool Inclui(this DiasSemana dias, DayOfWeek dia) => (dias & dia.ParaDiasSemana()) != 0;
}
