FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["MarketingFloy.ApiService/MarketingFloy.ApiService.csproj", "MarketingFloy.ApiService/"]
COPY ["MarketingFloy.ServiceDefaults/MarketingFloy.ServiceDefaults.csproj", "MarketingFloy.ServiceDefaults/"]
RUN dotnet restore "MarketingFloy.ApiService/MarketingFloy.ApiService.csproj"

COPY . .
WORKDIR "/src/MarketingFloy.ApiService"
RUN dotnet build "MarketingFloy.ApiService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MarketingFloy.ApiService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN mkdir -p /app/uploads

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "MarketingFloy.ApiService.dll"]
