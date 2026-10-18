using Atlas.Application.Business.Clientes.Common;
using Atlas.Application.Common.Exceptions;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Clientes.Enums;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Clientes;

public class ClienteAtualTests : IDisposable
{
    private const long UsuarioId = 10;

    private readonly ApplicationDbContext _context;

    public ClienteAtualTests()
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
        var servico = new ClienteAtual(new UsuarioFake(null), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveRecusarUsuarioSemVinculo()
    {
        await CriarClienteAsync(SituacaoCliente.Ativo, vincularUsuario: false);
        var servico = new ClienteAtual(new UsuarioFake(UsuarioId), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveRecusarVinculoInativo()
    {
        await CriarClienteAsync(SituacaoCliente.Ativo, vinculoAtivo: false);
        var servico = new ClienteAtual(new UsuarioFake(UsuarioId), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveRecusarClienteBloqueado()
    {
        await CriarClienteAsync(SituacaoCliente.Bloqueado);
        var servico = new ClienteAtual(new UsuarioFake(UsuarioId), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveDevolverClientePapelEFusoDoUsuario()
    {
        var cliente = await CriarClienteAsync(SituacaoCliente.Ativo);
        var servico = new ClienteAtual(new UsuarioFake(UsuarioId), _context);

        var resultado = await servico.ObterAsync(CancellationToken.None);

        resultado.Should().Be(new ClienteDoUsuario(cliente.Id, PapelCliente.Proprietario, "America/Manaus"));
    }

    [Fact]
    public async Task ObterAsync_DeveIgnorarVinculoDeOutroUsuario()
    {
        await CriarClienteAsync(SituacaoCliente.Ativo);
        var servico = new ClienteAtual(new UsuarioFake(UsuarioId + 1), _context);

        var acao = () => servico.ObterAsync(CancellationToken.None);

        await acao.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ObterAsync_DeveConsultarOBancoUmaVezPorRequisicao()
    {
        var cliente = await CriarClienteAsync(SituacaoCliente.Ativo);
        var servico = new ClienteAtual(new UsuarioFake(UsuarioId), _context);
        await servico.ObterAsync(CancellationToken.None);

        // Bloqueado depois da primeira leitura: a mesma requisição segue com o
        // valor já resolvido — a próxima requisição (nova instância) é que vê.
        cliente.Bloquear("Teste");
        await _context.SaveChangesAsync(CancellationToken.None);

        var resultado = await servico.ObterAsync(CancellationToken.None);

        resultado.ClienteId.Should().Be(cliente.Id);
    }

    private async Task<Cliente> CriarClienteAsync(
        SituacaoCliente situacao,
        bool vincularUsuario = true,
        bool vinculoAtivo = true)
    {
        var cliente = new Cliente
        {
            Nome = "Academia Centro",
            Email = "dono@academia.test",
            FusoHorario = "America/Manaus",
        };

        if (situacao == SituacaoCliente.Bloqueado)
        {
            cliente.Bloquear("Inadimplência");
        }

        _context.Clientes.Add(cliente);

        if (vincularUsuario)
        {
            _context.UsuariosCliente.Add(new UsuarioCliente
            {
                UsuarioId = UsuarioId,
                Cliente = cliente,
                Ativo = vinculoAtivo,
            });
        }

        await _context.SaveChangesAsync(CancellationToken.None);
        return cliente;
    }

    private sealed class UsuarioFake(long? id) : IUser
    {
        public long? Id { get; } = id;
        public List<string>? Roles => null;
    }
}
