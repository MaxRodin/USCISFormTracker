FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["USCISFormTracker.Api/USCISFormTracker.Api.csproj", "USCISFormTracker.Api/"]
COPY ["USCISFormTracker.Processor/USCISFormTracker.Processor.csproj", "USCISFormTracker.Processor/"]
COPY ["USCISFormTracker.Core/USCISFormTracker.Core.csproj", "USCISFormTracker.Core/"]
COPY ["USCISFormTracker.Dto/USCISFormTracker.Dto.csproj", "USCISFormTracker.Dto/"]
RUN dotnet restore "USCISFormTracker.Api/USCISFormTracker.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/USCISFormTracker.Api"
RUN dotnet build "USCISFormTracker.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "USCISFormTracker.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "USCISFormTracker.Api.dll"]
