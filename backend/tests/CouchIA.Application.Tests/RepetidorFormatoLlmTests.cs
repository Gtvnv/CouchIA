using CouchIA.Application.Mentor.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CouchIA.Application.Tests;

public class RepetidorFormatoLlmTests
{
    private sealed class LlmFalso : ILlmClient
    {
        private readonly Queue<string> _respostas;
        public int Chamadas { get; private set; }

        public LlmFalso(IEnumerable<string> respostas) => _respostas = new Queue<string>(respostas);

        public Task<string> CompletarAsync(RequisicaoLlm requisicao, CancellationToken ct)
        {
            Chamadas++;
            return Task.FromResult(_respostas.Dequeue());
        }

        public IAsyncEnumerable<string> CompletarEmFluxoAsync(RequisicaoLlm requisicao, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private static (bool Valido, string? Valor) Validar(string bruto)
        => bruto.StartsWith("OK:", StringComparison.Ordinal) ? (true, bruto) : (false, null);

    // Cada resposta é válida ("OK:n") ou inválida ("lixo n"); sempre 2 respostas disponíveis.
    private static Gen<string> GeradorResposta() =>
        Gen.Choose(0, 1000).SelectMany(n => Gen.Elements($"OK:{n}", $"lixo {n}"));

    // Feature: ia-mentor-carreira, Property 22: Repetição por formato inválido
    // **Validates: Requirements 1.5, 3.7**
    [Property(MaxTest = 100)]
    public Property No_maximo_duas_chamadas_primeira_valida_ou_erro_sem_alterar_dados()
    {
        var geradorPar = GeradorResposta().SelectMany(a => GeradorResposta().Select(b => (a, b)));
        return Prop.ForAll(geradorPar.ToArbitrary(), par =>
        {
            var (a, b) = par;
            var historico = new List<string> { "m1", "m2" };
            var snapshot = historico.ToList();
            var llm = new LlmFalso(new[] { a, b });
            var requisicao = new RequisicaoLlm("sistema", new List<MensagemLlm>());

            var resultado = new RepetidorFormatoLlm(llm)
                .ExecutarAsync(requisicao, Validar, CancellationToken.None).GetAwaiter().GetResult();

            var primeiraValida = Validar(a).Valido;
            var segundaValida = Validar(b).Valido;

            bool correto;
            if (primeiraValida)
                correto = resultado.Sucesso && resultado.Valor == a && llm.Chamadas == 1;
            else if (segundaValida)
                correto = resultado.Sucesso && resultado.Valor == b && llm.Chamadas == 2;
            else
                correto = !resultado.Sucesso && resultado.Valor is null && llm.Chamadas == 2;

            return correto
                && llm.Chamadas <= 2
                && historico.SequenceEqual(snapshot);
        });
    }
}
