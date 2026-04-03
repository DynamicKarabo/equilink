FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EquiLink.sln ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Api/Api.csproj src/Api/

RUN dotnet restore

COPY src/Domain/ src/Domain/
COPY src/Shared/ src/Shared/
COPY src/Infrastructure/ src/Infrastructure/
COPY src/Api/ src/Api/

RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV EQUILINK_ROLE=api
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["sh", "-c", "\
  case \"$EQUILINK_ROLE\" in \
    api) \
      echo \"Starting as API role\" && \
      exec dotnet Api.dll ;; \
    consumer) \
      echo \"Starting as Consumer role\" && \
      exec dotnet Api.dll --consumer ;; \
    migrations) \
      echo \"Running migrations\" && \
      exec dotnet Api.dll --migrations ;; \
    *) \
      echo \"Unknown role: $EQUILINK_ROLE. Defaulting to API.\" && \
      exec dotnet Api.dll ;; \
  esac \
"]
