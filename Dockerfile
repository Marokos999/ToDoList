# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore in its own layer so NuGet packages are cached until the csproj changes
COPY TodoApp/TodoApp.csproj TodoApp/
RUN dotnet restore TodoApp/TodoApp.csproj

COPY TodoApp/ TodoApp/
RUN dotnet publish TodoApp/TodoApp.csproj -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Writable dir for ASP.NET Data Protection keys (auth cookies survive container restarts when mounted as a volume)
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys \
    && chown -R app:app /home/app/.aspnet

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

USER app
ENTRYPOINT ["dotnet", "TodoApp.dll"]
