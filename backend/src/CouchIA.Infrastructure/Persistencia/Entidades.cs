namespace CouchIA.Infrastructure.Persistencia;

public sealed class SessaoEntidade
{
    /// <summary>Hash SHA-256 (hex) do identificador da Sessao. O valor bruto só existe no cookie.</summary>
    public string Id { get; set; } = default!;
    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset UltimaAtividadeEm { get; set; }
    public string Entrevista { get; set; } = default!;   // JSONB
    public string Perfil { get; set; } = default!;       // JSON cifrado
    public List<MensagemEntidade> Mensagens { get; set; } = new();
    public List<RecomendacaoEntidade> Recomendacoes { get; set; } = new();
    public List<FeedbackEntidade> Feedbacks { get; set; } = new();
    public List<PlanoEntidade> Planos { get; set; } = new();
}

public sealed class MensagemEntidade
{
    public string SessaoId { get; set; } = default!;
    public int Ordem { get; set; }
    public Guid ClientMessageId { get; set; }
    public int Papel { get; set; }
    public string Texto { get; set; } = default!;        // cifrado
    public DateTimeOffset CriadaEm { get; set; }
}

public sealed class RecomendacaoEntidade
{
    public string SessaoId { get; set; } = default!;
    public string AreaId { get; set; } = default!;
    public int Pontuacao { get; set; }
    public string Justificativa { get; set; } = default!; // cifrada
    public int OrdemOriginal { get; set; }
}

public sealed class FeedbackEntidade
{
    public string SessaoId { get; set; } = default!;
    public string AreaId { get; set; } = default!;
    public int Tipo { get; set; }
    public string? Motivo { get; set; }                   // cifrado
    public DateTimeOffset Em { get; set; }
}

public sealed class PlanoEntidade
{
    public Guid Id { get; set; }
    public string SessaoId { get; set; } = default!;
    public string AreaId { get; set; } = default!;
    public int Ordem { get; set; }
    public string Conteudo { get; set; } = default!;      // JSON cifrado
}
