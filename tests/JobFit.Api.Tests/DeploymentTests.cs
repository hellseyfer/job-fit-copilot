namespace JobFit.Api.Tests;

public class DeploymentTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Api_dockerfile_is_multistage_non_root_and_exposes_8080()
    {
        var dockerfile = File.ReadAllText(Path.Combine(RepoRoot, "src", "JobFit.Api", "Dockerfile"));
        Assert.Contains("FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build", dockerfile);
        Assert.Contains("FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime", dockerfile);
        Assert.Contains("USER $APP_UID", dockerfile);
        Assert.Contains("ASPNETCORE_HTTP_PORTS=8080", dockerfile);
        Assert.Contains("http://localhost:8080/alive", dockerfile);
    }

    [Fact]
    public void Web_dockerfile_and_nginx_proxy_api_and_health_endpoints()
    {
        var dockerfile = File.ReadAllText(Path.Combine(RepoRoot, "src", "JobFit.Web", "Dockerfile"));
        Assert.Contains("FROM node:22-alpine AS build", dockerfile);
        Assert.Contains("FROM nginx:alpine AS runtime", dockerfile);
        Assert.Contains("API_ORIGIN=http://api:8080", dockerfile);
        Assert.Contains("/etc/nginx/templates/default.conf.template", dockerfile);
        Assert.Contains("dist/job-fit-web/browser", dockerfile);

        var nginx = File.ReadAllText(Path.Combine(RepoRoot, "src", "JobFit.Web", "nginx.conf"));
        Assert.Contains("location /api/", nginx);
        Assert.Contains("proxy_pass ${API_ORIGIN};", nginx);
        Assert.Contains("location = /health", nginx);
        Assert.Contains("location = /alive", nginx);
    }

    [Fact]
    public void Compose_file_orchestrates_postgres_api_and_web_with_health_gates()
    {
        var compose = File.ReadAllText(Path.Combine(RepoRoot, "compose.yaml"));
        Assert.Contains("pgvector/pgvector:pg17", compose);
        Assert.Contains("condition: service_healthy", compose);
        Assert.Contains("src/JobFit.Api/Dockerfile", compose);
        Assert.Contains("profiles: [\"ollama\"]", compose);
        Assert.DoesNotContain("sk-", compose);
    }

    [Fact]
    public void Ci_workflow_includes_container_build_job()
    {
        var ci = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "ci.yml"));
        Assert.Contains("container-build:", ci);
        Assert.Contains("docker build -f src/JobFit.Api/Dockerfile", ci);
        Assert.Contains("docker build -f src/JobFit.Web/Dockerfile", ci);
    }

    [Fact]
    public void Ghcr_publish_workflow_and_azure_deploy_script_are_configured()
    {
        var publish = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "publish-images.yml"));
        Assert.Contains("ghcr.io", publish);
        Assert.Contains("job-fit-copilot-api", publish);
        Assert.Contains("job-fit-copilot-web", publish);

        var script = File.ReadAllText(Path.Combine(RepoRoot, "deploy", "azure", "deploy.sh"));
        Assert.Contains("NEON_CONNECTION_STRING", script);
        Assert.Contains("OPENAI_API_KEY", script);
        Assert.Contains("--min-replicas 0 --max-replicas 1", script);
        Assert.Contains("API_ORIGIN", script);
    }
}
