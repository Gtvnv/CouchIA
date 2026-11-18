using CouchIA.Domain;
using CouchIA.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CouchIA.Infrastructure.Tests;

/// <summary>Falha de gravação com uma nova tentativa (Req. 7.3), contra PostgreSQL real.</summary>
public class FluxosDeFalhaTests : IClassFixture<PostgresFixture>
{
    private static readonly SessaoOptions Opcoes = new(TimeSpan.FromDays(30), TimeSpan.FromMinutes(60));
    private static readonly DateTimeOffset Agora = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly PostgresFixture _pg;

    public FluxosDeFalhaTests(PostgresFixture pg) => _pg = pg;

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    /// <summary>Faz as primeiras N gravações falharem com <see cref="DbUpdateException"/>.</summary>
    private sealed class FalhaNaGravacao : SaveChangesInterceptor
    {
        private int _restantes;
        public int Tentativas { get; private set; }
        public FalhaNaGravacao(int falhas) => _restantes = falhas;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Tentativas++;
            if (_restantes > 0)
            {
                _restantes--;
                throw new DbUpdateException("falha simulada de gravação");
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private ApplicationDbContext ContextoComFalhas(FalhaNaGravacao interceptor)
    {
        using var normal = _pg.NovoContexto();
        var opcoes = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(normal.Database.GetConnectionString())
            .AddInterceptors(interceptor)
            .Options;
        return new ApplicationDbContext(opcoes, _pg.Criptografia);
    }

    private static Sessao NovaSessao(string objetivo) => new(
        SessaoId.Gerar(), Agora, Array.Empty<MensagemSessao>(),
        new PerfilUsuario(new[] { "dados" }, null, null, null, 10, objetivo, new HashSet<HabilidadeId>()),
        new Entrevista(EstadoEntrevista.EmAndamento,
            Enum.GetValues<CategoriaEntrevista>().ToDictionary(c => c, _ => EstadoCategoria.Pendente),
            new Dictionary<CategoriaEntrevista, int>()),
        Array.Empty<RecomendacaoArea>(), Array.Empty<PlanoDesenvolvimento>(), Array.Empty<FeedbackRecomendacao>());

    private async Task<Sessao?> LerAsync(SessaoId id)
    {
        await using var db = _pg.NovoContexto();
        return await new SessaoRepository(db, Opcoes, new RelogioFixo()).ObterAsync(id, default);
    }

    [Fact(Timeout = 600000)]
    public async Task Uma_falha_de_gravacao_e_repetida_uma_vez_e_a_sessao_e_salva()
    {
        var interceptor = new FalhaNaGravacao(falhas: 1);
        var sessao = NovaSessao("objetivo");

        await using (var db = ContextoComFalhas(interceptor))
        {
            await new SessaoRepository(db, Opcoes, new RelogioFixo()).SalvarAsync(sessao, default);
        }

        Assert.Equal(2, interceptor.Tentativas);
        var lida = await LerAsync(sessao.Id);
        Assert.NotNull(lida);
        Assert.Equal("objetivo", lida!.Perfil.Objetivos);
    }

    [Fact(Timeout = 600000)]
    public async Task Erro_na_segunda_tentativa_e_propagado_e_o_estado_anterior_permanece()
    {
        var original = NovaSessao("original");
        await using (var db = _pg.NovoContexto())
        {
            await new SessaoRepository(db, Opcoes, new RelogioFixo()).SalvarAsync(original, default);
        }

        var interceptor = new FalhaNaGravacao(falhas: 2);
        var alterada = original with { Perfil = original.Perfil with { Objetivos = "alterado" } };

        await using (var db = ContextoComFalhas(interceptor))
        {
            await Assert.ThrowsAsync<DbUpdateException>(
                () => new SessaoRepository(db, Opcoes, new RelogioFixo()).SalvarAsync(alterada, default));
        }

        Assert.Equal(2, interceptor.Tentativas);
        var lida = await LerAsync(original.Id);
        Assert.Equal("original", lida!.Perfil.Objetivos);
    }
}
