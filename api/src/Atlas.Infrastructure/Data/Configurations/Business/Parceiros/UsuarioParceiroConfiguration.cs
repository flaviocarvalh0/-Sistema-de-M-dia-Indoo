using Atlas.Domain.Business.Parceiros.Entities;
using Atlas.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Infrastructure.Data.Configurations.Business.Parceiros;

public class UsuarioParceiroConfiguration : IEntityTypeConfiguration<UsuarioParceiro>
{
    public void Configure(EntityTypeBuilder<UsuarioParceiro> builder)
    {
        builder.ToTable("usuario_parceiro", "business");

        // Um usuário pertence a um único parceiro. O índice único também é o
        // que torna barata a resolução do parceiro atual.
        builder.HasIndex(v => v.UsuarioId)
            .IsUnique();

        builder.HasOne(v => v.Parceiro)
            .WithMany()
            .HasForeignKey(v => v.ParceiroId)
            .OnDelete(DeleteBehavior.Restrict);

        // O domínio não conhece ApplicationUser (fica na Infrastructure); a FK
        // existe só no mapeamento.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
