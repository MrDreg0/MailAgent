FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MailAgent.Web/MailAgent.Web.csproj MailAgent.Web/
COPY MailAgent.Application/MailAgent.Application.csproj MailAgent.Application/
COPY MailAgent.Database/MailAgent.Database.csproj MailAgent.Database/
COPY MailAgent.Database.PostgreSql/MailAgent.Database.PostgreSql.csproj MailAgent.Database.PostgreSql/

RUN dotnet restore MailAgent.Web/MailAgent.Web.csproj

COPY MailAgent.Web/ MailAgent.Web/
COPY MailAgent.Application/ MailAgent.Application/
COPY MailAgent.Database/ MailAgent.Database/
COPY MailAgent.Database.PostgreSql/ MailAgent.Database.PostgreSql/

RUN dotnet publish MailAgent.Web/MailAgent.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
  && apt-get install -y --no-install-recommends libgssapi-krb5-2 gss-ntlmssp \
  && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "MailAgent.Web.dll"]
