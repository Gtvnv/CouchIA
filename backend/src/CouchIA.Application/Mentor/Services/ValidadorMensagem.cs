namespace CouchIA.Application.Mentor.Services;

/// <summary>
/// Valida mensagens do usuário: aceita se o trim não é vazio e o tamanho está entre 1 e 2000 caracteres.
/// </summary>
public static class ValidadorMensagem
{
    public const int TamanhoMinimo = 1;
    public const int TamanhoMaximo = 2000;

    public static bool EhValida(string? mensagem)
    {
        if (mensagem is null || string.IsNullOrWhiteSpace(mensagem))
            return false;

        return mensagem.Length >= TamanhoMinimo && mensagem.Length <= TamanhoMaximo;
    }
}
