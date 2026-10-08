FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

# Copy csproj files and restore dependencies
COPY ["src/USCISFormTracker.Emailer/USCISFormTracker.Emailer.csproj", "src/USCISFormTracker.Emailer/"]
COPY ["src/USCISFormTracker.Dto/USCISFormTracker.Dto.csproj", "src/USCISFormTracker.Dto/"]
COPY ["src/USCISFormTracker.Formatting/USCISFormTracker.Formatting.csproj", "src/USCISFormTracker.Formatting/"]
COPY ["src/USCISFormTracker.Core/USCISFormTracker.Core.csproj", "src/USCISFormTracker.Core/"]
RUN dotnet restore "src/USCISFormTracker.Emailer/USCISFormTracker.Emailer.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/repo/src/USCISFormTracker.Emailer"
RUN dotnet build "USCISFormTracker.Emailer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "USCISFormTracker.Emailer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "USCISFormTracker.Emailer.dll"]
