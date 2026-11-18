namespace CouchIA.Application.Common.Interfaces;

/// <summary>Porta de acesso ao banco para operações que não pertencem ao agregado Sessao (Req. 7.4).</summary>
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Remove, com exclusão em cascata de mensagens, recomendações, feedbacks e planos, as sessões
    /// sem atividade desde <paramref name="limite"/>. Devolve a quantidade de sessões removidas.
    /// </summary>
    Task<int> RemoverSessoesInativasAsync(DateTimeOffset limite, CancellationToken cancellationToken);
}
