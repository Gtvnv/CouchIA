using CouchIA.Application.Common.Interfaces;
using CouchIA.Infrastructure.Criptografia;
using Microsoft.EntityFrameworkCore;

namespace CouchIA.Infrastructure.Persistencia;

public sealed class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICriptografiaCampo _criptografia;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICriptografiaCampo criptografia)
        : base(options)
    {
        _criptografia = criptografia;
    }

    public DbSet<SessaoEntidade> Sessoes => Set<SessaoEntidade>();
    public DbSet<MensagemEntidade> Mensagens => Set<MensagemEntidade>();
    public DbSet<RecomendacaoEntidade> Recomendacoes => Set<RecomendacaoEntidade>();
    public DbSet<FeedbackEntidade> Feedbacks => Set<FeedbackEntidade>();
    public DbSet<PlanoEntidade> Planos => Set<PlanoEntidade>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessaoEntidade>(e =>
        {
            e.ToTable("sessoes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
            e.Property(x => x.CriadaEm).HasColumnName("criada_em");
            e.Property(x => x.UltimaAtividadeEm).HasColumnName("ultima_atividade_em");
            e.Property(x => x.Entrevista).HasColumnName("entrevista").HasColumnType("jsonb");
            e.Property(x => x.Perfil).HasColumnName("perfil").HasCriptografiaCampo(_criptografia);
            e.HasIndex(x => x.UltimaAtividadeEm).HasDatabaseName("ix_sessoes_ultima_atividade_em");

            e.HasMany(x => x.Mensagens).WithOne().HasForeignKey(x => x.SessaoId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Recomendacoes).WithOne().HasForeignKey(x => x.SessaoId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Feedbacks).WithOne().HasForeignKey(x => x.SessaoId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Planos).WithOne().HasForeignKey(x => x.SessaoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MensagemEntidade>(e =>
        {
            e.ToTable("mensagens");
            e.HasKey(x => new { x.SessaoId, x.Ordem });
            e.Property(x => x.SessaoId).HasColumnName("sessao_id").HasMaxLength(64);
            e.Property(x => x.Ordem).HasColumnName("ordem");
            e.Property(x => x.ClientMessageId).HasColumnName("client_message_id");
            e.Property(x => x.Papel).HasColumnName("papel");
            e.Property(x => x.Texto).HasColumnName("texto").HasCriptografiaCampo(_criptografia);
            e.Property(x => x.CriadaEm).HasColumnName("criada_em");
            e.HasIndex(x => new { x.SessaoId, x.ClientMessageId }).IsUnique().HasDatabaseName("ux_mensagens_sessao_client_message_id");
        });

        modelBuilder.Entity<RecomendacaoEntidade>(e =>
        {
            e.ToTable("recomendacoes");
            e.HasKey(x => new { x.SessaoId, x.AreaId });
            e.Property(x => x.SessaoId).HasColumnName("sessao_id").HasMaxLength(64);
            e.Property(x => x.AreaId).HasColumnName("area_id").HasMaxLength(200);
            e.Property(x => x.Pontuacao).HasColumnName("pontuacao");
            e.Property(x => x.Justificativa).HasColumnName("justificativa").HasCriptografiaCampo(_criptografia);
            e.Property(x => x.OrdemOriginal).HasColumnName("ordem_original");
        });

        modelBuilder.Entity<FeedbackEntidade>(e =>
        {
            e.ToTable("feedbacks");
            e.HasKey(x => new { x.SessaoId, x.AreaId });
            e.Property(x => x.SessaoId).HasColumnName("sessao_id").HasMaxLength(64);
            e.Property(x => x.AreaId).HasColumnName("area_id").HasMaxLength(200);
            e.Property(x => x.Tipo).HasColumnName("tipo");
            e.Property(x => x.Motivo).HasColumnName("motivo")
#pragma warning disable CS8620 // o conversor não é invocado para null
                .HasCriptografiaCampo(_criptografia);
#pragma warning restore CS8620
            e.Property(x => x.Em).HasColumnName("em");
        });

        modelBuilder.Entity<PlanoEntidade>(e =>
        {
            e.ToTable("planos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.SessaoId).HasColumnName("sessao_id").HasMaxLength(64);
            e.Property(x => x.AreaId).HasColumnName("area_id").HasMaxLength(200);
            e.Property(x => x.Ordem).HasColumnName("ordem");
            e.Property(x => x.Conteudo).HasColumnName("conteudo").HasCriptografiaCampo(_criptografia);
            e.HasIndex(x => x.SessaoId).HasDatabaseName("ix_planos_sessao_id");
        });
    }

    public async Task<int> RemoverSessoesInativasAsync(DateTimeOffset limite, CancellationToken cancellationToken)
    {
        var corte = limite.ToUniversalTime();
        // O ON DELETE CASCADE do banco remove mensagens, recomendações, feedbacks e planos.
        return await Sessoes.Where(s => s.UltimaAtividadeEm < corte).ExecuteDeleteAsync(cancellationToken);
    }
}
