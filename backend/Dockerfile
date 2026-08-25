# Development image. The source is bind mounted by compose and dotnet watch
# rebuilds on change, so this stage exists mainly to cache the NuGet restore.
FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src

# Restore first, with only the project files copied, so a source edit does not
# invalidate the package cache layer.
COPY Ocwip.slnx ./
COPY src/Ocwip.Api/Ocwip.Api.csproj src/Ocwip.Api/
COPY tests/Ocwip.Api.Tests/Ocwip.Api.Tests.csproj tests/Ocwip.Api.Tests/
RUN dotnet restore Ocwip.slnx

# Migration tooling lives in the image so nobody installs the SDK on the host.
# Version tracks the EF Core packages in Ocwip.Api.csproj.
RUN dotnet tool install --global dotnet-ef --version 10.0.4
ENV PATH="${PATH}:/root/.dotnet/tools"

COPY . .

EXPOSE 8080

CMD ["dotnet", "watch", "run", "--project", "src/Ocwip.Api/Ocwip.Api.csproj", "--no-launch-profile"]
