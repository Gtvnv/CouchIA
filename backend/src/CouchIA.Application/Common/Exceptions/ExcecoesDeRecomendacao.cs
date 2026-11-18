using CouchIA.Application.Mentor.Services;

namespace CouchIA.Application.Common.Exceptions;

/// <summary>Encerramento solicitado sem nenhuma categoria respondida; a entrevista segue ativa (Req. 2.7).</summary>
public sealed class EntrevistaSemRespostasException : Exception
{
    public EntrevistaSemRespostasException()
        : base("É necessário responder ao menos uma categoria da entrevista.") { }
}

/// <summary>
/// As recomendações não puderam ser geradas (catálogo com menos de 3 áreas, falha ou saída inválida da IA).
/// O perfil permanece inalterado e o Usuário pode tentar novamente (Req. 3.7).
/// </summary>
public sealed class RecomendacoesIndisponiveisException : Exception
{
    public RecomendacoesIndisponiveisException(Exception? inner = null)
        : base("As recomendações não puderam ser geradas.", inner) { }
}

/// <summary>O Mapa_de_Habilidades não pôde ser carregado; nenhum mapa parcial é devolvido (Req. 4.6).</summary>
public sealed class MapaHabilidadesIndisponivelException : Exception
{
    public TipoErroMapa Motivo { get; }

    public MapaHabilidadesIndisponivelException(TipoErroMapa motivo)
        : base("O mapa de habilidades não pôde ser carregado.") => Motivo = motivo;
}
