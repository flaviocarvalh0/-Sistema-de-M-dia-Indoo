using System.Reflection;
using Atlas.Application.Common.Interfaces;
using Atlas.Domain.Business.Indoor.Entities;
using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Domain.Common;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Atlas.Infrastructure.Data;

public class ApplicationDbContext
    : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        long,
        ApplicationUserClaim,
        IdentityUserRole<long>,
        IdentityUserLogin<long>,
        ApplicationRoleClaim,
        IdentityUserToken<long>>
    , IApplicationDbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Platform
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<SmtpConfiguration> SmtpConfigurations => Set<SmtpConfiguration>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<EmailMessageRecipient> EmailMessageRecipients => Set<EmailMessageRecipient>();
    public DbSet<EmailMessageAttachment> EmailMessageAttachments => Set<EmailMessageAttachment>();
    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();
    public DbSet<SystemIcon> SystemIcons => Set<SystemIcon>();
    public DbSet<EntityMetadata> EntityMetadata => Set<EntityMetadata>();
    public DbSet<SystemModule> SystemModules => Set<SystemModule>();
    public DbSet<SystemMenuItem> SystemMenuItems => Set<SystemMenuItem>();
    public DbSet<FileContent> FileContents => Set<FileContent>();
    public DbSet<EntityAttachment> EntityAttachments => Set<EntityAttachment>();

    // Business — Parceiros (inquilino)
    public DbSet<Parceiro> Parceiros => Set<Parceiro>();
    public DbSet<UsuarioParceiro> UsuariosParceiro => Set<UsuarioParceiro>();

    // Business — Mídia Indoor
    public DbSet<Tela> Telas => Set<Tela>();
    public DbSet<EstadoTela> EstadosTela => Set<EstadoTela>();
    public DbSet<Midia> Midias => Set<Midia>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<ItemPlaylist> ItensPlaylist => Set<ItemPlaylist>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUserClaim>().ToTable("user_claim", "platform");
        builder.Entity<ApplicationRoleClaim>().ToTable("role_claim", "platform");

        builder.Entity<ApplicationUser>().ToTable("user", "platform");
        builder.Entity<ApplicationRole>().ToTable("role", "platform");

        builder.Entity<IdentityUserRole<long>>().ToTable("user_role", "platform");
        builder.Entity<IdentityUserLogin<long>>().ToTable("user_login", "platform");
        builder.Entity<IdentityUserToken<long>>().ToTable("user_token", "platform");

        // Configuração Global de Controle de Concorrência Otimista (Optimistic Concurrency)
        // Mapeia a coluna sistêmica e oculta 'xmin' do PostgreSQL como Shadow Property ("Version")
        // para todas as entidades derivadas de BaseEntity. Isso garante detecção automática de 
        // edições concorrentes sem necessidade de migrations ou colunas adicionais no banco.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                builder.Entity(entityType.ClrType)
                    .Property<uint>("Version")
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .ValueGeneratedOnAddOrUpdate()
                    .IsRowVersion();
            }
        }

        builder.ApplyConfigurationsFromAssembly(
            Assembly.GetExecutingAssembly());
    }
}
