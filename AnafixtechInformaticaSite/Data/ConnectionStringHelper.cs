using Npgsql;

namespace AnafixtechInformaticaSite.Data
{
    public static class ConnectionStringHelper
    {
        // O Render entrega a conexão no formato postgres://usuario:senha@host/banco?sslmode=require.
        // O Npgsql só entende o formato chave=valor, então convertemos quando necessário.
        // Strings que já estão no formato chave=valor passam sem alteração.
        public static string Normalizar(string valor)
        {
            if (!valor.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
                !valor.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                return valor;
            }

            var uri = new Uri(valor);
            var credenciais = uri.UserInfo.Split(':', 2);

            var cs = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 5432 : uri.Port,
                Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
                Username = Uri.UnescapeDataString(credenciais[0]),
                Password = credenciais.Length > 1 ? Uri.UnescapeDataString(credenciais[1]) : null
            };

            // Parâmetros da query (sslmode=require, por exemplo).
            foreach (var par in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var partes = par.Split('=', 2);
                var nome = Uri.UnescapeDataString(partes[0]);
                var conteudo = partes.Length > 1 ? Uri.UnescapeDataString(partes[1]) : string.Empty;

                if (nome.Equals("sslmode", StringComparison.OrdinalIgnoreCase) &&
                    Enum.TryParse<SslMode>(conteudo, true, out var modoSsl))
                {
                    cs.SslMode = modoSsl;
                }
            }

            return cs.ConnectionString;
        }
    }
}
