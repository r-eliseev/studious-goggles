FROM node:22.23.3-bookworm-slim AS frontend
WORKDIR /src/ImageGen/ImageGen.Web/ClientApp
COPY ImageGen/ImageGen.Web/ClientApp/package.json ImageGen/ImageGen.Web/ClientApp/package-lock.json ./
RUN npm ci
COPY ImageGen/ImageGen.Web/ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble AS publish
WORKDIR /src
COPY . ./
COPY --from=frontend /src/ImageGen/ImageGen.Web/wwwroot ./ImageGen/ImageGen.Web/wwwroot
RUN dotnet publish ImageGen/ImageGen.Web/ImageGen.Web.csproj --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install --no-install-recommends --yes curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data \
    && chown "$APP_UID:$APP_UID" /data
COPY --from=publish /app/publish/ ./
ENV IMAGEGEN_DATA_DIR=/data \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
VOLUME ["/data"]
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=3s --start-period=15s --retries=3 CMD ["curl", "--fail", "--silent", "http://127.0.0.1:8080/api/health"]
ENTRYPOINT ["dotnet", "ImageGen.Web.dll"]
