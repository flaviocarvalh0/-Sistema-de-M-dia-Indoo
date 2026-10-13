using Atlas.Domain.Platform.Entities;

namespace Atlas.Application.Platform.Smtp.Commands.UpsertSmtpConfiguration;

[Authorize(Policy = PermissionKeys.Settings.ManageSmtp)]
public record UpsertSmtpConfigurationCommand : IRequest<Unit>
{
    public string Host { get; init; } = null!;
    public int Port { get; init; }
    public bool EnableSsl { get; init; }

    [LogSensitive]
    public string UserName { get; init; } = null!;

    [LogSensitive]
    public string Password { get; init; } = null!;

    [LogSensitive]
    public string SenderEmail { get; init; } = null!;
    public string? SenderName { get; init; }
}

public class UpsertSmtpConfigurationCommandHandler : IRequestHandler<UpsertSmtpConfigurationCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UpsertSmtpConfigurationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UpsertSmtpConfigurationCommand request, CancellationToken cancellationToken)
    {
        // Tenta obter o único registro existente
        var config = await _context.SmtpConfigurations
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null)
        {
            // Cenário 1: Primeiro acesso do painel administrativo (Cria o registro)
            config = new SmtpConfiguration
            {
                Host = request.Host,
                Port = request.Port,
                EnableSsl = request.EnableSsl,
                UserName = request.UserName,
                Password = request.Password, 
                SenderEmail = request.SenderEmail,
                SenderName = request.SenderName
            };

            _context.SmtpConfigurations.Add(config);
        }
        else
        {
            // Cenário 2: Atualização cadastral (Garante o Single Record alterando a mesma linha)
            config.Host = request.Host;
            config.Port = request.Port;
            config.EnableSsl = request.EnableSsl;
            config.UserName = request.UserName;
            config.SenderEmail = request.SenderEmail;
            config.SenderName = request.SenderName;

            // Só atualiza a senha se o usuário digitou uma nova senha na tela
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                config.Password = request.Password;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
