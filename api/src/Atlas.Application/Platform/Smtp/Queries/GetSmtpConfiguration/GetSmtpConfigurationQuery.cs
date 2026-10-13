namespace Atlas.Application.Platform.Smtp.Queries.GetSmtpConfiguration;

[Authorize(Policy = PermissionKeys.Settings.ViewSmtp)]
public record GetSmtpConfigurationQuery : IRequest<SmtpConfigurationDto?>;

public class GetSmtpConfigurationQueryHandler : IRequestHandler<GetSmtpConfigurationQuery, SmtpConfigurationDto?>
{
    private readonly IApplicationDbContext _context;

    public GetSmtpConfigurationQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SmtpConfigurationDto?> Handle(GetSmtpConfigurationQuery request, CancellationToken cancellationToken)
    {
        // Busca o único registro existente no banco de dados
        var config = await _context.SmtpConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        if (config == null) return null;

        return new SmtpConfigurationDto
        {
            Host = config.Host,
            Port = config.Port,
            EnableSsl = config.EnableSsl,
            UserName = config.UserName,
            SenderEmail = config.SenderEmail,
            SenderName = config.SenderName
        };
    }
}
