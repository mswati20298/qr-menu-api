# QrMenu API. Build from the api repo root:  docker build -t qrenvo-api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY QrMenu.sln ./
COPY src/QrMenu.Domain/QrMenu.Domain.csproj src/QrMenu.Domain/
COPY src/QrMenu.Application/QrMenu.Application.csproj src/QrMenu.Application/
COPY src/QrMenu.Infrastructure/QrMenu.Infrastructure.csproj src/QrMenu.Infrastructure/
COPY src/QrMenu.Api/QrMenu.Api.csproj src/QrMenu.Api/
RUN dotnet restore src/QrMenu.Api/QrMenu.Api.csproj -r linux-x64 -p:PublishReadyToRun=true
COPY src ./src
# ReadyToRun: compiled ahead of time, so the API answers quickly right after a deploy instead of compiling on first use.
RUN dotnet publish src/QrMenu.Api/QrMenu.Api.csproj -c Release -o /app --no-restore -r linux-x64 --self-contained false -p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:8.0
# Fonts for QuestPDF invoices and QR cards.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fonts-dejavu-core libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Uploaded photos live in a volume mounted here; the non-root "app" user must own it.
RUN mkdir -p /app/wwwroot/uploads && chown -R app:app /app/wwwroot
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "QrMenu.Api.dll"]
