# ---- Stage 1: build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY marketWep.csproj .
RUN dotnet restore            # این لایه کش می‌شه تا وقتی csproj عوض نشده
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

# ---- Stage 2: runtime (فقط چیزی که برای اجرا لازمه) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "marketWep.dll"]