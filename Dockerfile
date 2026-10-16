# digitestCRM API — Railway (ve genel Docker) için imaj
# Yerel deneme: docker build -t digitestcrm-api .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY DigitestCrm.Api.csproj .
RUN dotnet restore DigitestCrm.Api.csproj
COPY . .
RUN dotnet publish DigitestCrm.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Railway portu PORT değişkeniyle verir; yoksa 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet DigitestCrm.Api.dll"]
