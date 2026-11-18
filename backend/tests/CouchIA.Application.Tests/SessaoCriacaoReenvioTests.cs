using CouchIA.Application.Common.Interfaces;
using CouchIA.Application.Mentor.Services;
using CouchIA.Application.Sessoes.Commands;
using CouchIA.Domain;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class SessaoCriacaoReenvioTests
{
    private static readonly DateTimeOffset Agora = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private sealed class RelogioFixo : TimeProvider
    {
        private readonly DateTimeOffset _agora;
        public RelogioFixo(DateTimeOffset agora) => _agora = agora;
        public override DateTimeOffset GetUtcNow() => _agora;
    }

    private sealed class RepositorioEmMemoria : ISessaoRepository
    {
        public Dictionary<string, Sessao> Sessoes { get; } = new();

        public Task<Sessao?> ObterAsync(SessaoId id, CancellationToken ct) =>
            Task.FromResult(Sessoes.TryGetValue(id.Valor, out var s) ? s : null);

        public Task SalvarAsync(Sessao sessao, CancellationToken ct)
        {
            Sessoes[sessao.Id.Valor] = sessao;
            return Task.CompletedTask;
        }

        public Task ExcluirAsync(SessaoId id, CancellationToken ct)
        {
            Sessoes.Remove(id.Valor);
            return Task.CompletedTask;
        }
    }

    private sealed class LlmFalso : ILlmClient
    {
        public int Chamadas { get; private set; }

        public Task<string> CompletarAsync(RequisicaoLlm requisicao, CancellationToken ct) =>
            Task.FromResult(Resposta());

        public async IAsyncEnumerable<string> CompletarEmFluxoAsync(
            RequisicaoLlm requisicao, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            Chamadas++;
            await Task.Yield();
            yield return Resposta();
        }

        private static string Resposta() =>
            "Conte mais sobre você?\n" + MensagemEnviarCommandHandler.MarcadorExtracao +
            "{\"utilizavel\":true,\"interesses\":[\"dados\"],\"texto\":\"resumo\",\"horasEstudoPorSemana\":10}";
    }

    // Feature: ia-mentor-carreira, Property 1: Criação de Sessao
    // **Validates: Requirements 1.2, 7.1**
    [Property(MaxTest = 100)]
    public Property Criacao_gera_id_de_256_bits_unico_historico_vazio_e_data_de_criacao()
    {
        var gen = Gen.Two(Gen.Elements(true, false)).Select(p => p.Item1);
        return Prop.ForAll(gen.ToArbitrary(), iniciar =>
        {
            var repo = new RepositorioEmMemoria();
            var handler = new SessaoCriarCommandHandler(repo, new RelogioFixo(Agora));

            var vms = Enumerable.Range(0, 5)
                .Select(_ => handler.Handle(new SessaoCriarCommand(iniciar), CancellationToken.None).GetAwaiter().GetResult())
                .ToList();

            var idsUnicos = vms.Select(v => v.Id).Distinct().Count() == vms.Count;
            var idsValidos = vms.All(v =>
                SessaoId.TryParse(v.Id, out var id)
                && id.ObterBytes().Length == 32
                && v.Id.Length == SessaoId.TamanhoEmCaracteres);
            var persistidas = vms.All(v =>
                repo.Sessoes.TryGetValue(v.Id, out var s)
                && s.Historico.Count == 0
                && s.CriadaEm == Agora
                && v.CriadaEm == Agora);

            return idsUnicos && idsValidos && persistidas;
        });
    }

    // Feature: ia-mentor-carreira, Property 3: Reenvio idempotente
    // **Validates: Requirements 1.5**
    [Property(MaxTest = 100)]
    public Property Reenvio_do_mesmo_clientMessageId_nao_duplica_historico_nem_altera_perfil()
    {
        var gen = from total in Gen.Choose(1, 4)
                  from escolhida in Gen.Choose(0, total - 1)
                  from reenvios in Gen.Choose(1, 3)
                  from textos in Gen.ListOf(Gen.Elements<string>("Gosto de dados", "Trabalho com web", "Tenho 10 horas", "Quero crescer"), total)
                  select (total, escolhida, reenvios, textos: textos.ToList());

        return Prop.ForAll(gen.ToArbitrary(), caso =>
        {
            var repo = new RepositorioEmMemoria();
            var llm = new LlmFalso();
            var relogio = new RelogioFixo(Agora);
            var criar = new SessaoCriarCommandHandler(repo, relogio);
            var enviar = new MensagemEnviarCommandHandler(repo, llm, relogio);

            var vm = criar.Handle(new SessaoCriarCommand(), CancellationToken.None).GetAwaiter().GetResult();
            var sessaoId = SessaoId.Criar(vm.Id);

            var ids = Enumerable.Range(0, caso.total).Select(_ => Guid.NewGuid()).ToList();
            for (var i = 0; i < caso.total; i++)
            {
                enviar.Handle(new MensagemEnviarCommand(sessaoId, ids[i], caso.textos[i]), CancellationToken.None)
                    .GetAwaiter().GetResult();
            }

            var antes = repo.Sessoes[vm.Id];
            var chamadasAntes = llm.Chamadas;

            var respostas = Enumerable.Range(0, caso.reenvios)
                .Select(_ => enviar.Handle(
                    new MensagemEnviarCommand(sessaoId, ids[caso.escolhida], "texto diferente no reenvio"),
                    CancellationToken.None).GetAwaiter().GetResult())
                .ToList();

            var depois = repo.Sessoes[vm.Id];

            return antes.Historico.Count == caso.total * 2
                && depois.Historico.Count == antes.Historico.Count
                && depois.Historico.SequenceEqual(antes.Historico)
                && depois.Historico.Count(m => m.ClientMessageId == ids[caso.escolhida]) == 1
                && depois.Perfil == antes.Perfil
                && depois.Entrevista == antes.Entrevista
                && llm.Chamadas == chamadasAntes
                && respostas.All(r => r.Reenvio);
        });
    }
}
