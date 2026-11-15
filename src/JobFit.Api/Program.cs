using System.Text.Json;
using System.Text.Json.Serialization;
using JobFit.Api.Analysis;
using JobFit.Api.CoverLetters;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Knowledge;
using JobFit.Api.Llm;
using JobFit.Api.Requirements;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDataSource("jobfit", configureDataSourceBuilder: dataSource => dataSource.UseVector());
builder.AddJobFitChatClient();
builder.AddJobFitEmbeddingGenerator();
builder.AddKnowledgeIngestion();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower)));
builder.Services.AddSingleton<RequirementExtractor>();
builder.Services.AddSingleton<EvidenceRetriever>();
builder.Services.AddSingleton<FitJudge>();
builder.Services.AddSingleton<FitScoringService>();
builder.Services.Configure<WorkflowOptions>(builder.Configuration.GetSection("Workflow"));
builder.Services.AddSingleton<StudyPlanner>();
builder.Services.AddSingleton<CoverLetterWriter>();
builder.Services.AddSingleton<CoverLetterVerifier>();
builder.Services.AddSingleton<JobAnalysisWorkflow>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();
app.MapRequirementEndpoints();
app.MapKnowledgeEndpoints();
app.MapEvidenceEndpoints();
app.MapFitEndpoints();
app.MapAnalysisEndpoints();

app.Run();
