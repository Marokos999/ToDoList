# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore in its own layer so NuGet packages are cached until the csproj changes
COPY TodoApp/TodoApp.csproj TodoApp/
RUN dotnet restore TodoApp/TodoApp.csproj

COPY TodoApp/ TodoApp/
# No --no-restore: the SDK only adds the pack containing blazor.web.js once it sees .razor files, which the csproj-only restore above can't
RUN dotnet publish TodoApp/TodoApp.csproj -c Release -o /app/publish

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# QuestPDF (PDF report export) needs fontconfig and a font on Linux
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

# Writable dir for ASP.NET Data Protection keys (auth cookies survive container restarts when mounted as a volume)
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /home/app/.aspnet

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER app
ENTRYPOINT ["dotnet", "TodoApp.dll"]
