FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CompanyKnowledgeAssistant.API/CompanyKnowledgeAssistant.API.csproj", "CompanyKnowledgeAssistant.API/"]
COPY ["CompanyKnowledgeAssistant.Core/CompanyKnowledgeAssistant.Core.csproj", "CompanyKnowledgeAssistant.Core/"]
COPY ["CompanyKnowledgeAssistant.Infrastructure/CompanyKnowledgeAssistant.Infrastructure.csproj", "CompanyKnowledgeAssistant.Infrastructure/"]
RUN dotnet restore "CompanyKnowledgeAssistant.API/CompanyKnowledgeAssistant.API.csproj"
COPY . .
WORKDIR "/src/CompanyKnowledgeAssistant.API"
RUN dotnet build "CompanyKnowledgeAssistant.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CompanyKnowledgeAssistant.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CompanyKnowledgeAssistant.API.dll"]