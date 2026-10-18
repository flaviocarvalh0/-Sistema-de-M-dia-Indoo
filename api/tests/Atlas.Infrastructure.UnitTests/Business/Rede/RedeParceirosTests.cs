using Atlas.Application.Business.Rede.Parceiros;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Infrastructure.Data;
using Atlas.Infrastructure.UnitTests.Business.Indoor;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Rede;

/// <summary>Parceiros: os locais das TVs, sem acesso ao sistema.</summary>
public class RedeParceirosTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task Criar_DeveNormalizarEGravarSemCriarAcesso()
    {
        var id = await new CriarParceiroCommandHandler(_context).Handle(new CriarParceiroCommand
        {
            Nome = " Academia Centro ",
            Telefone = "(92) 99999-0000",
            Email = " Contato@Academia.COM ",
            Documento = "529.982.247-25",
            Endereco = " Av. Brasil, 100 ",
            Cidade = "Manaus",
            FusoHorario = "America/Manaus",
        }, CancellationToken.None);

        var dto = await Obter(id);
        dto.Should().BeEquivalentTo(new
        {
            Nome = "Academia Centro",
            Telefone = "92999990000",
            Email = "contato@academia.com",
            Documento = "52998224725",
            Endereco = "Av. Brasil, 100",
            FusoHorario = "America/Manaus",
            Ativo = true,
            ClienteId = (long?)null,
            QuantidadeTelas = 0,
        });
        _context.UsuariosCliente.Should().BeEmpty("o parceiro não acessa o sistema");
    }

    [Fact]
    public async Task Vinculo_ComClienteQueTambemAnuncia()
    {
        var cliente = new Cliente { Nome = "Academia Centro", Email = "a@teste.local" };
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();

        var id = await new CriarParceiroCommandHandler(_context)
            .Handle(new CriarParceiroCommand { Nome = "Academia", ClienteId = cliente.Id }, CancellationToken.None);

        (await Obter(id)).ClienteNome.Should().Be("Academia Centro");

        var inexistente = () => new CriarParceiroCommandHandler(_context)
            .Handle(new CriarParceiroCommand { Nome = "Outro", ClienteId = 999 }, CancellationToken.None);
        (await inexistente.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("ClienteId");
    }

    [Fact]
    public async Task Validacao_DeveRecusarCamposInvalidos()
    {
        var erros = (await new CriarParceiroCommandValidator().ValidateAsync(new CriarParceiroCommand
        {
            Nome = "",
            Telefone = "123",
            Email = "não-é-email",
            Documento = "111.111.111-11",
            FusoHorario = "Lua/Base",
        })).Errors.Select(e => e.PropertyName).Distinct();

        erros.Should().BeEquivalentTo("Nome", "Telefone", "Email", "Documento", "FusoHorario");
    }

    [Fact]
    public async Task Excluir_ComTelaDeveSerRecusado_SemTelaApaga()
    {
        var comTela = new Parceiro { Nome = "Com TV" };
        var semTela = new Parceiro { Nome = "Sem TV" };
        _context.Parceiros.AddRange(comTela, semTela);
        _context.Telas.Add(new Tela { Parceiro = comTela, Nome = "Recepção" });
        await _context.SaveChangesAsync();

        var acao = () => new ExcluirParceiroCommandHandler(_context).Handle(new ExcluirParceiroCommand(comTela.Id), CancellationToken.None);
        (await acao.Should().ThrowAsync<ValidationException>()).Which.Errors["Id"].Should().ContainMatch("*1 tela*");

        await new ExcluirParceiroCommandHandler(_context).Handle(new ExcluirParceiroCommand(semTela.Id), CancellationToken.None);
        _context.Parceiros.Select(p => p.Nome).Should().Equal("Com TV");
        (await Obter(comTela.Id)).QuantidadeTelas.Should().Be(1);
    }

    [Fact]
    public async Task Inativar_DeveTirarDaListaDeAtivos()
    {
        var parceiro = new Parceiro { Nome = "Padaria" };
        _context.Parceiros.Add(parceiro);
        await _context.SaveChangesAsync();

        await new DefinirAtivoParceiroCommandHandler(_context)
            .Handle(new DefinirAtivoParceiroCommand(parceiro.Id, false, 0), CancellationToken.None);

        var ativos = await new GetParceirosQueryHandler(_context, new IdentityNomesFake())
            .Handle(new GetParceirosQuery { Ativo = true }, CancellationToken.None);
        ativos.Items.Should().BeEmpty();
    }

    private Task<ParceiroDto> Obter(long id) =>
        new GetParceiroByIdQueryHandler(_context, new IdentityNomesFake()).Handle(new GetParceiroByIdQuery(id), CancellationToken.None);
}
