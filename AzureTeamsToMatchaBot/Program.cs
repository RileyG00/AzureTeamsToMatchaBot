using MatchaRunner.Configuration;
using MatchaRunner.Constants;
using MatchaRunner.Handlers;
using Microsoft.Teams.Api.Auth;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Activities;
using Microsoft.Teams.Apps.Activities.Invokes;
using Microsoft.Teams.Apps.Extensions;
using Microsoft.Teams.Plugins.AspNetCore.Extensions;


//-----------------------------------------------------------
// Build the Web Application Builder and Configs
//-----------------------------------------------------------
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//-----------------------------------------------------------
// Add Site Configurations
//-----------------------------------------------------------
string appDir = AppContext.BaseDirectory;
string currentEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.ToLower() ?? "production";
const string appSettings = "appsettings.json";
const string appSettingsDev = "appsettings.development.json";
const string appSettingsPlayground = "appsettings.playground.json";

string appSettingsPath = Path.Combine(appDir, appSettings);
string appSettingsDevPath = Path.Combine(appDir, appSettingsDev);
string appSettingsPlaygroundPath = Path.Combine(appDir, appSettingsPlayground);


IConfigurationRoot configurationBuilder = new ConfigurationBuilder()
	.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true)
	.AddJsonFile(appSettingsDevPath, optional: true, reloadOnChange: true)
	.AddJsonFile(appSettingsPlaygroundPath, optional: true, reloadOnChange: true)
	.Build();


IConfigurationSection matchaSection = builder.Configuration.GetSection("Matcha");
IConfigurationSection teamsSection = builder.Configuration.GetSection("Teams");

builder.Services.Configure<Matcha>(matchaSection);
builder.Services.Configure<Teams>(teamsSection);

Matcha matchSettings = matchaSection.Get<Matcha>() ?? throw new Exception("Matcha settings must be provided.");
Teams teamsSettings = teamsSection.Get<Teams>() ?? throw new Exception("Teams settings must be provided.");

//-----------------------------------------------------------
// Add Services
//-----------------------------------------------------------
builder.Services.AddSingleton<IBotMessageHandler, BotMessageHandler>();
builder.Services.AddHttpClient(ApiNamedClients.Matcha, options =>
{
	options.BaseAddress = new Uri(matchSettings.BaseUrl);
	options.DefaultRequestHeaders.Add("x-api-key", matchSettings.DesignStudioApiKey);
	options.DefaultRequestHeaders.Add("User-Agent", "MatchaProductTeamAnalysis");
});


//-----------------------------------------------------------
// Build the App Builder
//-----------------------------------------------------------
AppBuilder appBuilder = App.Builder();


//-----------------------------------------------------------
// Add Credentials and Teams
//-----------------------------------------------------------
if (teamsSettings.BotType == "SingleTenant" && !string.IsNullOrWhiteSpace(teamsSettings.ClientId))
{
	appBuilder.AddCredentials(new ClientCredentials(
		teamsSettings.ClientId,
		teamsSettings.ClientSecret,
		teamsSettings.TenantId
	));
}


builder.AddTeams(appBuilder);


//-----------------------------------------------------------
// Build Application
//-----------------------------------------------------------
WebApplication app = builder.Build();

App teamsApp = app.UseTeams();


//-----------------------------------------------------------
// Handle Incoming Chat Messages
//-----------------------------------------------------------
IBotMessageHandler botMessageHandler = app.Services.GetRequiredService<IBotMessageHandler>();

teamsApp.OnMessage(async (context, cancellationToken) =>
{
	try
	{
		await botMessageHandler.HandleMessageAsync(context, cancellationToken);
	}
	catch (Exception ex)
	{
		await context.Send($"[PROGRAM FILE ERROR]: Critical Error: {ex.Message} \n\n StackTrace: {ex.StackTrace}", cancellationToken);
	}
});

teamsApp.OnFileConsent(async (context, cancellationToken) =>
{
	try
	{
		await botMessageHandler.HandleFileConsentAsync(context, cancellationToken);
	}
	catch (Exception ex)
	{
		await context.Send($"[PROGRAM FILE ERROR]: Critical Error: {ex.Message} \n\n StackTrace: {ex.StackTrace}", cancellationToken);
	}
});

app.Run();