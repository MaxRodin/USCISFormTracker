FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

# Copy csproj files and restore dependencies
COPY ["src/USCISFormTracker.Processor/USCISFormTracker.Processor.csproj", "src/USCISFormTracker.Processor/"]
COPY ["src/USCISFormTracker.Core/USCISFormTracker.Core.csproj", "src/USCISFormTracker.Core/"]
COPY ["src/USCISFormTracker.Data/USCISFormTracker.Data.csproj", "src/USCISFormTracker.Data/"]
COPY ["src/USCISFormTracker.Dto/USCISFormTracker.Dto.csproj", "src/USCISFormTracker.Dto/"]
RUN dotnet restore "src/USCISFormTracker.Processor/USCISFormTracker.Processor.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/repo/src/USCISFormTracker.Processor"
RUN dotnet build "USCISFormTracker.Processor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "USCISFormTracker.Processor.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Root directory for downloaded PDFs (mounted as a volume by docker-compose)
RUN mkdir -p /app/forms

ENTRYPOINT ["dotnet", "USCISFormTracker.Processor.dll"]
