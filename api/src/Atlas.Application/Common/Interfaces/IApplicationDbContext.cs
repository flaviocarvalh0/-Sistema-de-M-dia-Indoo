using Atlas.Domain.Business.Conteudo.Entities;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Platform.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Atlas.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Platform
    DbSet<SmtpConfiguration> SmtpConfigurations { get; }
    DbSet<EmailMessage> EmailMessages { get; }
    DbSet<EmailMessageRecipient> EmailMessageRecipients { get; }
    DbSet<EmailMessageAttachment> EmailMessageAttachments { get; }
    DbSet<SystemParameter> SystemParameters { get; }
    DbSet<SystemIcon> SystemIcons { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<EntityMetadata> EntityMetadata { get; }
    DbSet<SystemModule> SystemModules { get; }
    DbSet<SystemMenuItem> SystemMenuItems { get; }
    DbSet<FileContent> FileContents { get; }
    DbSet<EntityAttachment> EntityAttachments { get; }

    // Business — Clientes (anunciantes, com acesso ao painel)
    DbSet<Cliente> Clientes { get; }
    DbSet<UsuarioCliente> UsuariosCliente { get; }

    // Business — Parceiros (locais das TVs, sem acesso)
    DbSet<Parceiro> Parceiros { get; }

    // Business — Mídia Indoor
    DbSet<Tela> Telas { get; }
    DbSet<EstadoTela> EstadosTela { get; }

    // Prova de veiculação (ADR-023)
    DbSet<RegistroExibicao> RegistrosExibicao { get; }
    DbSet<LeituraQr> LeiturasQr { get; }

    // Teto de armazenamento (ADR-025)
    DbSet<ReservaEnvio> ReservasEnvio { get; }
    DbSet<InventarioArmazenamento> InventariosArmazenamento { get; }
    DbSet<Midia> Midias { get; }
    DbSet<Playlist> Playlists { get; }
    DbSet<ItemPlaylist> ItensPlaylist { get; }

    // Business — Conteúdo externo (notícias e clima)
    DbSet<FonteNoticia> FontesNoticia { get; }
    DbSet<Noticia> Noticias { get; }
    DbSet<PrevisaoClima> PrevisoesClima { get; }
    DbSet<Cotacao> Cotacoes { get; }

    DbSet<VersaoApp> VersoesApp { get; }

    /// <summary>
    /// Retorna a entrada de rastreamento do ChangeTracker para a entidade informada.
    /// Utilizado para manipular estados, propriedades de sombra (ex: xmin/Version) e concorrência.
    /// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Fachada do Entity Framework para operações relacionadas ao banco de dados,
    /// como gerenciamento de transações.
    /// </summary>
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
