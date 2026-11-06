# Acesse https://aka.ms/customizecontainer para saber como personalizar seu contêiner de depuração e como o Visual Studio usa este Dockerfile para criar suas imagens para uma depuração mais rápida.

# Esta fase é usada durante a execução no VS no modo rápido (Padrão para a configuração de Depuração)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Esta fase é usada para compilar o projeto de serviço
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["AnafixtechInformaticaSite/AnafixtechInformaticaSite.csproj", "AnafixtechInformaticaSite/"]
RUN dotnet restore "./AnafixtechInformaticaSite/AnafixtechInformaticaSite.csproj"
COPY . .
WORKDIR "/src/AnafixtechInformaticaSite"
RUN dotnet build "./AnafixtechInformaticaSite.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Esta fase é usada para publicar o projeto de serviço a ser copiado para a fase final
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AnafixtechInformaticaSite.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Esta fase é usada na produção ou quando executada no VS no modo normal (padrão quando não está usando a configuração de Depuração)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# A imagem roda com usuário sem privilégios (APP_UID) e o COPY acima cria os arquivos como root.
# A pasta de uploads precisa ser gravável por esse usuário, senão o envio de foto falha com 500.
USER root
RUN mkdir -p /app/wwwroot/uploads/requests && chown -R $APP_UID /app/wwwroot/uploads
USER $APP_UID

ENTRYPOINT ["dotnet", "AnafixtechInformaticaSite.dll"]