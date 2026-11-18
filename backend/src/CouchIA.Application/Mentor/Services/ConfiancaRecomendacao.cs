namespace CouchIA.Application.Mentor.Services;

/// <summary>
/// Calcula o indicador de baixa confiança das recomendações.
/// </summary>
public static class ConfiancaRecomendacao
{
    public const int MinimoCategoriasRespondidas = 3;

    public static bool BaixaConfianca(int categoriasRespondidas)
        => categoriasRespondidas < MinimoCategoriasRespondidas;
}
