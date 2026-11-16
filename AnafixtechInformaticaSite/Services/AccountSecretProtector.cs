using System.Security.Cryptography; // AesGcm, RandomNumberGenerator, SHA256
using System.Text;                  // Encoding (texto → bytes)

namespace AnafixtechInformaticaSite.Services
{
    // Criptografa as senhas das contas da empresa antes de gravar no banco.
    //
    // Como funciona:
    //  1. A chave vem da configuração "Contas:ChaveCriptografia" (variável de ambiente
    //     Contas__ChaveCriptografia). O texto pode ter qualquer tamanho; ele é resumido
    //     com SHA-256 para virar exatamente 32 bytes (256 bits), que é o tamanho do AES.
    //  2. Cada senha é cifrada com AES-GCM, que além de esconder o texto também garante
    //     que ninguém alterou o conteúdo.
    //  3. O que vai para o banco é um Base64 com: [versão][nonce][tag][texto cifrado].
    //
    // Se a chave não estiver configurada, a aplicação NÃO grava a senha em texto puro:
    // o formulário mostra um erro e pede para configurar a variável.
    public class AccountSecretProtector
    {
        private const byte VersaoAtual = 1;   // permite trocar o formato no futuro
        private const int TamanhoNonce = 12;  // 12 bytes é o recomendado para o AES-GCM
        private const int TamanhoTag = 16;    // 16 bytes de "assinatura" (autenticação)
        private const int TamanhoMinimoChave = 16;

        private readonly byte[]? _chave; // 32 bytes derivados do texto da configuração

        public AccountSecretProtector(IConfiguration configuration)
        {
            var texto = configuration["Contas:ChaveCriptografia"];

            if (string.IsNullOrWhiteSpace(texto)) return; // sem chave: só não dá para guardar senhas

            // Chave muito curta é erro de configuração: melhor falhar na subida do que
            // ficar com uma proteção fraca sem ninguém perceber.
            if (texto.Length < TamanhoMinimoChave)
            {
                throw new InvalidOperationException(
                    "A chave 'Contas:ChaveCriptografia' (variável Contas__ChaveCriptografia) " +
                    $"precisa ter pelo menos {TamanhoMinimoChave} caracteres.");
            }

            _chave = SHA256.HashData(Encoding.UTF8.GetBytes(texto));
        }

        // A tela usa isto para saber se pode oferecer o campo "senha" e o botão "Mostrar".
        public bool EstaConfigurado => _chave != null;

        // Transforma a senha digitada no texto cifrado que vai para o banco.
        public string Proteger(string senha)
        {
            if (_chave == null)
                throw new InvalidOperationException("Chave de criptografia das contas não configurada.");

            var textoPuro = Encoding.UTF8.GetBytes(senha);
            var nonce = RandomNumberGenerator.GetBytes(TamanhoNonce); // novo a cada senha
            var cifrado = new byte[textoPuro.Length];
            var tag = new byte[TamanhoTag];

            using (var aes = new AesGcm(_chave, TamanhoTag))
            {
                aes.Encrypt(nonce, textoPuro, cifrado, tag);
            }

            // Monta o pacote final na ordem em que o Revelar espera encontrar.
            var pacote = new byte[1 + TamanhoNonce + TamanhoTag + cifrado.Length];
            pacote[0] = VersaoAtual;
            nonce.CopyTo(pacote, 1);
            tag.CopyTo(pacote, 1 + TamanhoNonce);
            cifrado.CopyTo(pacote, 1 + TamanhoNonce + TamanhoTag);

            return Convert.ToBase64String(pacote);
        }

        // Devolve a senha original. Retorna null se a chave estiver errada, o dado estiver
        // corrompido ou o formato for desconhecido (não lança exceção para não derrubar a tela).
        public string? Revelar(string? protegido)
        {
            if (_chave == null || string.IsNullOrWhiteSpace(protegido)) return null;

            try
            {
                var pacote = Convert.FromBase64String(protegido);
                if (pacote.Length < 1 + TamanhoNonce + TamanhoTag) return null;
                if (pacote[0] != VersaoAtual) return null; // formato que esta versão não conhece

                var nonce = pacote.AsSpan(1, TamanhoNonce);
                var tag = pacote.AsSpan(1 + TamanhoNonce, TamanhoTag);
                var cifrado = pacote.AsSpan(1 + TamanhoNonce + TamanhoTag);
                var textoPuro = new byte[cifrado.Length];

                using var aes = new AesGcm(_chave, TamanhoTag);
                aes.Decrypt(nonce, cifrado, tag, textoPuro);

                return Encoding.UTF8.GetString(textoPuro);
            }
            catch (FormatException) { return null; }        // não era Base64
            catch (CryptographicException) { return null; } // chave errada ou dado alterado
        }
    }
}
