FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FairShare.sln .
COPY src/FairShare.Domain/FairShare.Domain.csproj src/FairShare.Domain/
COPY src/FairShare.Application/FairShare.Application.csproj src/FairShare.Application/
COPY src/FairShare.Infrastructure/FairShare.Infrastructure.csproj src/FairShare.Infrastructure/
COPY src/FairShare.API/FairShare.API.csproj src/FairShare.API/
RUN dotnet restore src/FairShare.API/FairShare.API.csproj
COPY . .
RUN dotnet publish src/FairShare.API/FairShare.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FairShare.API.dll"]
