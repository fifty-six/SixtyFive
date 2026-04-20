# See: https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /source

# Restore and publish separately so we can
# change code witohut doing a restore again
COPY --link ./SixtyFive/*.csproj .
RUN dotnet restore -a $TARGETARCH

COPY --link ./SixtyFive/. .
RUN dotnet publish -a $TARGETARCH --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --link --from=build /app .
USER $APP_UID
ENTRYPOINT ["./SixtyFive"]
