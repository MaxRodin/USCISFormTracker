FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["USCISFormTracker.Processor/USCISFormTracker.Processor.csproj", "USCISFormTracker.Processor/"]
COPY ["USCISFormTracker.Core/USCISFormTracker.Core.csproj", "USCISFormTracker.Core/"]
COPY ["USCISFormTracker.Dto/USCISFormTracker.Dto.csproj", "USCISFormTracker.Dto/"]
RUN dotnet restore "USCISFormTracker.Processor/USCISFormTracker.Processor.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/USCISFormTracker.Processor"
RUN dotnet build "USCISFormTracker.Processor.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "USCISFormTracker.Processor.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create forms directory for downloaded PDFs
RUN mkdir -p /app/forms

ENTRYPOINT ["dotnet", "USCISFormTracker.Processor.dll"]
