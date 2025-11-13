FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["USCISFormTracker.Emailer/USCISFormTracker.Emailer.csproj", "USCISFormTracker.Emailer/"]
COPY ["USCISFormTracker.Dto/USCISFormTracker.Dto.csproj", "USCISFormTracker.Dto/"]
RUN dotnet restore "USCISFormTracker.Emailer/USCISFormTracker.Emailer.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/USCISFormTracker.Emailer"
RUN dotnet build "USCISFormTracker.Emailer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "USCISFormTracker.Emailer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "USCISFormTracker.Emailer.dll"]
