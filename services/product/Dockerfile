# syntax=docker/dockerfile:1
# Build context là thư mục gốc repo vì service tham chiếu tới shared/BuildingBlocks.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy trước file build props + csproj để tận dụng cache layer khi chỉ đổi code.
COPY Directory.Build.props Directory.Packages.props ./
COPY shared/ shared/
COPY services/product/src/ services/product/src/

RUN dotnet restore services/product/src/Product.Api/Product.Api.csproj
RUN dotnet publish services/product/src/Product.Api/Product.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Chạy bằng user không phải root.
RUN adduser --disabled-password --gecos "" --uid 5678 appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

EXPOSE 8080
ENTRYPOINT ["dotnet", "Product.Api.dll"]
