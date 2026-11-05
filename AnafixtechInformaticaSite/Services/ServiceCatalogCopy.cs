using System.Globalization;
using System.Text;

namespace AnafixtechInformaticaSite.Services
{
    // Texto complementar do catálogo: o que está incluso e uma noção de prazo
    // para cada serviço. A descrição curta continua vindo do banco.
    public static class ServiceCatalogCopy
    {
        public readonly record struct Detalhe(string Inclui, string Prazo);

        private static readonly Dictionary<string, Detalhe> Itens = new(StringComparer.Ordinal)
        {
            ["configuracao de antivirus"] = new(
                "Instalação do antivírus, atualização das definições, verificação completa e remoção das ameaças encontradas. Se o programa exigir licença, ela é responsabilidade do cliente.",
                "Normalmente em 24 a 96 horas, dependendo do tamanho do disco e da quantidade de ameaças."),

            ["configuracao de backup automatico"] = new(
                "Definição do que será copiado, da frequência e do destino: HD externo, nuvem ou outro computador. Inclui um teste de restauração para confirmar que o backup funciona.",
                "Feito no atendimento, depois de definirmos onde o backup será guardado."),

            ["otimizacao de desempenho"] = new(
                "Verificação de programas de inicialização, atualizações, integridade do disco e uso de memória. Não inclui troca de peças.",
                "Normalmente em 24 a 96 horas. Se o problema for peça, o orçamento é separado."),

            ["substituicao de fonte de alimentacao"] = new(
                "Teste da fonte, substituição por um modelo compatível e teste de carga. A peça é cobrada conforme o modelo cotado.",
                "Depende da peça. O prazo é informado no orçamento antes do serviço."),

            ["backup e restauracao de dados"] = new(
                "Cópia dos arquivos indicados e restauração em outro disco ou pasta. Não recupera arquivos que já foram apagados.",
                "Prazo definido após avaliar o volume de dados e o estado do disco."),

            ["reparo e troca de placas (quando viavel)"] = new(
                "Teste com peças de reposição, reparo quando é viável ou substituição da placa. Se o conserto não compensar, você recebe o diagnóstico e o valor da avaliação é explicado antes.",
                "Depende da disponibilidade da peça e do resultado do diagnóstico."),

            ["atualizacao de drivers e bios"] = new(
                "Atualização dos drivers do fabricante e da BIOS, com conferência de versão e teste depois da gravação. A atualização é feita na bancada para reduzir o risco de interrupção.",
                "Feito na bancada. O tempo depende da atualização e do teste."),

            ["troca de pasta termica"] = new(
                "Desmontagem, limpeza da pasta antiga, aplicação de pasta nova e teste de temperatura. Indicado quando o notebook esquenta ou desliga sozinho.",
                "Normalmente em 24 a 96 horas, com o equipamento na bancada."),

            ["migracao de sistema para ssd"] = new(
                "Clonagem do sistema e dos arquivos para o SSD, ajuste da inicialização e teste. O SSD é cobrado à parte.",
                "Normalmente em 24 a 96 horas, se o SSD estiver disponível."),

            ["troca de teclado, touchpad e tela (notebooks)"] = new(
                "Substituição da peça danificada e teste das funções. A peça é cotada conforme o modelo do notebook.",
                "Depende do modelo e da disponibilidade da peça."),

            ["troca de cooler e dissipadores"] = new(
                "Substituição do cooler ou do dissipador e teste de rotação e temperatura. Indicado para ruído alto, vibração ou superaquecimento.",
                "Depende da peça. O prazo é informado no orçamento."),

            ["troca e expansao de memoria ram"] = new(
                "Verificação de compatibilidade, instalação do módulo e teste de reconhecimento. O módulo é cobrado à parte.",
                "Normalmente em 24 a 96 horas, se o módulo estiver disponível."),

            ["softwares de edicao (quando permitido por licenca)"] = new(
                "Instalação e configuração do programa com a licença do cliente. Não fornecemos licença e não instalamos versão pirata.",
                "Normalmente em 24 a 96 horas, após a licença ser apresentada."),

            ["limpeza interna completa (desktop/notebook)"] = new(
                "Desmontagem, limpeza de poeira, ventilação e contatos, remontagem e teste. A troca de pasta térmica pode ser combinada à parte.",
                "Normalmente em 24 a 96 horas, com o equipamento na bancada."),

            ["reducao de aquecimento excessivo"] = new(
                "Diagnóstico de temperatura, limpeza, ajuste de ventilação e verificação da pasta térmica. Se a causa for uma peça, o orçamento é separado.",
                "Normalmente em 24 a 96 horas, depois do diagnóstico."),

            ["troca de hd por ssd"] = new(
                "Substituição do HD por SSD e instalação do sistema. Quando o HD ainda funciona, os arquivos podem ser transferidos. O SSD é cobrado à parte.",
                "Normalmente em 24 a 96 horas, se o SSD estiver disponível."),

            ["backup de dados pessoais"] = new(
                "Cópia de fotos, documentos e outros arquivos indicados por você para um destino à sua escolha, com conferência do que foi copiado.",
                "Depende do volume de arquivos. O prazo é informado depois da avaliação."),

            ["recuperacao de arquivos apagados (casos simples)"] = new(
                "Tentativa de recuperação em casos simples. O equipamento fica parado para evitar sobrescrever os arquivos. O sucesso depende do estado do disco e não é garantido.",
                "Prazo definido após a avaliação. Não há garantia de recuperação."),

            ["limpeza de arquivos e programas desnecessarios"] = new(
                "Remoção de arquivos temporários, cache, programas não usados e verificação de espaço. Documentos pessoais só são apagados com a sua confirmação.",
                "Normalmente em 24 a 96 horas, com o equipamento na bancada."),

            ["clonagem de hd/ssd"] = new(
                "Cópia do disco para outro, por setores ou por arquivos, conforme o caso. Indicada para trocar o disco sem reinstalar tudo.",
                "Depende da capacidade do disco e do volume de dados."),

            ["instalacao e atualizacao de sistemas operacionais"] = new(
                "Instalação limpa ou atualização do Windows ou Linux, com drivers e teste. A licença do sistema é responsabilidade do cliente.",
                "Normalmente em 24 a 96 horas, com o equipamento na bancada."),

            ["instalacao de programas essenciais"] = new(
                "Navegador, leitor de PDF, compactador, reprodutor de mídia e outros programas gratuitos. Programas pagos exigem licença do cliente.",
                "Normalmente em 24 a 96 horas, com o equipamento na bancada."),

            ["pacote office"] = new(
                "Instalação e configuração do Microsoft Office ou de uma alternativa compatível, com a licença do cliente.",
                "Normalmente em 24 a 96 horas, após a licença ser apresentada."),

            ["correcao de erros de inicializacao"] = new(
                "Diagnóstico do erro, reparo do setor de inicialização ou restauração do sistema. Quando o disco está falhando, explicamos a limitação antes de continuar.",
                "Normalmente em 24 a 96 horas, depois do diagnóstico."),

            ["montagem de computadores personalizados"] = new(
                "Montagem conforme a lista de peças aprovada, organização dos cabos, instalação do sistema e teste de estabilidade. As peças não estão inclusas.",
                "Depende da entrega das peças e da instalação do sistema."),

            ["formatacao de computadores (windows/linux)"] = new(
                "Backup dos arquivos indicados, instalação limpa, drivers, atualizações e programas básicos. A licença do sistema é responsabilidade do cliente.",
                "Normalmente em 24 a 96 horas, com o equipamento na bancada.")
        };

        public static Detalhe Obter(string titulo)
        {
            var chave = Normalizar(titulo);
            if (Itens.TryGetValue(chave, out var detalhe))
                return detalhe;

            return new Detalhe(
                "O serviço descrito acima, com teste do equipamento antes da entrega. O escopo é confirmado no orçamento.",
                "O prazo é informado no orçamento, depois do diagnóstico.");
        }

        private static string Normalizar(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            var semAcento = texto.Normalize(NormalizationForm.FormD);
            var construtor = new StringBuilder(semAcento.Length);

            foreach (var caractere in semAcento)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(caractere) != UnicodeCategory.NonSpacingMark)
                    construtor.Append(char.ToLowerInvariant(caractere));
            }

            return construtor.ToString().Normalize(NormalizationForm.FormC).Trim();
        }
    }
}
