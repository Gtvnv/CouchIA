using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Domain;
using CouchIA.Infrastructure.Criptografia;
using CouchIA.Infrastructure.Persistencia;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace CouchIA.Infrastructure.Tests;

/// <summary>Um PostgreSQL real (Testcontainers) com as migrations aplicadas, compartilhado pelos testes da classe.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public ICriptografiaCampo Criptografia { get; } = new CriptografiaCampoAesGcm(RandomNumberGenerator.GetBytes(32));

    public ApplicationDbContext NovoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_container.GetConnectionString()).Options, Criptografia);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = NovoContexto();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

public class SessaoPersistenciaTests : IClassFixture<PostgresFixture>
{
    private static readonly SessaoOptions Opcoes = new(TimeSpan.FromDays(30), TimeSpan.FromMinutes(60));

    private readonly PostgresFixture _pg;

    public SessaoPersistenciaTests(PostgresFixture pg) => _pg = pg;

    private sealed class RelogioFalso : TimeProvider
    {
        public DateTimeOffset Agora { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    // Cada operação usa um DbContext novo, para provar que o dado veio do banco e não do change tracker.
    private async Task SalvarAsync(Sessao s, TimeProvider relogio)
    {
        await using var db = _pg.NovoContexto();
        await new SessaoRepository(db, Opcoes, relogio).SalvarAsync(s, default);
    }

    private async Task<Sessao?> ObterAsync(SessaoId id, TimeProvider relogio)
    {
        await using var db = _pg.NovoContexto();
        return await new SessaoRepository(db, Opcoes, relogio).ObterAsync(id, default);
    }

    private async Task ExcluirAsync(SessaoId id, TimeProvider relogio)
    {
        await using var db = _pg.NovoContexto();
        await new SessaoRepository(db, Opcoes, relogio).ExcluirAsync(id, default);
    }

    private async Task<int> ContarLinhasAsync(SessaoId id)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id.Valor))).ToLowerInvariant();
        await using var db = _pg.NovoContexto();
        return await db.Sessoes.CountAsync(x => x.Id == hash)
             + await db.Mensagens.CountAsync(x => x.SessaoId == hash)
             + await db.Recomendacoes.CountAsync(x => x.SessaoId == hash)
             + await db.Feedbacks.CountAsync(x => x.SessaoId == hash)
             + await db.Planos.CountAsync(x => x.SessaoId == hash);
    }

    // ---------- geração ----------

    private static string Texto(Random r, int min, int max)
    {
        string[] pool = { "a", "Z", "ç", "ã", "é", " ", "-", "9", "\n", "\"", "\\", "漢", "😀", "'", "<", "%" };
        var n = r.Next(min, max + 1);
        var sb = new StringBuilder();
        for (var i = 0; i < n; i++) sb.Append(pool[r.Next(pool.Length)]);
        return sb.ToString();
    }

    private static DateTimeOffset Data(Random r) =>
        new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromMilliseconds(r.NextInt64(0, 100_000_000_000));

    /// <summary>Sessao válida construída de forma determinística a partir da semente (gerador FsCheck de inteiros).</summary>
    private static Sessao CriarSessao(int semente, DateTimeOffset agora)
    {
        var r = new Random(semente);
        var criada = new DateTimeOffset(agora.UtcTicks / TimeSpan.TicksPerMillisecond * TimeSpan.TicksPerMillisecond, TimeSpan.Zero);

        var mensagens = Enumerable.Range(0, r.Next(0, 12))
            .Select(i => new MensagemSessao(Guid.NewGuid(), i, (PapelMensagem)r.Next(2), Texto(r, 0, 80), Data(r)))
            .ToList();

        string? Opc(int max) => r.Next(3) == 0 ? null : Texto(r, 0, max);

        var perfil = new PerfilUsuario(
            Enumerable.Range(0, r.Next(0, 5)).Select(_ => Texto(r, 1, 30)).ToList(),
            Opc(60), Opc(60), Opc(60),
            r.Next(2) == 0 ? null : r.Next(1, 169),
            Opc(60),
            Enumerable.Range(0, r.Next(0, 6)).Select(i => new HabilidadeId($"hab-{i}-{r.Next(100)}")).ToHashSet());

        var categorias = Enum.GetValues<CategoriaEntrevista>()
            .ToDictionary(c => c, _ => (EstadoCategoria)r.Next(3));
        var reform = Enum.GetValues<CategoriaEntrevista>().Where(_ => r.Next(2) == 0)
            .ToDictionary(c => c, _ => r.Next(0, 4));
        var entrevista = new Entrevista((EstadoEntrevista)r.Next(4), categorias, reform);

        var recomendacoes = Enumerable.Range(0, r.Next(0, 6))
            .Select(i => new RecomendacaoArea(new AreaId($"area-{i}"), r.Next(0, 101), Texto(r, 1, 100)))
            .OrderBy(_ => r.Next()).ToList();

        var feedbacks = Enumerable.Range(0, r.Next(0, 5))
            .Select(i => new FeedbackRecomendacao(new AreaId($"area-{i}"), (TipoFeedback)r.Next(2),
                r.Next(2) == 0 ? null : Texto(r, 0, 100), criada.AddSeconds(i + 1)))
            .ToList();

        var planos = Enumerable.Range(0, r.Next(0, 4)).Select(i =>
        {
            var etapas = Enumerable.Range(1, r.Next(1, 4)).Select(n => new EtapaPlano(
                n,
                Enumerable.Range(0, r.Next(1, 4)).Select(k => new HabilidadeId($"h{n}-{k}")).ToList(),
                r.Next(1, 13),
                Enumerable.Range(0, r.Next(1, 3)).Select(k =>
                    new RecursoEstudo(Texto(r, 1, 30), new Uri($"https://exemplo.com/{r.Next(1000)}/{k}"))).ToList())).ToList();
            return new PlanoDesenvolvimento(Guid.NewGuid(), new AreaId($"area-{i}"), r.Next(1, 169), etapas,
                etapas.Sum(e => e.DuracaoSemanas));
        }).ToList();

        return new Sessao(SessaoId.Gerar(), criada, mensagens, perfil, entrevista, recomendacoes, planos, feedbacks);
    }

    /// <summary>Forma canônica para comparar estruturalmente (records com listas não têm igualdade de valor).</summary>
    private static string Canonico(Sessao s) => JsonSerializer.Serialize(new
    {
        Id = s.Id.Valor,
        Criada = s.CriadaEm.UtcTicks,
        Historico = s.Historico.Select(m => new { m.ClientMessageId, m.Ordem, Papel = (int)m.Papel, m.Texto, Em = m.CriadaEm.UtcTicks }),
        Perfil = new
        {
            s.Perfil.Interesses,
            s.Perfil.ExperienciaPrevia,
            s.Perfil.Formacao,
            s.Perfil.EstiloDeTrabalho,
            s.Perfil.HorasEstudoPorSemana,
            s.Perfil.Objetivos,
            Habilidades = s.Perfil.HabilidadesDominadas.Select(h => h.Valor).OrderBy(x => x, StringComparer.Ordinal)
        },
        Entrevista = new
        {
            Estado = (int)s.Entrevista.Estado,
            Categorias = s.Entrevista.Categorias.OrderBy(k => (int)k.Key).Select(k => new { C = (int)k.Key, V = (int)k.Value }),
            Reformulacoes = s.Entrevista.Reformulacoes.OrderBy(k => (int)k.Key).Select(k => new { C = (int)k.Key, V = k.Value })
        },
        Recomendacoes = s.Recomendacoes.Select(r => new { Area = r.Area.Valor, r.Pontuacao, r.Justificativa }),
        Planos = s.Planos.Select(p => new
        {
            p.Id,
            Area = p.Area.Valor,
            p.HorasPorSemana,
            p.DuracaoTotalSemanas,
            Etapas = p.Etapas.Select(e => new
            {
                e.Numero,
                Habilidades = e.Habilidades.Select(h => h.Valor),
                e.DuracaoSemanas,
                Recursos = e.Recursos.Select(x => new { x.Titulo, Url = x.Url.OriginalString })
            })
        }),
        Feedbacks = s.Feedbacks.Select(f => new { Area = f.Area.Valor, Tipo = (int)f.Tipo, f.Motivo, Em = f.Em.UtcTicks })
    });

    private static Arbitrary<int> Sementes() => Gen.Choose(0, int.MaxValue).ToArbitrary();

    // Feature: ia-mentor-carreira, Property 19: Round-trip de persistência da Sessao
    // **Validates: Requirements 7.1, 7.2, 7.3**
    [Property(MaxTest = 100)]
    public Property Gravar_e_reler_preserva_a_sessao()
    {
        return Prop.ForAll(Sementes(), semente =>
        {
            var relogio = new RelogioFalso();
            var original = CriarSessao(semente, relogio.Agora);
            SalvarAsync(original, relogio).GetAwaiter().GetResult();
            var lida = ObterAsync(original.Id, relogio).GetAwaiter().GetResult();
            return lida is not null && Canonico(original) == Canonico(lida);
        });
    }

    // Feature: ia-mentor-carreira, Property 19: Round-trip de persistência da Sessao
    // **Validates: Requirements 7.3, 7.4**
    [Property(MaxTest = 100)]
    public Property Regravar_substitui_o_conteudo_anterior()
    {
        return Prop.ForAll(Sementes(), Sementes(), (a, b) =>
        {
            var relogio = new RelogioFalso();
            var primeira = CriarSessao(a, relogio.Agora);
            var segunda = CriarSessao(b, relogio.Agora) with { Id = primeira.Id };
            SalvarAsync(primeira, relogio).GetAwaiter().GetResult();
            SalvarAsync(segunda, relogio).GetAwaiter().GetResult();
            var lida = ObterAsync(primeira.Id, relogio).GetAwaiter().GetResult();
            return lida is not null && Canonico(segunda) == Canonico(lida);
        });
    }

    // Feature: ia-mentor-carreira, Property 19: Round-trip de persistência da Sessao
    // **Validates: Requirements 7.4, 7.5**
    [Property(MaxTest = 100)]
    public Property Excluir_remove_todos_os_dados()
    {
        return Prop.ForAll(Sementes(), semente =>
        {
            var relogio = new RelogioFalso();
            var s = CriarSessao(semente, relogio.Agora);
            SalvarAsync(s, relogio).GetAwaiter().GetResult();
            var antes = ContarLinhasAsync(s.Id).GetAwaiter().GetResult();
            ExcluirAsync(s.Id, relogio).GetAwaiter().GetResult();
            var depois = ContarLinhasAsync(s.Id).GetAwaiter().GetResult();
            var lida = ObterAsync(s.Id, relogio).GetAwaiter().GetResult();
            return antes >= 1 && depois == 0 && lida is null;
        });
    }

    // Feature: ia-mentor-carreira, Property 19: Round-trip de persistência da Sessao
    // **Validates: Requirements 7.5**
    [Property(MaxTest = 100)]
    public Property Sessao_expirada_nao_e_encontrada()
    {
        return Prop.ForAll(Sementes(), Gen.Choose(1, 365).ToArbitrary(), (semente, diasExtras) =>
        {
            var relogio = new RelogioFalso();
            var s = CriarSessao(semente, relogio.Agora);
            SalvarAsync(s, relogio).GetAwaiter().GetResult();

            // Dentro do prazo: ainda existe.
            relogio.Agora = relogio.Agora.AddDays(29);
            var viva = ObterAsync(s.Id, relogio).GetAwaiter().GetResult();

            // Além do prazo de inatividade (contado da última atividade): não existe e foi removida.
            relogio.Agora = relogio.Agora.AddDays(30).AddDays(diasExtras);
            var expirada = ObterAsync(s.Id, relogio).GetAwaiter().GetResult();
            var restantes = ContarLinhasAsync(s.Id).GetAwaiter().GetResult();

            return viva is not null && expirada is null && restantes == 0;
        });
    }
}
