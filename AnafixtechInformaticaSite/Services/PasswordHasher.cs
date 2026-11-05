// =============================================================================================
// PasswordHasher — guarda senhas de forma segura (hash PBKDF2) em vez de texto puro.
//
// Por que isso importa:
//   Se o banco vazar, quem ler a coluna "Password" vê apenas um hash (irreversível).
//   Antes, a senha ficava legível ("123456") e qualquer funcionário com acesso ao banco
//   ou qualquer backup perdido entregava a senha de todo mundo.
//
// Formato gravado no banco:
//   pbkdf2-sha256$210000$<salt em Base64>$<hash em Base64>
//
// O "salt" é um valor aleatório por usuário: duas pessoas com a mesma senha geram hashes
// diferentes. O número de iterações deixa o cálculo lento de propósito, dificultando
// ataques de força bruta.
// =============================================================================================

using System.Security.Cryptography;   // Rfc2898DeriveBytes (PBKDF2), RandomNumberGenerator, CryptographicOperations

namespace AnafixtechInformaticaSite.Services
{
    public static class PasswordHasher
    {
        // Identificador do formato. Serve para saber se o texto guardado já é um hash.
        private const string Prefixo = "pbkdf2-sha256";

        // Custo do cálculo. Quanto maior, mais difícil o ataque e mais lento o login.
        // 210.000 é um valor equilibrado para servidor pequeno (recomendação OWASP para SHA-256).
        private const int Iteracoes = 210_000;

        private const int TamanhoSalt = 16; // bytes aleatórios
        private const int TamanhoHash = 32; // 256 bits

        // Transforma a senha digitada em hash. Use SEMPRE que criar ou trocar uma senha.
        public static string Hash(string senha)
        {
            // Salt novo a cada chamada: impede tabelas prontas (rainbow tables).
            var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);

            // Deriva a chave com PBKDF2-HMAC-SHA256.
            var hash = Rfc2898DeriveBytes.Pbkdf2(
                password: senha,
                salt: salt,
                iterations: Iteracoes,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: TamanhoHash);

            return $"{Prefixo}${Iteracoes}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        // Diz se o texto guardado já está no formato de hash (útil para migrar contas antigas).
        public static bool EhHash(string? guardado)
            => !string.IsNullOrWhiteSpace(guardado) && guardado.StartsWith(Prefixo + "$", StringComparison.Ordinal);

        // Confere a senha digitada contra o hash guardado.
        // A comparação é feita em tempo constante para não vazar informação pelo tempo de resposta.
        public static bool Verificar(string senhaDigitada, string hashGuardado)
        {
            if (string.IsNullOrWhiteSpace(hashGuardado)) return false;

            // Formato inesperado: não tenta adivinhar, apenas recusa.
            var partes = hashGuardado.Split('$');
            if (partes.Length != 4) return false;

            if (!int.TryParse(partes[1], out var iteracoes) || iteracoes <= 0) return false;

            try
            {
                var salt = Convert.FromBase64String(partes[2]);
                var hashEsperado = Convert.FromBase64String(partes[3]);

                // Refaz o cálculo com o MESMO salt e o MESMO número de iterações gravados.
                var hashDigitado = Rfc2898DeriveBytes.Pbkdf2(
                    password: senhaDigitada,
                    salt: salt,
                    iterations: iteracoes,
                    hashAlgorithm: HashAlgorithmName.SHA256,
                    outputLength: hashEsperado.Length);

                return CryptographicOperations.FixedTimeEquals(hashDigitado, hashEsperado);
            }
            catch (FormatException)
            {
                // Base64 inválido: hash corrompido. Nega o acesso sem derrubar a aplicação.
                return false;
            }
        }
    }
}
