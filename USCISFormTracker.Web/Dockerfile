FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["USCISFormTracker.Web/USCISFormTracker.Web.csproj", "USCISFormTracker.Web/"]
COPY ["USCISFormTracker.Core/USCISFormTracker.Core.csproj", "USCISFormTracker.Core/"]
COPY ["USCISFormTracker.Data/USCISFormTracker.Data.csproj", "USCISFormTracker.Data/"]
COPY ["USCISFormTracker.Dto/USCISFormTracker.Dto.csproj", "USCISFormTracker.Dto/"]
RUN dotnet restore "USCISFormTracker.Web/USCISFormTracker.Web.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/USCISFormTracker.Web"
RUN dotnet build "USCISFormTracker.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "USCISFormTracker.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "USCISFormTracker.Web.dll"]
