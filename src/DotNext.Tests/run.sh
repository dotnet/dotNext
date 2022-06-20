# dotnet tool install -g dotnet-reportgenerator-globaltool
# reportgenerator -reports:"coverage.info" -targetdir:"output"
dotnet test --collect:"XPlat Code Coverage" /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=./TestResults/lcov.info