using Atlas.Application.Business.Parceiros.Common;
using Atlas.Application.Common.Exceptions;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Business.Parceiros.Enums;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Parceiros;

public class ParceiroAtualTests : IDisposable
{
    private const long UsuarioId = 10;

    private readonly ApplicationDbContext _context;

    public ParceiroAtualTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ObterAsync_DeveRecusarRequisicaoSemUsuario()
    {
        var servico = new ParceiroAtual(new UsuarioFake(null), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveRecusarUsuarioSemVinculo()
    {
        await CriarParceiroAsync(SituacaoParceiro.Ativo, vincularUsuario: false);
        var servico = new ParceiroAtual(new UsuarioFake(UsuarioId), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveRecusarVinculoInativo()
    {
        await CriarParceiroAsync(SituacaoParceiro.Ativo, vinculoAtivo: false);
        var servico = new ParceiroAtual(new UsuarioFake(UsuarioId), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveRecusarParceiroBloqueado()
    {
        await CriarParceiroAsync(SituacaoParceiro.Bloqueado);
        var servico = new ParceiroAtual(new UsuarioFake(UsuarioId), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveDevolverParceiroPapelEFusoDoUsuario()
    {
        var parceiro = await CriarParceiroAsync(SituacaoParceiro.Ativo);
        var servico = new ParceiroAtual(new UsuarioFake(UsuarioId), _context);

        var resultado = await servico.ObterAsync(CancellationToken.None);

        resultado.Should().Be(new ParceiroDoUsuario(parceiro.Id, PapelParceiro.Proprietario, "America/Manaus"));
    }

    [Fact]
    public async Task ObterAsync_DeveIgnorarVinculoDeOutroUsuario()
    {
        await CriarParceiroAsync(SituacaoParceiro.Ativo);
        var servico = new ParceiroAtual(new UsuarioFake(UsuarioId + 1), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveConsultarOBancoUmaVezPorRequisicao()
    {
        var parceiro = await CriarParceiroAsync(SituacaoParceiro.Ativo);
        var servico = new ParceiroAtual(new UsuarioFake(UsuarioId), _context);
        await servico.ObterAsync(CancellationToken.None);

        // Bloqueado depois da primeira leitura: a mesma requisição segue com o
        // valor já resolvido — a próxima requisição (nova instância) é que vê.
        parceiro.Bloquear("Teste");
        await _context.SaveChangesAsync(CancellationToken.None);

        var resultado = await servico.ObterAsync(CancellationToken.None);

        resultado.ParceiroId.Should().Be(parceiro.Id);
    }

    private async Task<Parceiro> CriarParceiroAsync(
        SituacaoParceiro situacao,
        bool vincularUsuario = true,
        bool vinculoAtivo = true)
    {
        var parceiro = new Parceiro
        {
            Nome = "Academia Centro",
            Email = "dono@academia.test",
            FusoHorario = "America/Manaus",
        };

        if (situacao == SituacaoParceiro.Bloqueado)
        {
            parceiro.Bloquear("Inadimplência");
        }

        _context.Parceiros.Add(parceiro);

        if (vincularUsuario)
        {
            _context.UsuariosParceiro.Add(new UsuarioParceiro
            {
                UsuarioId = UsuarioId,
                Parceiro = parceiro,
                Ativo = vinculoAtivo,
            });
        }

        await _context.SaveChangesAsync(CancellationToken.None);
        return parceiro;
    }

    private sealed class UsuarioFake(long? id) : IUser
    {
        public long? Id { get; } = id;
        public List<string>? Roles => null;
    }
}
