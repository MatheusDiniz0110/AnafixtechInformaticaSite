# Deploy — AnaFixTech

O site roda em ASP.NET Core 10 com PostgreSQL. O `render.yaml` cria tudo no Render:
Web Service via Docker + banco PostgreSQL.

## 1. Rodar local

Pré-requisitos: .NET 10 SDK e Docker.

```powershell
# Sobe o PostgreSQL local (porta 5432)
docker compose up -d db

# Configura os segredos locais (uma vez só, ficam fora do repositório)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=anafixtech;Username=anafixtech;Password=anafixtech_dev;" --project AnafixtechInformaticaSite
dotnet user-secrets set "Jwt:SecretKey" "<chave aleatória de 32+ caracteres>" --project AnafixtechInformaticaSite

# Cria/atualiza o schema
dotnet ef database update --project AnafixtechInformaticaSite

# Preenche serviços e números da home (só na primeira vez)
docker cp db/seed.sql anafixtech-db:/tmp/seed.sql
docker exec anafixtech-db psql -U anafixtech -d anafixtech -f /tmp/seed.sql

# Sobe o site
dotnet run --project AnafixtechInformaticaSite --launch-profile https
```

A connection string local **não fica no repositório**: vem dos user secrets e usa
usuário/senha só de desenvolvimento (`anafixtech` / `anafixtech_dev`), criados pelo
`docker-compose.yml`. O `dotnet ef` também lê os user secrets, então os comandos acima
funcionam sem variável de ambiente extra.

### Criar o primeiro administrador

Todo cadastro pelo site nasce como cliente comum (`isAdmin = false`). Depois de criar sua
conta em `/Account/Register`, promova-a:

```powershell
docker exec anafixtech-db psql -U anafixtech -d anafixtech -c "UPDATE \"Users\" SET \"isAdmin\" = true WHERE \"Email\" = 'seu@email.com';"
```

## 2. Deploy no Render (Blueprint)

1. Faça push do repositório no GitHub.
2. No Render: **New > Blueprint Instance** e selecione o repositório.
3. Confirme o `render.yaml`. Ele cria:
   - `anafixtech` (Web Service Docker, plano Free, região Virginia)
   - `anafixtech-db` (PostgreSQL Free, mesma região)
4. Clique em **Apply**.

O Render injeta a connection string no serviço e gera a `Jwt__SecretKey`. As migrations
são aplicadas automaticamente no startup (`Database__MigrateOnStartup=true`).

Depois do primeiro deploy, abra **Web Service > Environment** e confirme que existem as
variáveis abaixo. Se o serviço foi criado na mão (fora do Blueprint), elas não aparecem
sozinhas:

| Variável | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Internal Database URL do `anafixtech-db` |
| `Database__MigrateOnStartup` | `true` |
| `Jwt__SecretKey` | 32+ caracteres aleatórios (não troque depois) |
| `Admin__Email` | `matheus.diniz.t@outlook.com` |
| `Admin__Password` | senha do admin, mínimo 8 caracteres (só no painel) |

Ao alterar qualquer variável, use **Save and deploy** (não apenas "Save only"), senão a
instância que está rodando continua com o ambiente antigo. A aplicação também aceita a
variável `DATABASE_URL` como alternativa à `ConnectionStrings__DefaultConnection`.

O Render entrega a conexão como `postgres://usuario:senha@host/banco`. O `Program.cs`
converte esse formato para o formato chave=valor que o Npgsql entende, então **não é
necessário converter a URL na mão**. A variável `PORT` também é lida automaticamente.

### Dados iniciais e conta admin (automático)

Com `Database__MigrateOnStartup=true`, o startup faz três coisas:

1. aplica as migrations pendentes;
2. se a tabela `Services` estiver vazia, executa o `db/seed.sql` embutido no binário —
   entram os 26 serviços do catálogo e a linha de números da home;
3. se `Admin__Email` e `Admin__Password` estiverem definidos, cria a conta admin (ou
   redefine a senha, se a conta já existir).

Não é preciso rodar `psql` na máquina nem ter shell no Render. O seed é idempotente
(`ON CONFLICT DO NOTHING`): reiniciar o serviço não duplica nada.

Para trocar a senha do admin depois, basta alterar `Admin__Password` no painel e fazer um
novo deploy. Também é seguro remover a variável depois que a conta existe — ela não é
apagada.

Se preferir criar a conta pelo site: cadastre-se em `/Account/Register` usando exatamente o
e-mail configurado em `Admin__Email`. No próximo deploy/restart a conta é promovida a
administradora (mesmo sem `Admin__Password`).

### Preencher os dados manualmente (alternativa)

Se quiser aplicar o seed sem depender do startup, pegue a **External Database URL** em
`anafixtech-db > Connect` e rode:

```powershell
docker run --rm -v "${PWD}/db:/db" postgres:17-alpine psql "<EXTERNAL_DATABASE_URL>" -f /db/seed.sql
```

Confirme o acesso: `https://SEU-SERVICO.onrender.com`.

### Deploy manual (sem Blueprint)

Se preferir criar o serviço na mão:

| Campo | Valor |
|---|---|
| Language | Docker |
| Dockerfile Path | `AnafixtechInformaticaSite/Dockerfile` |
| Root Directory | vazio (raiz do repositório) |
| Instance Type | Free (ou pago) |
| Health Check Path | `/` |

Variáveis de ambiente:

| Variável | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Database__MigrateOnStartup` | `true` |
| `ConnectionStrings__DefaultConnection` | Internal/External Database URL do Postgres |
| `Jwt__SecretKey` | 32+ caracteres aleatórios |
| `Admin__Email` | `matheus.diniz.t@outlook.com` |
| `Admin__Password` | senha escolhida, mínimo 8 caracteres |

Crie também um **PostgreSQL** no Render, na mesma região do Web Service, para usar a
conexão pela rede interna.

## 3. Cuidados

- **Plano Free do Postgres expira 30 dias após a criação** e os dados são apagados. Para uso
  contínuo, mude o banco para um plano pago.
- **Web Service Free hiberna** após 15 minutos sem acesso. A primeira requisição seguinte
  demora cerca de um minuto.
- **Uploads são efêmeros**: as fotos de `wwwroot/uploads/requests` somem a cada deploy,
  restart ou hibernação. O `Dockerfile` cria essa pasta já com permissão de escrita para o
  usuário do contêiner (senão o envio de foto falharia), mas para manter as fotos é preciso
  um plano pago com disco persistente montado em `/app/wwwroot/uploads` (ajustando o dono da
  pasta) ou um storage externo.
- **Migrações em produção**: a imagem final não tem o SDK, por isso as migrations rodam via
  `Database__MigrateOnStartup=true`. Se desligar essa opção, rode
  `dotnet ef database update --connection "<EXTERNAL_DATABASE_URL>"` localmente antes do deploy.
  O seed automático e a criação do admin também só rodam com essa opção ligada.
- **Seed automático**: só executa quando a tabela `Services` está vazia. Depois disso, edições
  e exclusões feitas pela dashboard são respeitadas em novos deploys.
- **Trocar a `Jwt__SecretKey`** derruba todas as sessões ativas (os cookies antigos deixam de
  validar). É o comportamento esperado.
- **Nunca commite** a connection string de produção nem a chave JWT. Use as variáveis do
  Render.

## 4. Atualizar o site

```powershell
git add .
git commit -m "sua mensagem"
git push
```

O Render rebuilda e reimplanta automaticamente (autoDeploy). Se houver migration nova, ela é
aplicada no startup.

## 5. Problemas comuns no Render

### O site devolve erro 500 e o log mostra `Failed to connect to 127.0.0.1:5432`

A aplicação está lendo a conexão local em vez da conexão do Render, ou seja, a variável
`ConnectionStrings__DefaultConnection` não chegou ao serviço. No log aparece algo como:

```
An error occurred using the connection to database 'anafixtech' on server 'tcp://localhost:5432'.
```

Corrija em **Web Service > Environment**:

1. Adicione `ConnectionStrings__DefaultConnection` com a **Internal Database URL** do
   `anafixtech-db` (aba **Connect** do banco). Não use a External URL aqui.
2. Confira as outras três variáveis da tabela da seção 2.
3. Clique em **Save and deploy** e aguarde o novo deploy.
4. No log, procure a linha `PostgreSQL: <host>:5432/<banco> (...)`. O host deve ser interno
   do Render (`dpg-...`), nunca `localhost`.

Se a variável existir mas o log continuar igual, o deploy não foi refeito ou o serviço em
produção é outro (por exemplo, um serviço criado na mão em vez do Blueprint). Nesse caso,
confira o nome do serviço no topo do log e ajuste as variáveis nele.

A aplicação agora falha na inicialização com uma mensagem explícita quando a conexão aponta
para `localhost` em produção, em vez de subir e devolver 500 em todas as páginas.

### O log mostra `Name or service not known` para o host `dpg-...-a`

O hostname interno do Postgres só resolve entre serviços do Render na **mesma região** e no
**mesmo ambiente/projeto**. Confira a região do Web Service (**Settings > Region**) e a do
banco; se forem diferentes, o DNS privado não funciona.

Saídas:

1. Usar a **External Database URL** do banco na variável `ConnectionStrings__DefaultConnection`
   (funciona de qualquer lugar, apenas um pouco mais lenta); ou
2. recriar o banco na mesma região do serviço (o banco novo está vazio, então não se perde
   nada) e usar a Internal URL.

Esse hostname (`dpg-...-a`, sem domínio) nunca resolve a partir da sua máquina — isso é
esperado.
