var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
    .WithDataVolume();

var database = postgres.AddDatabase("jobfit");

builder.AddProject<Projects.JobFit_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health", endpointName: "http");

builder.Build().Run();
