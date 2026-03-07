FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["YogitaFashionAPI/YogitaFashionAPI/YogitaFashionAPI.csproj", "YogitaFashionAPI/YogitaFashionAPI/"]
RUN dotnet restore "YogitaFashionAPI/YogitaFashionAPI/YogitaFashionAPI.csproj"

COPY . .
RUN dotnet publish "YogitaFashionAPI/YogitaFashionAPI/YogitaFashionAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "YogitaFashionAPI.dll"]
