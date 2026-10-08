FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /repo

# Copy csproj files and restore dependencies
COPY ["src/USCISFormTracker.Web/USCISFormTracker.Web.csproj", "src/USCISFormTracker.Web/"]
COPY ["src/USCISFormTracker.Core/USCISFormTracker.Core.csproj", "src/USCISFormTracker.Core/"]
COPY ["src/USCISFormTracker.Data/USCISFormTracker.Data.csproj", "src/USCISFormTracker.Data/"]
COPY ["src/USCISFormTracker.Formatting/USCISFormTracker.Formatting.csproj", "src/USCISFormTracker.Formatting/"]
COPY ["src/USCISFormTracker.Email/USCISFormTracker.Email.csproj", "src/USCISFormTracker.Email/"]
RUN dotnet restore "src/USCISFormTracker.Web/USCISFormTracker.Web.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/repo/src/USCISFormTracker.Web"
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
