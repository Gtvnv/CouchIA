using CouchIA.Application.Common.Interfaces;
using CouchIA.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CouchIA.Infrastructure.Persistencia;

public sealed class SessaoRepository : ISessaoRepository
{
    private readonly ApplicationDbContext _db;
    private readonly SessaoOptions _options;
    private readonly TimeProvider _relogio;

    public SessaoRepository(ApplicationDbContext db, SessaoOptions options, TimeProvider relogio)
    {
        _db = db;
        _options = options;
        _relogio = relogio;
    }

    public async Task<Sessao?> ObterAsync(SessaoId id, CancellationToken ct)
    {
        var hash = SessaoMapeador.CalcularHash(id);
        var agora = _relogio.GetUtcNow();
        var limite = agora - _options.Inatividade;

        // Sempre filtrado pela Sessao; filhos carregados pelo Include.
        var entidade = await _db.Sessoes
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Mensagens)
            .Include(s => s.Recomendacoes)
            .Include(s => s.Feedbacks)
            .Include(s => s.Planos)
            .FirstOrDefaultAsync(s => s.Id == hash, ct);

        if (entidade is null) return null;

        if (entidade.UltimaAtividadeEm < limite)
        {
            // Expirada por inatividade: remove e trata como inexistente.
            await _db.Sessoes.Where(s => s.Id == hash && s.UltimaAtividadeEm < limite).ExecuteDeleteAsync(ct);
            return null;
        }

        await _db.Sessoes.Where(s => s.Id == hash).ExecuteUpdateAsync(
            set => set.SetProperty(s => s.UltimaAtividadeEm, agora.ToUniversalTime()), ct);

        return SessaoMapeador.ParaDominio(id, entidade);
    }

    public Task SalvarAsync(Sessao sessao, CancellationToken ct) =>
        ComUmaRetentativaAsync(() => SalvarUmaVezAsync(sessao, ct));

    public async Task ExcluirAsync(SessaoId id, CancellationToken ct)
    {
        var hash = SessaoMapeador.CalcularHash(id);
        // ON DELETE CASCADE remove mensagens, recomendações, feedbacks e planos.
        await ComUmaRetentativaAsync(async () =>
            await _db.Sessoes.Where(s => s.Id == hash).ExecuteDeleteAsync(ct));
    }

    private async Task SalvarUmaVezAsync(Sessao sessao, CancellationToken ct)
    {
        var hash = SessaoMapeador.CalcularHash(sessao.Id);
        var agora = _relogio.GetUtcNow().ToUniversalTime();

        var entidade = await _db.Sessoes
            .AsSplitQuery()
            .Include(s => s.Mensagens)
            .Include(s => s.Recomendacoes)
            .Include(s => s.Feedbacks)
            .Include(s => s.Planos)
            .FirstOrDefaultAsync(s => s.Id == hash, ct);

        if (entidade is null)
        {
            entidade = new SessaoEntidade { Id = hash, CriadaEm = sessao.CriadaEm.ToUniversalTime() };
            _db.Sessoes.Add(entidade);
        }

        entidade.UltimaAtividadeEm = agora;
        entidade.Entrevista = SessaoMapeador.SerializarEntrevista(sessao.Entrevista);
        entidade.Perfil = SessaoMapeador.SerializarPerfil(sessao.Perfil);

        SincronizarMensagens(entidade, sessao);
        SincronizarRecomendacoes(entidade, sessao);
        SincronizarFeedbacks(entidade, sessao);
        SincronizarPlanos(entidade, sessao);

        await _db.SaveChangesAsync(ct);
    }

    private static void SincronizarMensagens(SessaoEntidade e, Sessao s)
    {
        var novas = s.Historico.ToDictionary(m => m.Ordem);
        e.Mensagens.RemoveAll(m => !novas.ContainsKey(m.Ordem));
        foreach (var m in s.Historico)
        {
            var existente = e.Mensagens.FirstOrDefault(x => x.Ordem == m.Ordem);
            if (existente is null)
            {
                existente = new MensagemEntidade { SessaoId = e.Id, Ordem = m.Ordem };
                e.Mensagens.Add(existente);
            }
            existente.ClientMessageId = m.ClientMessageId;
            existente.Papel = (int)m.Papel;
            existente.Texto = m.Texto;
            existente.CriadaEm = m.CriadaEm.ToUniversalTime();
        }
    }

    private static void SincronizarRecomendacoes(SessaoEntidade e, Sessao s)
    {
        var chaves = s.Recomendacoes.Select(r => r.Area.Valor).ToHashSet();
        e.Recomendacoes.RemoveAll(r => !chaves.Contains(r.AreaId));
        var ordem = 0;
        foreach (var r in s.Recomendacoes)
        {
            var existente = e.Recomendacoes.FirstOrDefault(x => x.AreaId == r.Area.Valor);
            if (existente is null)
            {
                existente = new RecomendacaoEntidade { SessaoId = e.Id, AreaId = r.Area.Valor };
                e.Recomendacoes.Add(existente);
            }
            existente.Pontuacao = r.Pontuacao;
            existente.Justificativa = r.Justificativa;
            existente.OrdemOriginal = ordem++;
        }
    }

    private static void SincronizarFeedbacks(SessaoEntidade e, Sessao s)
    {
        var chaves = s.Feedbacks.Select(f => f.Area.Valor).ToHashSet();
        e.Feedbacks.RemoveAll(f => !chaves.Contains(f.AreaId));
        foreach (var f in s.Feedbacks)
        {
            var existente = e.Feedbacks.FirstOrDefault(x => x.AreaId == f.Area.Valor);
            if (existente is null)
            {
                existente = new FeedbackEntidade { SessaoId = e.Id, AreaId = f.Area.Valor };
                e.Feedbacks.Add(existente);
            }
            existente.Tipo = (int)f.Tipo;
            existente.Motivo = f.Motivo;
            existente.Em = f.Em.ToUniversalTime();
        }
    }

    private static void SincronizarPlanos(SessaoEntidade e, Sessao s)
    {
        var ids = s.Planos.Select(p => p.Id).ToHashSet();
        e.Planos.RemoveAll(p => !ids.Contains(p.Id));
        var ordem = 0;
        foreach (var p in s.Planos)
        {
            var existente = e.Planos.FirstOrDefault(x => x.Id == p.Id);
            if (existente is null)
            {
                existente = new PlanoEntidade { Id = p.Id, SessaoId = e.Id };
                e.Planos.Add(existente);
            }
            existente.AreaId = p.Area.Valor;
            existente.Ordem = ordem++;
            existente.Conteudo = SessaoMapeador.SerializarPlano(p);
        }
    }

    /// <summary>Uma nova tentativa em falha de gravação (Req. 7.3). A segunda falha é propagada.</summary>
    private async Task ComUmaRetentativaAsync(Func<Task> operacao)
    {
        try
        {
            await operacao();
        }
        catch (Exception ex) when (ex is DbUpdateException or NpgsqlException or TimeoutException)
        {
            _db.ChangeTracker.Clear();
            await operacao();
        }
    }
}
