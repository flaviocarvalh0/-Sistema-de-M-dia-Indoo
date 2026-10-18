using Atlas.Domain.Business.Clientes.Entities;
using Atlas.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Clientes;

public class UsuarioClienteConfiguration : IEntityTypeConfiguration<UsuarioCliente>
{
    public void Configure(EntityTypeBuilder<UsuarioCliente> builder)
    {
        builder.ToTable("usuario_cliente", "business");

        // Um usuário pertence a um único cliente. O índice único também é o
        // que torna barata a resolução do cliente atual.
        builder.HasIndex(v => v.UsuarioId)
            .IsUnique();

        builder.HasOne(v => v.Cliente)
            .WithMany()
            .HasForeignKey(v => v.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // O domínio não conhece ApplicationUser (fica na Infrastructure); a FK
        // existe só no mapeamento.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
