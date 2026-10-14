using Atlas.Application.Business.Common;
using Atlas.Application.Business.Rede.Common;
using Atlas.Application.Platform.EmailMessages.Common;
using Atlas.Domain.Business.Constants;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Business.Parceiros.Enums;

namespace Atlas.Application.Business.Rede.Parceiros.Commands.CriarParceiro;

/// <summary>
/// Cadastro de parceiro pela operadora: cria o usuário do responsável (com
/// senha temporária e troca obrigatória no primeiro acesso), o parceiro e o
/// vínculo entre eles, e enfileira o e-mail de boas-vindas com o acesso.
/// </summary>
[Authorize(Policy = BusinessPermissionKeys.Parceiros.Create)]
public record CriarParceiroCommand : IRequest<long>
{
    public string Nome { get; init; } = string.Empty;
    public string NomeResponsavel { get; init; } = string.Empty;

    /// <summary>E-mail de contato e de acesso ao painel (vira o usuário do responsável).</summary>
    [LogSensitive]
    public string Email { get; init; } = string.Empty;

    public string? Telefone { get; init; }
    public string? Documento { get; init; }
    public string? Cidade { get; init; }
    public string FusoHorario { get; init; } = FusosHorarios.Padrao;
    public int CotaArmazenamentoMb { get; init; } = Parceiro.CotaPadraoMb;

    /// <summary>Plano: mídias ativas ao mesmo tempo.</summary>
    public int LimiteMidiasAtivas { get; init; } = Parceiro.LimiteMidiasAtivasPadrao;
    public string? ObservacaoInterna { get; init; }
}

public class CriarParceiroCommandHandler(
    IApplicationDbContext context,
    IIdentityService identityService,
    IEmailMessageService emailMessageService,
    IConfiguracaoIndoor configuracao)
    : IRequestHandler<CriarParceiroCommand, long>
{
    public async Task<long> Handle(CriarParceiroCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var nomeResponsavel = request.NomeResponsavel.Trim();
        var senhaTemporaria = GeradorSenhaTemporaria.Gerar();

        // O Identity também recusa o e-mail repetido, mas com mensagem técnica
        // em inglês; o caso comum (outro parceiro) ganha uma mensagem clara.
        if (await context.Parceiros.AnyAsync(p => p.Email == email, cancellationToken))
        {
            throw new ValidationException(nameof(request.Email), ["Já existe um parceiro com este e-mail."]);
        }

        // CreateUserAsync abre a própria transação e grava o contexto inteiro:
        // nada é alterado no contexto antes dele.
        var usuario = await identityService.CreateUserAsync(
            nomeResponsavel,
            email,
            email,
            senhaTemporaria,
            mustChangePassword: true,
            [BusinessRoles.ProprietarioParceiro],
            cancellationToken);

        if (!usuario.Succeeded)
        {
            throw new ValidationException(nameof(request.Email), usuario.Errors);
        }

        try
        {
            // Duas gravações (o e-mail referencia o id do parceiro) numa transação só.
            await using var transacao = await context.Database.BeginTransactionAsync(cancellationToken);

            var parceiro = new Parceiro
            {
                Nome = request.Nome.Trim(),
                NomeResponsavel = nomeResponsavel,
                Email = email,
                Telefone = Limpar(DocumentosBrasil.SomenteDigitos(request.Telefone)),
                Documento = DocumentosBrasil.NormalizarDocumento(request.Documento),
                Cidade = Limpar(request.Cidade),
                FusoHorario = request.FusoHorario.Trim(),
                CotaArmazenamentoMb = request.CotaArmazenamentoMb,
                LimiteMidiasAtivas = request.LimiteMidiasAtivas,
                ObservacaoInterna = Limpar(request.ObservacaoInterna),
            };

            context.Parceiros.Add(parceiro);
            context.UsuariosParceiro.Add(new UsuarioParceiro
            {
                UsuarioId = usuario.Value,
                Parceiro = parceiro,
                Papel = PapelParceiro.Proprietario,
            });

            await context.SaveChangesAsync(cancellationToken);

            await emailMessageService.EnqueueAsync(EmailBoasVindas(parceiro, senhaTemporaria), cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            await transacao.CommitAsync(cancellationToken);

            return parceiro.Id;
        }
        catch
        {
            // Sem como excluir usuário pela plataforma: a compensação é inativar.
            // O e-mail fica reservado; a operadora reativa o usuário no Studio se precisar.
            await identityService.DeactivateUserAsync(usuario.Value, CancellationToken.None);
            throw;
        }
    }

    private EmailMessageRequest EmailBoasVindas(Parceiro parceiro, string senhaTemporaria)
    {
        var urlPainel = configuracao.UrlPainel.TrimEnd('/');

        return new EmailMessageRequest
        {
            Subject = $"Bem-vindo à Mídia Indoor — {parceiro.Nome}",
            HtmlBody = ModeloEmail.Montar(
                previa: $"O {parceiro.Nome} já está na Mídia Indoor. Seu acesso ao painel está aqui.",
                titulo: "Seu painel está pronto!",
                motivo: $"Você recebeu este e-mail porque o {parceiro.Nome} foi cadastrado na Mídia Indoor.",
                ModeloEmail.Paragrafo($"Olá, {parceiro.NomeResponsavel}!"),
                ModeloEmail.ParagrafoHtml($"O <strong>{ModeloEmail.E(parceiro.Nome)}</strong> já está na Mídia Indoor. Entre no painel com o acesso abaixo:"),
                ModeloEmail.Destaque(("Usuário", parceiro.Email), ("Senha temporária", senhaTemporaria)),
                ModeloEmail.Botao("Entrar no painel", $"{urlPainel}/login"),
                ModeloEmail.Paragrafo("No primeiro acesso você cria a sua senha. Depois é só enviar os vídeos, montar a playlist e parear as TVs."),
                ModeloEmail.Nota("A senha temporária só vale até o primeiro acesso. Não repasse este e-mail.")),
            Recipients =
            [
                new EmailRecipientRequest
                {
                    Name = parceiro.NomeResponsavel,
                    Email = parceiro.Email,
                    Type = EmailRecipientType.To,
                },
            ],
            RelatedEntityType = nameof(Parceiro),
            RelatedEntityId = parceiro.Id.ToString(),
        };
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
