FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish "src/Services/ITM-Tickets Global.Inventory.Api/ITM-Tickets Global.Inventory.Api.csproj" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
EXPOSE 5002
ENTRYPOINT ["dotnet", "ITM-Tickets Global.Inventory.Api.dll"]
