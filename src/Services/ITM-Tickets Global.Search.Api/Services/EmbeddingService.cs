using System.Security.Cryptography;
using System.Text;

namespace ITM_Tickets_Global.Search.Api.Services;

/// <summary>
/// Genera embeddings deterministas de 128 dimensiones a partir del texto.
///
/// NOTA DE DISEÑO: Este es un embedding sintético (hashing trick + bag-of-tokens
/// normalizado) que captura similitud léxica básica entre frases. Es funcional
/// para entornos de desarrollo y permite que Qdrant ejecute búsqueda vectorial
/// real, pero NO reemplaza un modelo entrenado.
///
/// Para producción, sustituir esta clase por:
///   - OpenAI text-embedding-3-small (1536d)
///   - SentenceTransformers vía ONNX runtime local (384d)
///   - Servicio cohere/voyage/azure openai
/// La interfaz queda intencionalmente igual para que el cambio sea drop-in.
/// </summary>
public class EmbeddingService
{
    public const int Dimensions = 128;

    public float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        if (string.IsNullOrWhiteSpace(text)) return vector;

        var tokens = Tokenize(text);
        foreach (var token in tokens)
        {
            // Hashing trick: cada token modifica una dimensión determinista
            // (parecido a feature hashing en sklearn).
            var hash = StableHash(token);
            var idx = (int)(hash % Dimensions);
            var sign = (hash & 1) == 0 ? 1f : -1f;
            vector[idx] += sign;
        }

        // L2 normalization para que el coseno tenga sentido.
        var norm = (float)Math.Sqrt(vector.Sum(v => v * v));
        if (norm > 0)
        {
            for (var i = 0; i < Dimensions; i++) vector[i] /= norm;
        }

        return vector;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var lower = text.ToLowerInvariant();
        var sb = new StringBuilder();
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else
            {
                if (sb.Length > 2) yield return sb.ToString();
                sb.Clear();
            }
        }
        if (sb.Length > 2) yield return sb.ToString();
    }

    private static uint StableHash(string token)
    {
        using var sha = SHA1.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return BitConverter.ToUInt32(bytes, 0);
    }
}
