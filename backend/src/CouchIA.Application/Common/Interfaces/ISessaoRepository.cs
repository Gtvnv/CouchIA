using CouchIA.Domain;

namespace CouchIA.Application.Common.Interfaces;

public interface ISessaoRepository
{
    /// <summary>Devolve a Sessao ou null se não existe, foi excluída ou expirou por inatividade. Registra atividade.</summary>
    Task<Sessao?> ObterAsync(SessaoId id, CancellationToken ct);

    /// <summary>Cria ou atualiza a Sessao e seus filhos. Tenta novamente uma vez em falha de gravação (Req. 7.3).</summary>
    Task SalvarAsync(Sessao sessao, CancellationToken ct);

    /// <summary>Remove a Sessao e todos os dados associados, de forma irrecuperável (Req. 7.4, 7.5).</summary>
    Task ExcluirAsync(SessaoId id, CancellationToken ct);
}
