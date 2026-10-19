using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VibeZone.Modules.Catalog.Domain;

namespace VibeZone.Modules.Catalog.Infrastructure;

internal sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("genres", CatalogModule.Schema);
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).ValueGeneratedNever();
        builder.Property(g => g.Slug).HasMaxLength(Genre.MaxSlugLength).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(Genre.MaxNameLength).IsRequired();
        builder.HasIndex(g => g.Slug).IsUnique();

        // Reference data shipped to every environment (see data-model.md "Seed data").
        builder.HasData(GenreReferenceData.All);
    }
}
