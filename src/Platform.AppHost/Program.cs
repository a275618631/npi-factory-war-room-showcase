var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var db = postgres.AddDatabase("Default");

builder.AddProject<Projects.Platform_Host>("platform-host")
    .WithReference(postgres)
    .WithReference(db)
    .WaitFor(postgres);

builder.Build().Run();


