namespace Atlas.Infrastructure.IntegrationTests.Business;

/// <summary>
/// Os testes de negócio dependem das migrations mais recentes; aplicar as
/// pendentes antes de cada classe evita rodar de novo o
/// <c>configurar-banco-testes.ps1</c> a cada migration nova.
/// </summary>
internal static class BancoDeTestes
{
    public static async Task AtualizarAsync(DatabaseFixture fixture)
    {
        await using var context = fixture.CreateDbContext();
        await context.Database.MigrateAsync();
    }
}
