Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd src\ProjectHub.API; dotnet run"

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd src\ProjectHub.Web; dotnet run"