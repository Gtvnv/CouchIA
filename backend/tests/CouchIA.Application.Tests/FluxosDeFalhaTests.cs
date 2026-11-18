using CouchIA.Application.Common.Behaviours;
using CouchIA.Application.Common.Exceptions;
using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Common.Localization;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Planos.Commands;
using CouchIA.Application.Planos.Exceptions;
using CouchIA.Application.Planos.Queries;
using CouchIA.Application.Recomendacoes.Commands;
using CouchIA.Application.Recomendacoes.Queries;
using CouchIA.Application.Sessoes.Commands;
using CouchIA.Application.Sessoes.Queries;
using CouchIA.Domain;
using FluentValidation;
using Microsoft.Extensions.Localization;
using PapelMensagem = CouchIA.Domain.PapelMensagem;

namespace CouchIA.Application.Tests;

/// <summary>Testes de exemplo dos fluxos de falha (Req. 3.7, 7.3 via repositório, 7.4, 7.5, 9.7).</summary>
public class FluxosDeFalhaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private sealed class RelogioFixo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Agora;
    }

    private sealed class LocalizerFalso : IStringLocalizer<MentorLocalization>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }

    private sealed class RepositorioFalho : ISessaoRepository
    {
        public Dictionary<string, Sessao> Sessoes { get; } = new();
        public bool FalharAoSalvar { get; set; }
        public int ChamadasSalvar { get; private set; }
        public int ChamadasExcluir { get; private set; }

        public Task<Sessao?> ObterAsync(SessaoId id, CancellationToken ct) =>
            Task.FromResult(Sessoes.TryGetValue(id.Valor, out var s) ? s : null);

        public Task SalvarAsync(Sessao sessao, CancellationToken ct)
        {
            ChamadasSalvar++;
            if (FalharAoSalvar) throw new IOException("falha de gravação");
            Sessoes[sessao.Id.Valor] = sessao;
            return Task.CompletedTask;
        }

        public Task ExcluirAsync(SessaoId id, CancellationToken ct)
        {
            ChamadasExcluir++;
            Sessoes.Remove(id.Valor);
            return Task.CompletedTask;
        }
    }

    private sealed class CatalogoFalso : ICatalogoProvider
    {
        public Catalogo? Ativo { get; init; }
        public ResultadoCarga Carregar(string json) => throw new NotSupportedException();
    }

    private sealed class LlmFalho : ILlmClient
    {
        private readonly Func<string> _resposta;
        public int Chamadas { get; private set; }
        public LlmFalho(Func<string> resposta) => _resposta = resposta;

        public Task<string> CompletarAsync(RequisicaoLlm requisicao, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(_resposta());
        }

        public IAsyncEnumerable<string> CompletarEmFluxoAsync(RequisicaoLlm requisicao, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private static Catalogo CatalogoComAreas(int quantidade) =>
        new(Enumerable.Range(0, quantidade)
                .Select(i => new AreaAtuacao(new AreaId($"area-{i}"), $"Área {i}", "Descrição.", Array.Empty<HabilidadeNaArea>()))
                .ToList(),
            Array.Empty<Habilidade>());

    private static Sessao NovaSessao(IReadOnlyList<RecomendacaoArea>? recomendacoes = null, IReadOnlyList<FeedbackRecomendacao>? feedbacks = null)
    {
        var categorias = Enum.GetValues<CategoriaEntrevista>()
            .ToDictionary(c => c, c => c == CategoriaEntrevista.Interesses ? EstadoCategoria.Respondida : EstadoCategoria.Pendente);
        return new Sessao(
            SessaoId.Gerar(), Agora, Array.Empty<MensagemSessao>(),
            new PerfilUsuario(new[] { "dados" }, null, null, null, 10, null, new HashSet<HabilidadeId>()),
            new Entrevista(EstadoEntrevista.EmAndamento, categorias, new Dictionary<CategoriaEntrevista, int>()),
            recomendacoes ?? Array.Empty<RecomendacaoArea>(),
            Array.Empty<PlanoDesenvolvimento>(),
            feedbacks ?? Array.Empty<FeedbackRecomendacao>());
    }

    // ---------- Req. 3.7: falha ao gerar recomendações não persiste dados parciais ----------

    [Fact]
    public async Task Falha_do_LLM_ao_gerar_recomendacoes_nao_persiste_nada()
    {
        var repo = new RepositorioFalho();
        var sessao = NovaSessao();
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var llm = new LlmFalho(() => throw new LlmTimeoutException());
        var handler = new RecomendacoesGerarCommandHandler(repo, new CatalogoFalso { Ativo = CatalogoComAreas(3) }, llm);

        await Assert.ThrowsAsync<RecomendacoesIndisponiveisException>(
            () => handler.Handle(new RecomendacoesGerarCommand(sessao.Id), default));

        Assert.Equal(0, repo.ChamadasSalvar);
        Assert.Same(sessao, repo.Sessoes[sessao.Id.Valor]);
    }

    [Fact]
    public async Task Duas_respostas_invalidas_do_LLM_nao_persistem_nada_e_mantem_recomendacoes_anteriores()
    {
        var anteriores = new[] { new RecomendacaoArea(new AreaId("area-0"), 80, "dados") };
        var repo = new RepositorioFalho();
        var sessao = NovaSessao(anteriores);
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var llm = new LlmFalho(() => "isto não é JSON");
        var handler = new RecomendacoesGerarCommandHandler(repo, new CatalogoFalso { Ativo = CatalogoComAreas(3) }, llm);

        await Assert.ThrowsAsync<RecomendacoesIndisponiveisException>(
            () => handler.Handle(new RecomendacoesGerarCommand(sessao.Id), default));

        Assert.Equal(RepetidorFormatoLlm.MaximoChamadas, llm.Chamadas);
        Assert.Equal(0, repo.ChamadasSalvar);
        Assert.Equal(anteriores, repo.Sessoes[sessao.Id.Valor].Recomendacoes);
        Assert.Equal(sessao.Perfil, repo.Sessoes[sessao.Id.Valor].Perfil);
    }

    // ---------- Req. 9.7: falha ao registrar feedback preserva o anterior ----------

    [Fact]
    public async Task Falha_ao_gravar_feedback_propaga_e_preserva_o_feedback_anterior()
    {
        var area = new AreaId("area-0");
        var anterior = new FeedbackRecomendacao(area, TipoFeedback.Util, "gostei", Agora.AddMinutes(-5));
        var repo = new RepositorioFalho();
        var sessao = NovaSessao(
            new[] { new RecomendacaoArea(area, 90, "dados") },
            new[] { anterior });
        repo.Sessoes[sessao.Id.Valor] = sessao;
        repo.FalharAoSalvar = true;
        var handler = new FeedbackRegistrarCommandHandler(
            repo, new CatalogoFalso { Ativo = CatalogoComAreas(3) }, new RelogioFixo(), new LocalizerFalso());

        await Assert.ThrowsAsync<IOException>(() => handler.Handle(
            new FeedbackRegistrarCommand(sessao.Id, area.Valor, TipoFeedback.NaoUtil, "mudei de ideia"), default));

        var guardado = Assert.Single(repo.Sessoes[sessao.Id.Valor].Feedbacks);
        Assert.Equal(anterior, guardado);
    }

    // ---------- Req. 1.5: timeout/falha do LLM preserva a mensagem e o reenvio não duplica ----------

    private sealed class LlmEmFluxo : ILlmClient
    {
        private readonly Func<IAsyncEnumerable<string>> _fluxo;
        public int Chamadas { get; private set; }
        public LlmEmFluxo(Func<IAsyncEnumerable<string>> fluxo) => _fluxo = fluxo;

        public Task<string> CompletarAsync(RequisicaoLlm requisicao, CancellationToken ct) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<string> CompletarEmFluxoAsync(RequisicaoLlm requisicao, CancellationToken ct)
        {
            Chamadas++;
            return _fluxo();
        }
    }

    private static async IAsyncEnumerable<string> FluxoQueExpira()
    {
        await Task.Yield();
        throw new LlmTimeoutException();
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<string> FluxoComTexto(string texto)
    {
        await Task.Yield();
        yield return texto;
    }

    [Fact]
    public async Task Timeout_do_LLM_preserva_a_mensagem_do_usuario_e_o_reenvio_nao_duplica()
    {
        var repo = new RepositorioFalho();
        var sessao = NovaSessao();
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var clientId = Guid.NewGuid();
        var comando = new MensagemEnviarCommand(sessao.Id, clientId, "quero trabalhar com dados");

        var expira = new MensagemEnviarCommandHandler(repo, new LlmEmFluxo(FluxoQueExpira), new RelogioFixo());
        await Assert.ThrowsAsync<LlmTimeoutException>(() => expira.Handle(comando, default));

        var aposFalha = repo.Sessoes[sessao.Id.Valor];
        var preservada = Assert.Single(aposFalha.Historico);
        Assert.Equal(clientId, preservada.ClientMessageId);
        Assert.Equal(PapelMensagem.Usuario, preservada.Papel);
        Assert.Equal("quero trabalhar com dados", preservada.Texto);
        Assert.Equal(sessao.Perfil, aposFalha.Perfil);
        Assert.Equal(sessao.Entrevista, aposFalha.Entrevista);

        var llmOk = new LlmEmFluxo(() => FluxoComTexto("Olá! Quais são seus interesses?"));
        var reenvio = new MensagemEnviarCommandHandler(repo, llmOk, new RelogioFixo());
        var resposta = await reenvio.Handle(comando, default);

        var depois = repo.Sessoes[sessao.Id.Valor];
        Assert.Equal(2, depois.Historico.Count);
        Assert.Single(depois.Historico, m => m.Papel == PapelMensagem.Usuario);
        Assert.Single(depois.Historico, m => m.ClientMessageId == clientId);
        Assert.Equal(PapelMensagem.Mentor, depois.Historico[1].Papel);
        Assert.Equal(1, llmOk.Chamadas);
        Assert.False(resposta.Reenvio);
        Assert.Equal(sessao.Perfil, depois.Perfil);
    }

    [Fact]
    public async Task Falha_do_LLM_apos_trecho_parcial_nao_grava_resposta_nem_altera_perfil()
    {
        var repo = new RepositorioFalho();
        var sessao = NovaSessao();
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var comando = new MensagemEnviarCommand(sessao.Id, Guid.NewGuid(), "olá");

        async IAsyncEnumerable<string> ParcialEFalha()
        {
            await Task.Yield();
            yield return "Olá, ";
            throw new IOException("conexão interrompida");
        }

        var handler = new MensagemEnviarCommandHandler(repo, new LlmEmFluxo(ParcialEFalha), new RelogioFixo());

        await Assert.ThrowsAsync<IOException>(() => handler.Handle(comando, default));

        var guardada = repo.Sessoes[sessao.Id.Valor];
        Assert.Single(guardada.Historico);
        Assert.Equal(PapelMensagem.Usuario, guardada.Historico[0].Papel);
        Assert.Equal(sessao.Perfil, guardada.Perfil);
        Assert.Equal(sessao.Entrevista, guardada.Entrevista);
    }

    // ---------- Req. 4.6 e catálogo ausente ----------

    [Fact]
    public async Task Catalogo_ausente_faz_o_mapa_falhar_sem_mapa_parcial()
    {
        var repo = new RepositorioFalho();
        var sessao = NovaSessao();
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var handler = new MapaHabilidadesObterQueryHandler(repo, new CatalogoFalso { Ativo = null });

        var ex = await Assert.ThrowsAsync<MapaHabilidadesIndisponivelException>(
            () => handler.Handle(new MapaHabilidadesObterQuery(sessao.Id, "area-0"), default));

        Assert.Equal(TipoErroMapa.CatalogoIndisponivel, ex.Motivo);
    }

    [Fact]
    public async Task Catalogo_ausente_faz_RecomendacoesGerar_lancar_indisponivel_sem_chamar_o_LLM_nem_gravar()
    {
        var repo = new RepositorioFalho();
        var sessao = NovaSessao();
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var llm = new LlmFalho(() => "{}");
        var handler = new RecomendacoesGerarCommandHandler(repo, new CatalogoFalso { Ativo = null }, llm);

        await Assert.ThrowsAsync<RecomendacoesIndisponiveisException>(
            () => handler.Handle(new RecomendacoesGerarCommand(sessao.Id), default));

        Assert.Equal(0, llm.Chamadas);
        Assert.Equal(0, repo.ChamadasSalvar);
    }

    [Fact]
    public async Task Catalogo_ausente_faz_PlanoGerar_lancar_catalogo_indisponivel_sem_gravar()
    {
        var repo = new RepositorioFalho();
        var sessao = NovaSessao();
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var handler = new PlanoGerarCommandHandler(repo, new CatalogoFalso { Ativo = null });

        await Assert.ThrowsAsync<CatalogoIndisponivelException>(
            () => handler.Handle(new PlanoGerarCommand(sessao.Id, new AreaId("area-0")), default));

        Assert.Equal(0, repo.ChamadasSalvar);
        Assert.Empty(repo.Sessoes[sessao.Id.Valor].Planos);
    }

    // ---------- Req. 5.8: falha de exportação ----------

    private sealed class ExportadorFalho : IExportadorPlano
    {
        public int Chamadas { get; private set; }

        public Task<ArquivoExportado> ExportarAsync(PlanoDesenvolvimento plano, FormatoExportacao formato, CancellationToken ct)
        {
            Chamadas++;
            throw new ExportacaoFalhouException("falha simulada");
        }
    }

    [Theory]
    [InlineData(FormatoExportacao.Markdown)]
    [InlineData(FormatoExportacao.Pdf)]
    public async Task Falha_de_exportacao_e_propagada_sem_arquivo_parcial(FormatoExportacao formato)
    {
        var plano = new PlanoDesenvolvimento(Guid.NewGuid(), new AreaId("area-0"), 10, Array.Empty<EtapaPlano>(), 0);
        var repo = new RepositorioFalho();
        var sessao = NovaSessao() with { Planos = new[] { plano } };
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var exportador = new ExportadorFalho();
        var handler = new PlanoExportarQueryHandler(repo, exportador);

        var resultado = await Record.ExceptionAsync(
            () => handler.Handle(new PlanoExportarQuery(sessao.Id, plano.Id, formato), default));

        Assert.IsType<ExportacaoFalhouException>(resultado);
        Assert.Equal(1, exportador.Chamadas);
        Assert.Equal(0, repo.ChamadasSalvar);
    }

    // ---------- Req. 7.4: exclusão sem confirmação mantém os dados ----------

    [Fact]
    public async Task Exclusao_sem_confirmacao_e_recusada_pela_validacao_e_mantem_os_dados()
    {
        var repo = new RepositorioFalho();
        var sessao = NovaSessao();
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var handler = new SessaoExcluirCommandHandler(repo);
        var validacao = new ValidationBehaviour<SessaoExcluirCommand, MediatR.Unit>(
            new IValidator<SessaoExcluirCommand>[] { new SessaoExcluirCommandValidator(new LocalizerFalso()) });
        var comando = new SessaoExcluirCommand(sessao.Id, Confirmado: false);

        await Assert.ThrowsAsync<ValidationException>(() => validacao.Handle(
            comando, async () => { await handler.Handle(comando, default); return MediatR.Unit.Value; }, default));

        Assert.Equal(0, repo.ChamadasExcluir);
        Assert.True(repo.Sessoes.ContainsKey(sessao.Id.Valor));
    }

    [Fact]
    public async Task Exclusao_confirmada_remove_a_sessao()
    {
        var repo = new RepositorioFalho();
        var sessao = NovaSessao();
        repo.Sessoes[sessao.Id.Valor] = sessao;
        var handler = new SessaoExcluirCommandHandler(repo);
        var validacao = new ValidationBehaviour<SessaoExcluirCommand, MediatR.Unit>(
            new IValidator<SessaoExcluirCommand>[] { new SessaoExcluirCommandValidator(new LocalizerFalso()) });
        var comando = new SessaoExcluirCommand(sessao.Id, Confirmado: true);

        await validacao.Handle(comando, async () => { await handler.Handle(comando, default); return MediatR.Unit.Value; }, default);

        Assert.Empty(repo.Sessoes);
    }

    // ---------- Req. 7.5: sessão inexistente, expirada ou malformada ----------

    [Fact]
    public async Task Sessao_inexistente_ou_expirada_retorna_nao_encontrada()
    {
        // Para o handler, expirada e inexistente são equivalentes: o repositório devolve null (expiração real testada na infraestrutura).
        var handler = new SessaoObterQueryHandler(new RepositorioFalho());

        await Assert.ThrowsAsync<SessaoNaoEncontradaException>(
            () => handler.Handle(new SessaoObterQuery(SessaoId.Gerar()), default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curto")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]   // 44 caracteres, com padding
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]    // alfabeto Base64 padrão, não Base64Url
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB")]    // bits finais não canônicos
    [InlineData("'; DROP TABLE sessoes; --AAAAAAAAAAAAAAAAAAAAAA")]
    public void Identificador_malformado_e_recusado_por_TryParse(string? texto)
    {
        Assert.False(SessaoId.TryParse(texto, out _));
        Assert.Throws<ArgumentException>(() => SessaoId.Criar(texto!));
    }
}
