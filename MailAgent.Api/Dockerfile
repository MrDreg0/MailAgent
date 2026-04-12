FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MailAgent.Api/MailAgent.Api.csproj MailAgent.Api/
COPY MailAgent.Application/MailAgent.Application.csproj MailAgent.Application/
COPY MailAgent.Database/MailAgent.Database.csproj MailAgent.Database/
COPY MailAgent.Database.PostgreSql/MailAgent.Database.PostgreSql.csproj MailAgent.Database.PostgreSql/
COPY MailAgent.Mail/MailAgent.Mail.csproj MailAgent.Mail/
COPY MailAgent.Api.Contracts/MailAgent.Api.Contracts.csproj MailAgent.Api.Contracts/

RUN dotnet restore MailAgent.Api/MailAgent.Api.csproj

COPY MailAgent.Api/ MailAgent.Api/
COPY MailAgent.Application/ MailAgent.Application/
COPY MailAgent.Database/ MailAgent.Database/
COPY MailAgent.Database.PostgreSql/ MailAgent.Database.PostgreSql/
COPY MailAgent.Mail/ MailAgent.Mail/
COPY MailAgent.Api.Contracts/ MailAgent.Api.Contracts/

RUN dotnet publish MailAgent.Api/MailAgent.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
  && apt-get install -y --no-install-recommends libgssapi-krb5-2 gss-ntlmssp \
  && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "MailAgent.Api.dll"]
