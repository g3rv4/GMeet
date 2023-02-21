# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:6.0.406-alpine3.16 AS builder
WORKDIR /src
COPY src /src/
RUN dotnet publish -c Release /src/GMeet.csproj -o /app

FROM mcr.microsoft.com/dotnet/aspnet:6.0.14-alpine3.16
VOLUME ["/data"]
COPY --from=builder /app /app
CMD ["/app/GMeet"]
