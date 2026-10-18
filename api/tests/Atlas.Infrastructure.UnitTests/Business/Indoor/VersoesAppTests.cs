using System.Security.Cryptography;
using Atlas.Application.Business.Rede.AppTv;
using Atlas.Application.Common.Exceptions;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Infrastructure.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Infrastructure.UnitTests.Business.Indoor;

/// <summary>Envio e distribuição das versões do app das TVs (ADR-021).</summary>
public class VersoesAppTests : IDisposable
{
    private readonly ApplicationDbContext _context = new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private readonly ArmazenamentoFake _armazenamento = new();

    public void Dispose() => _context.Dispose();

    private static byte[] Apk(int tamanho = 4000)
    {
        var dados = new byte[tamanho];
        Random.Shared.NextBytes(dados);
        dados[0] = 0x50; dados[1] = 0x4B; dados[2] = 0x03; dados[3] = 0x04;
        return dados;
    }

    private string Enviar(byte[] dados)
    {
        var chave = ArquivoApk.NovaChave();
        _armazenamento.Arquivos[chave] = dados;
        return chave;
    }

    private Task<long> Criar(CriarVersaoAppCommand comando) =>
        new CriarVersaoAppCommandHandler(_context, _armazenamento, NullLogger<CriarVersaoAppCommandHandler>.Instance)
            .Handle(comando, CancellationToken.None);

    [Fact]
    public async Task Criar_ConfereOApkGuardaOHashENasceSuspensa()
    {
        var apk = Apk();
        var chave = Enviar(apk);
        var hash = Convert.ToHexStringLower(SHA256.HashData(apk));

        var id = await Criar(new CriarVersaoAppCommand { Codigo = 5, Nome = " 1.4.0 ", Chave = chave, Hash = hash.ToUpperInvariant() });

        var versao = await _context.VersoesApp.SingleAsync(v => v.Id == id);
        versao.Hash.Should().Be(hash);
        versao.TamanhoBytes.Should().Be(apk.Length);
        versao.Nome.Should().Be("1.4.0");
        versao.Distribuicao.Should().Be(DistribuicaoVersao.Suspensa, "ninguém recebe antes de a operadora liberar");
    }

    [Fact]
    public async Task Criar_ArquivoQueNaoEApkOuHashDiferente_RecusaEApaga()
    {
        var falso = Enviar([0x89, 0x50, 0x4E, 0x47, 1, 2, 3]);
        var naoApk = () => Criar(new CriarVersaoAppCommand { Codigo = 5, Nome = "1.4.0", Chave = falso });
        (await naoApk.Should().ThrowAsync<ValidationException>()).Which.Errors["Chave"].Should().ContainMatch("*não é um APK*");
        _armazenamento.Removidos.Should().Contain(falso);

        var chave = Enviar(Apk());
        var outroHash = () => Criar(new CriarVersaoAppCommand { Codigo = 5, Nome = "1.4.0", Chave = chave, Hash = new string('a', 64) });
        (await outroHash.Should().ThrowAsync<ValidationException>()).Which.Errors["Chave"].Should().ContainMatch("*hash*");
    }

    [Fact]
    public async Task Criar_CodigoRepetidoERecusado()
    {
        await Criar(new CriarVersaoAppCommand { Codigo = 5, Nome = "1.4.0", Chave = Enviar(Apk()) });

        var repetido = () => Criar(new CriarVersaoAppCommand { Codigo = 5, Nome = "1.4.1", Chave = Enviar(Apk()) });

        (await repetido.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Codigo");
    }

    [Fact]
    public async Task Excluir_SoSuspensa()
    {
        var id = await Criar(new CriarVersaoAppCommand { Codigo = 5, Nome = "1.4.0", Chave = Enviar(Apk()) });
        await new DefinirDistribuicaoVersaoCommandHandler(_context)
            .Handle(new DefinirDistribuicaoVersaoCommand(id, DistribuicaoVersao.Todas, 0), CancellationToken.None);

        var excluir = () => new ExcluirVersaoAppCommandHandler(_context, _armazenamento, NullLogger<ExcluirVersaoAppCommandHandler>.Instance)
            .Handle(new ExcluirVersaoAppCommand(id), CancellationToken.None);
        await excluir.Should().ThrowAsync<ValidationException>();

        await new DefinirDistribuicaoVersaoCommandHandler(_context)
            .Handle(new DefinirDistribuicaoVersaoCommand(id, DistribuicaoVersao.Suspensa, 0), CancellationToken.None);
        await excluir();
        (await _context.VersoesApp.AnyAsync()).Should().BeFalse();
    }

    [Theory]
    [InlineData("app/0123456789abcdef0123456789abcdef.apk", true)]
    [InlineData("app/../midias/x.apk", false)]
    [InlineData("clientes/1/midias/0123456789abcdef0123456789abcdef.mp4", false)]
    public void ChaveDoApk(string chave, bool valida) => ArquivoApk.ChaveValida(chave).Should().Be(valida);
}
