# Build
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /src
COPY . .
RUN dotnet restore "Synchronization.API/Synchronization.API.csproj"

WORKDIR "/src/Synchronization.API"
RUN dotnet publish --configuration Release --output /app --no-restore

# Run	
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine
EXPOSE 80
WORKDIR /app
ENV Logging__Console__FormatterName=
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
RUN apk add icu-libs
COPY --from=build-env /app .

LABEL org.opencontainers.image.source = "https://github.com/nmshd/bkb-synchronization"

ENTRYPOINT ["dotnet", "Synchronization.API.dll"]
