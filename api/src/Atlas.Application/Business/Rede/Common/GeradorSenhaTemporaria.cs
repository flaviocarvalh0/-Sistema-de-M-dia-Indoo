using System.Security.Cryptography;

namespace Atlas.Application.Business.Rede.Common;

/// <summary>
/// Senha temporária do responsável pelo parceiro, trocada obrigatoriamente no
/// primeiro acesso. Gerador criptográfico.
/// </summary>
public static class GeradorSenhaTemporaria
{
    // Sem caracteres que se confundem ao ler num e-mail (0/O, 1/l/I).
    private const string Minusculas = "abcdefghijkmnopqrstuvwxyz";
    private const string Maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digitos = "23456789";
    private const int Tamanho = 12;

    /// <summary>Atende à política de senha do Identity da plataforma (maiúscula, minúscula e dígito).</summary>
    public static string Gerar()
    {
        var todos = Minusculas + Maiusculas + Digitos;

        var caracteres = new List<char>
        {
            Sortear(Minusculas),
            Sortear(Maiusculas),
            Sortear(Digitos),
        };

        while (caracteres.Count < Tamanho)
        {
            caracteres.Add(Sortear(todos));
        }

        // Embaralha para as classes obrigatórias não ficarem sempre no começo.
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string(caracteres.ToArray());
    }

    private static char Sortear(string alfabeto) => alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
}
