# Stage 1: Build Frontend
FROM node:20-alpine AS build-frontend
WORKDIR /app/frontend
COPY frontend/package*.json ./
RUN npm install
COPY frontend/ ./
# vite.config.ts outputs to ../backend/src/BetBuilder.Api/wwwroot
RUN mkdir -p /app/backend/src/BetBuilder.Api/wwwroot
RUN npm run build

# Stage 2: Build Backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-backend
WORKDIR /src

# Copy project files
COPY backend/src/BetBuilder.Api/BetBuilder.Api.csproj ./backend/src/BetBuilder.Api/
COPY backend/src/BetBuilder.Application/BetBuilder.Application.csproj ./backend/src/BetBuilder.Application/
COPY backend/src/BetBuilder.Domain/BetBuilder.Domain.csproj ./backend/src/BetBuilder.Domain/
COPY backend/src/BetBuilder.Infrastructure/BetBuilder.Infrastructure.csproj ./backend/src/BetBuilder.Infrastructure/
COPY backend/src/BetBuilder.MathEngine/BetBuilder.MathEngine.csproj ./backend/src/BetBuilder.MathEngine/

# Restore dependencies
RUN dotnet restore backend/src/BetBuilder.Api/BetBuilder.Api.csproj

# Copy the rest of the backend source code
COPY backend/ ./backend/

# Overwrite wwwroot with the fresh frontend build
COPY --from=build-frontend /app/backend/src/BetBuilder.Api/wwwroot ./backend/src/BetBuilder.Api/wwwroot

# Publish
WORKDIR /src/backend/src/BetBuilder.Api
RUN dotnet publish BetBuilder.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Expose port
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build-backend /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "BetBuilder.Api.dll"]
