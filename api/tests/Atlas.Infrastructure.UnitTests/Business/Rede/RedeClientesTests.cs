using Atlas.Domain.Business.Parceiros.Entities;
using System.Runtime.CompilerServices;
using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Clientes.Commands.AtualizarCliente;
using Atlas.Application.Business.Rede.Clientes.Commands.BloqueioCliente;
using Atlas.Application.Business.Rede.Clientes.Commands.CriarCliente;
using Atlas.Application.Business.Rede.Clientes.Queries;
using Atlas.Application.Common.Exceptions;
using Atlas.Application.Common.Interfaces;
using Atlas.Application.Common.Models;
using Atlas.Application.Common.Models.Pagination;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Indoor.Enums;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Clientes.Enums;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Rede;

public class RedeClientesTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            // O cadastro grava cliente e e-mail numa transação; o InMemory não tem transação.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    private readonly IdentityFake _identity = new();
    private readonly EmailFake _email = new();

    public void Dispose() => _context.Dispose();

    // ---------- Cadastro ----------

    [Fact]
    public async Task Criar_DeveCriarUsuarioClienteVinculoEEmail()
    {
        var id = await Criar(Comando() with
        {
            Email = " Dono@Academia.TEST ",
            Telefone = "(92) 99999-0000",
            Documento = "11.222.333/0001-81",
            Cidade = " Manaus ",
            FusoHorario = "America/Manaus",
            CotaArmazenamentoMb = 300,
            ObservacaoInterna = "  ",
        });

        var cliente = await _context.Clientes.SingleAsync();
        cliente.Id.Should().Be(id);
        cliente.Nome.Should().Be("Academia Centro");
        cliente.Email.Should().Be("dono@academia.test");
        cliente.Telefone.Should().Be("92999990000");
        cliente.Documento.Should().Be("11222333000181");
        cliente.Cidade.Should().Be("Manaus");
        cliente.FusoHorario.Should().Be("America/Manaus");
        cliente.CotaArmazenamentoMb.Should().Be(300);
        cliente.ObservacaoInterna.Should().BeNull();
        cliente.Situacao.Should().Be(SituacaoCliente.Ativo);

        _identity.Criados.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            NomeCompleto = "Maria Souza",
            Usuario = "dono@academia.test",
            Email = "dono@academia.test",
            TrocarSenha = true,
            Grupos = new[] { BusinessRoles.Cliente },
        });

        var vinculo = await _context.UsuariosCliente.SingleAsync();
        vinculo.UsuarioId.Should().Be(IdentityFake.NovoUsuarioId);
        vinculo.ClienteId.Should().Be(cliente.Id);
        vinculo.Papel.Should().Be(PapelCliente.Proprietario);
        vinculo.Ativo.Should().BeTrue();

        var email = _email.Enfileirados.Should().ContainSingle().Subject;
        email.Recipients.Should().ContainSingle().Which.Email.Should().Be("dono@academia.test");
        email.HtmlBody.Should().Contain(_identity.Criados[0].Senha).And.Contain("https://painel.indoor.test/login");
        email.RelatedEntityType.Should().Be(nameof(Cliente));
        email.RelatedEntityId.Should().Be(cliente.Id.ToString());

        _identity.Inativados.Should().BeEmpty();
    }

    [Fact]
    public async Task Criar_FalhaAoCriarUsuarioNaoDeveCriarCliente()
    {
        _identity.ErroAoCriar = "Username 'dono@academia.test' is already taken.";

        var acao = () => Criar(Comando());

        (await acao.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey(nameof(CriarClienteCommand.Email));
        (await _context.Clientes.AnyAsync()).Should().BeFalse();
        _email.Enfileirados.Should().BeEmpty();
    }

    [Fact]
    public async Task Criar_EmailDeOutroClienteDeveDarErroAntesDeCriarUsuario()
    {
        await CriarClienteAsync();

        var acao = () => Criar(Comando() with { Email = "DONO@academia.test" });

        (await acao.Should().ThrowAsync<ValidationException>())
            .Which.Errors[nameof(CriarClienteCommand.Email)].Should().Equal("Já existe um cliente com este e-mail.");
        _identity.Criados.Should().BeEmpty();
    }

    [Fact]
    public async Task Criar_FalhaDepoisDoUsuarioDeveInativarOUsuario()
    {
        _email.Falhar = true;

        var acao = () => Criar(Comando());

        await acao.Should().ThrowAsync<InvalidOperationException>();
        _identity.Inativados.Should().Equal(IdentityFake.NovoUsuarioId);
    }

    [Fact]
    public void Validador_DeveRecusarCamposInvalidos()
    {
        var resultado = new CriarClienteCommandValidator().Validate(new CriarClienteCommand
        {
            Nome = "",
            NomeResponsavel = "",
            Email = "não-é-email",
            Telefone = "123",
            Documento = "111.111.111-11",
            FusoHorario = "Hora de Brasília",
            CotaArmazenamentoMb = 5000,
        });

        resultado.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            "Nome", "NomeResponsavel", "Email", "Telefone", "Documento", "FusoHorario", "CotaArmazenamentoMb");
    }

    [Fact]
    public void Validador_DeveAceitarOsOpcionaisVazios()
    {
        var resultado = new CriarClienteCommandValidator().Validate(Comando() with { Telefone = "", Documento = null, Cidade = null });

        resultado.IsValid.Should().BeTrue();
    }

    // ---------- Edição ----------

    [Fact]
    public async Task Atualizar_DeveNormalizarEDevolverAVersao()
    {
        var cliente = await CriarClienteAsync();

        await new AtualizarClienteCommandHandler(_context).Handle(new AtualizarClienteCommand
        {
            Id = cliente.Id,
            Nome = " Academia Centro II ",
            NomeResponsavel = " ",
            Telefone = "(11) 3333-4444",
            Documento = "529.982.247-25",
            Cidade = "São Paulo",
            FusoHorario = "America/Sao_Paulo",
            CotaArmazenamentoMb = 500,
            ObservacaoInterna = "Contrato anual",
            Version = 0,
        }, CancellationToken.None);

        var atualizado = await _context.Clientes.SingleAsync();
        atualizado.Nome.Should().Be("Academia Centro II");
        atualizado.NomeResponsavel.Should().BeNull();
        atualizado.Telefone.Should().Be("1133334444");
        atualizado.Documento.Should().Be("52998224725");
        atualizado.CotaArmazenamentoMb.Should().Be(500);
        atualizado.ObservacaoInterna.Should().Be("Contrato anual");
        atualizado.Email.Should().Be("dono@academia.test", "o e-mail de acesso não muda pela edição");
    }

    [Fact]
    public async Task Atualizar_ClienteInexistenteDeveDar404()
    {
        var acao = () => new AtualizarClienteCommandHandler(_context).Handle(
            new AtualizarClienteCommand { Id = 999, Nome = "X", Version = 1 }, CancellationToken.None);

        await acao.Should().ThrowAsync<Ardalis.GuardClauses.NotFoundException>();
    }

    // ---------- Bloqueio ----------

    [Fact]
    public async Task BloquearEDesbloquear_DevemAlternarASituacao()
    {
        var cliente = await CriarClienteAsync();

        await new BloquearClienteCommandHandler(_context).Handle(
            new BloquearClienteCommand { Id = cliente.Id, Motivo = " Inadimplência ", Version = 0 }, CancellationToken.None);

        cliente.Situacao.Should().Be(SituacaoCliente.Bloqueado);
        cliente.MotivoBloqueio.Should().Be("Inadimplência");

        await new DesbloquearClienteCommandHandler(_context).Handle(
            new DesbloquearClienteCommand { Id = cliente.Id, Version = 0 }, CancellationToken.None);

        cliente.Situacao.Should().Be(SituacaoCliente.Ativo);
        cliente.MotivoBloqueio.Should().BeNull();
    }

    [Fact]
    public async Task Bloquear_JaBloqueadoDeveDarErroDeValidacao()
    {
        var cliente = await CriarClienteAsync();
        cliente.Bloquear("Inadimplência");
        await _context.SaveChangesAsync();

        var acao = () => new BloquearClienteCommandHandler(_context).Handle(
            new BloquearClienteCommand { Id = cliente.Id, Motivo = "De novo", Version = 0 }, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Desbloquear_NaoBloqueadoDeveDarErroDeValidacao()
    {
        var cliente = await CriarClienteAsync();

        var acao = () => new DesbloquearClienteCommandHandler(_context).Handle(
            new DesbloquearClienteCommand { Id = cliente.Id, Version = 0 }, CancellationToken.None);

        await acao.Should().ThrowAsync<ValidationException>();
    }

    // ---------- Consulta ----------

    [Fact]
    public async Task Listar_DeveBuscarFiltrarEContarTelasEArmazenamento()
    {
        var academia = await CriarClienteAsync("Academia Centro", "92999990000");
        var tapiocaria = await CriarClienteAsync("Tapiocaria da Praia", "85988887777");
        tapiocaria.Bloquear("Inadimplência");

        // QuantidadeTelas: TVs onde os anúncios do cliente passam (a midia ativa numa playlist ativa).
        var anuncio = NovaMidia(academia.Id, 1_000);
        _context.Midias.AddRange(anuncio, NovaMidia(academia.Id, 2_500));
        var playlist = new Playlist { Nome = "Grade" };
        playlist.Itens.Add(new ItemPlaylist { Midia = anuncio, Ordem = 0 });
        var local = new Parceiro { Nome = "Shopping" };
        _context.Telas.AddRange(
            new Tela { Parceiro = local, Nome = "Recepção", Playlist = playlist },
            new Tela { Parceiro = local, Nome = "Musculação", Playlist = playlist });
        await _context.SaveChangesAsync();

        var porTelefone = await Listar(new GetClientesQuery { SearchTerm = "(92) 9999" });
        porTelefone.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            academia.Id,
            QuantidadeTelas = 2,
            ArmazenamentoUsadoBytes = 3_500L,
        });

        var bloqueados = await Listar(new GetClientesQuery { Situacao = SituacaoCliente.Bloqueado });
        bloqueados.Items.Should().ContainSingle().Which.Nome.Should().Be("Tapiocaria da Praia");

        var todos = await Listar(new GetClientesQuery());
        todos.Items.Select(p => p.Nome).Should().Equal("Academia Centro", "Tapiocaria da Praia");
        todos.Items.Single(p => p.Id == tapiocaria.Id).ArmazenamentoUsadoBytes.Should().Be(0);
    }

    [Fact]
    public async Task ObterPorId_InexistenteDeveDar404()
    {
        var acao = () => new GetClienteByIdQueryHandler(_context, _identity).Handle(new GetClienteByIdQuery(999), CancellationToken.None);

        await acao.Should().ThrowAsync<Ardalis.GuardClauses.NotFoundException>();
    }

    // ---------- Apoio ----------

    private Task<long> Criar(CriarClienteCommand comando) =>
        new CriarClienteCommandHandler(_context, _identity, _email, new Indoor.ConfiguracaoIndoorFake())
            .Handle(comando, CancellationToken.None);

    private Task<PaginatedList<Application.Business.Rede.Clientes.Dtos.ClienteDto>> Listar(GetClientesQuery query) =>
        new GetClientesQueryHandler(_context, _identity).Handle(query, CancellationToken.None);

    private static CriarClienteCommand Comando() => new()
    {
        Nome = "Academia Centro",
        NomeResponsavel = "Maria Souza",
        Email = "dono@academia.test",
    };

    private async Task<Cliente> CriarClienteAsync(string nome = "Academia Centro", string? telefone = null)
    {
        var cliente = new Cliente { Nome = nome, Email = "dono@academia.test", Telefone = telefone };
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    private static Midia NovaMidia(long clienteId, long tamanho) => new()
    {
        ClienteId = clienteId,
        Titulo = "Promoção",
        Tipo = TipoMidia.Video,
        ChaveArmazenamento = Guid.NewGuid().ToString("N"),
        TipoConteudo = "video/mp4",
        TamanhoBytes = tamanho,
        Hash = new string('a', 64),
    };

    private sealed class EmailFake : IEmailMessageService
    {
        public List<EmailMessageRequest> Enfileirados { get; } = [];
        public bool Falhar { get; set; }

        public Task<EmailMessage> EnqueueAsync(EmailMessageRequest request, CancellationToken cancellationToken = default)
        {
            if (Falhar)
            {
                throw new InvalidOperationException("SMTP indisponível.");
            }

            Enfileirados.Add(request);
            return Task.FromResult((EmailMessage)RuntimeHelpers.GetUninitializedObject(typeof(EmailMessage)));
        }
    }

    private sealed record UsuarioCriado(
        string NomeCompleto, string Usuario, string? Email, string Senha, bool TrocarSenha, string[] Grupos);

    /// <summary>Só o que a Rede usa; o resto da interface não é chamado.</summary>
    private sealed class IdentityFake : IIdentityService
    {
        public const long NovoUsuarioId = 77;

        public List<UsuarioCriado> Criados { get; } = [];
        public List<long> Inativados { get; } = [];
        public string? ErroAoCriar { get; set; }

        public Task<Result<long>> CreateUserAsync(
            string fullName, string userName, string? email, string temporaryPassword,
            bool mustChangePassword, IEnumerable<string> roles, CancellationToken cancellationToken = default)
        {
            if (ErroAoCriar is not null)
            {
                return Task.FromResult(Result<long>.Failure([ErroAoCriar]));
            }

            Criados.Add(new UsuarioCriado(fullName, userName, email, temporaryPassword, mustChangePassword, roles.ToArray()));
            return Task.FromResult(Result<long>.Success(NovoUsuarioId));
        }

        public Task<Result> DeactivateUserAsync(long userId, CancellationToken cancellationToken = default)
        {
            Inativados.Add(userId);
            return Task.FromResult(Result.Success());
        }

        public Task<IReadOnlyDictionary<long, string>> GetUserNamesAsync(IEnumerable<long> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<long, string>>(new Dictionary<long, string>());

        public Task<string?> GetUserNameAsync(long userId) => throw new NotImplementedException();
        public Task<bool> IsInRoleAsync(long userId, string role) => throw new NotImplementedException();
        public Task<bool> AuthorizeAsync(long userId, string policyName) => throw new NotImplementedException();
        public Task<UserDto?> GetUserByIdAsync(long userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result> UpdateUserAsync(long userId, string fullName, string? email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result> ActivateUserAsync(long userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result> ResetPasswordAsync(long userId, string temporaryPassword, bool mustChangePassword, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result> AssignRoleAsync(long userId, string roleName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Result> RemoveRoleAsync(long userId, string roleName, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasPermissionAsync(long userId, string permission, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<string>> GetUserPermissionsAsync(long userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> IsSuperUserAsync(long userId) => throw new NotImplementedException();
    }
}
