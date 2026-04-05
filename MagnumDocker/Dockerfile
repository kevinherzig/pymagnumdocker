FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY src/Magnum.Serial/Magnum.Serial.csproj src/Magnum.Serial/
COPY src/Magnum.Server/Magnum.Server.csproj src/Magnum.Server/
RUN dotnet restore src/Magnum.Server/Magnum.Server.csproj -a $TARGETARCH

COPY src/ src/
RUN dotnet publish src/Magnum.Server/Magnum.Server.csproj \
    -a $TARGETARCH \
    --no-restore \
    -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 17223
ENTRYPOINT ["dotnet", "Magnum.Server.dll"]
