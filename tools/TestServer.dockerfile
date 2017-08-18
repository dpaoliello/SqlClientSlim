FROM microsoft/mssql-server-windows-express
COPY SampleDBs/*.sql Setup-TestServer.ps1 C:/TestDBs/
RUN ["powershell.exe", "-File", "C:\\TestDBs\\Setup-TestServer.ps1"]