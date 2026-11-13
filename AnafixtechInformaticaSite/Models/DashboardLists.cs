namespace AnafixtechInformaticaSite.Models
{
    // Listas fixas usadas nos formulários (as mesmas opções do Base44).
    public static class DashboardLists
    {
        // Categorias possíveis para lançamentos de ENTRADA.
        public static readonly string[] CategoriasEntrada =
        {
            "Servico de Manutencao", "Upgrade de Hardware", "Recuperacao de Dados",
            "Formatacao e Reinstalacao", "Montagem de PC", "Suporte Remoto",
            "Comissao de Socio", "Bonus", "Outros (Entrada)"
        };

        // Categorias possíveis para lançamentos de SAÍDA.
        public static readonly string[] CategoriasSaida =
        {
            "Ferramentas", "Pecas e Componentes", "Salario e Comissao", "Marketing",
            "Aluguel e Contas", "Transporte", "Softwares", "Investimentos", "Outros (Saida)"
        };

        // Sócios que podem ser vinculados a um lançamento.
        public static readonly string[] Socios = { "Empresa", "Matheus", "Vinicius", "Marcel", "Joao Augusto" };

        // Categorias de peças do estoque.
        public static readonly string[] CategoriasEstoque =
        {
            "memoria", "armazenamento", "processador", "placa_mae", "fonte",
            "cooler", "tela", "teclado", "bateria", "cabo", "outro"
        };

        // Categorias (classes) dos serviços do catálogo. É só uma sugestão: o formulário
        // aceita qualquer texto, para não perder categorias antigas já gravadas.
        public static readonly string[] CategoriasServico =
        {
            "Software", "Hardware", "Dados", "Consultoria"
        };
    }
}
