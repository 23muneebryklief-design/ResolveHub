# ResolveHub

Customer Support and Service Request Management Platform.

## Technology Stack

- ASP.NET Core Web API
- .NET 10
- C#
- Swashbuckle / Swagger

## Prerequisites

- .NET 10 SDK

## Project Structure

ResolveHub/
|-- src/
|   |-- ResolveHub.Api/
|-- ResolveHub.slnx
|-- .gitignore
|-- README.md

## Getting Started

### Restore dependencies

dotnet restore .\ResolveHub.slnx

### Build the solution

dotnet build .\ResolveHub.slnx

### Run the API

dotnet run --project .\src\ResolveHub.Api\ResolveHub.Api.csproj --launch-profile https

## API Documentation

https://localhost:7257/swagger

## Health Check

https://localhost:7257/health

## Environment

Development
