using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Common.Localization;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Planos.Commands;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Localization;

namespace CouchIA.Application.Tests;

public class PlanoGerarValidatorTests
{
    private static readonly AreaId Area = new("area-teste");

    private sealed class LocalizerFake : IStringLocalizer<MentorLocalization>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private sealed class RepositorioFake : ISessaoRepository
    {
        public Sessao Sessao { get; private set; }
        public int Salvamentos { get; private set; }

        public RepositorioFake(Sessao sessao) => Sessao = sessao;

        public Task<Sessao?> ObterAsync(SessaoId id, CancellationToken ct) =>
            Task.FromResult<Sessao?>(id == Sessao.Id ? Sessao : null);

        public Task SalvarAsync(Sessao sessao, CancellationToken ct)
        {
            Salvamentos++;
            Sessao = sessao;
            return Task.CompletedTask;
        }

        public Task ExcluirAsync(SessaoId id, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class CatalogoFake : ICatalogoProvider
    {
        public CatalogoFake(Catalogo? ativo) => Ativo = ativo;
        public Catalogo? Ativo { get; }
        public ResultadoCarga Carregar(string json) => throw new NotSupportedException();
    }

    private static Catalogo CriarCatalogo()
    {
        var id = new HabilidadeId("h0");
        var habilidade = new Habilidade(id, "H0", "Descrição.", 10,
            new[] { new RecursoEstudo("R", new Uri("https://exemplo.com/0")) }, new List<HabilidadeId>());
        var area = new AreaAtuacao(Area, "Área", "Descrição",
            new[] { new HabilidadeNaArea(id, PrioridadeHabilidade.Essencial) });
        return new Catalogo(new[] { area }, new[] { habilidade });
    }

    private static Sessao CriarSessao(int? horasPerfil) => new(
        SessaoId.Gerar(),
        DateTimeOffset.UtcNow,
        new List<MensagemSessao>(),
        new PerfilUsuario(new List<string>(), null, null, null, horasPerfil, null, new HashSet<HabilidadeId>()),
        new Entrevista(EstadoEntrevista.Concluida,
            new Dictionary<CategoriaEntrevista, EstadoCategoria>(),
            new Dictionary<CategoriaEntrevista, int>()),
        new List<RecomendacaoArea>(),
        new List<PlanoDesenvolvimento>(),
        new List<FeedbackRecomendacao>());

    private static bool Valido(int horas) => horas is >= 1 and <= 168;

    // Inteiros arbitrários, com viés para as fronteiras 1 e 168.
    private static Gen<int> GeradorInteiros() => Gen.OneOf(
        Gen.Choose(-1000, 1000),
        Gen.Choose(-5, 5),
        Gen.Choose(160, 176),
        Gen.Elements(int.MinValue, int.MaxValue, 0, 1, 168, 169));

    // Feature: ia-mentor-carreira, Property 14: Validação da disponibilidade semanal
    // **Validates: Requirements 5.6, 2.3**
    [Property(MaxTest = 100)]
    public Property Disponibilidade_informada_e_aceita_se_e_somente_se_entre_1_e_168_e_rejeitada_nao_altera_nada()
    {
        return Prop.ForAll(GeradorInteiros().ToArbitrary(), horas =>
        {
            var sessao = CriarSessao(10);
            var repo = new RepositorioFake(sessao);
            var catalogo = new CatalogoFake(CriarCatalogo());
            var validator = new PlanoGerarCommandValidator(new LocalizerFake(), repo, catalogo);
            var comando = new PlanoGerarCommand(sessao.Id, Area, horas);

            var aceita = validator.ValidateAsync(comando).GetAwaiter().GetResult().IsValid;
            if (aceita != Valido(horas)) return false;

            if (!aceita)
            {
                // Pipeline: a validação falha antes do handler. Mesmo se o handler fosse chamado, nada muda.
                var handler = new PlanoGerarCommandHandler(repo, catalogo);
                try { handler.Handle(comando, CancellationToken.None).GetAwaiter().GetResult(); }
                catch (Exception) { /* rejeição por exceção também não pode alterar estado */ }
                if (repo.Salvamentos != 0 || repo.Sessao != sessao) return false;
            }

            return true;
        });
    }

    // Feature: ia-mentor-carreira, Property 14: Validação da disponibilidade semanal (valor vindo do perfil)
    // **Validates: Requirements 5.6, 2.3**
    [Property(MaxTest = 100)]
    public Property Sem_valor_no_pedido_a_disponibilidade_do_perfil_decide_e_perfil_invalido_nao_gera_plano()
    {
        var gen = Gen.OneOf(GeradorInteiros().Select(h => (int?)h), Gen.Constant<int?>(null));
        return Prop.ForAll(gen.ToArbitrary(), horasPerfil =>
        {
            var sessao = CriarSessao(horasPerfil);
            var repo = new RepositorioFake(sessao);
            var catalogo = new CatalogoFake(CriarCatalogo());
            var validator = new PlanoGerarCommandValidator(new LocalizerFake(), repo, catalogo);
            var comando = new PlanoGerarCommand(sessao.Id, Area);

            var aceita = validator.ValidateAsync(comando).GetAwaiter().GetResult().IsValid;
            var esperado = horasPerfil.HasValue && Valido(horasPerfil.Value);
            if (aceita != esperado) return false;

            if (!aceita)
            {
                var handler = new PlanoGerarCommandHandler(repo, catalogo);
                try { handler.Handle(comando, CancellationToken.None).GetAwaiter().GetResult(); }
                catch (Exception) { }
                if (repo.Salvamentos != 0 || repo.Sessao != sessao) return false;
            }

            return true;
        });
    }
}
