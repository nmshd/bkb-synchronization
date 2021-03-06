using Azure.Identity;
using Enmeshed.BuildingBlocks.API.Extensions;
using Enmeshed.Tooling.Extensions;
using Microsoft.AspNetCore;
using Synchronization.API;
using Synchronization.API.Extensions;
using Synchronization.Domain.Entities;
using Synchronization.Infrastructure.Persistence.Database;

CreateWebHostBuilder(args).Build()
    .MigrateDbContext<ApplicationDbContext>((dbContext, _) =>
    {
        CreateDatawalletsForIdentitiesWithoutDatawallet(dbContext);

        dbContext.SaveChanges();
    })
    .Run();


static void CreateDatawalletsForIdentitiesWithoutDatawallet(ApplicationDbContext dbContext)
{
    var modificationsWithoutDatawallet = dbContext.DatawalletModifications.Where(m => m.Datawallet == null).ToList();

    foreach (var modificationsByOwner in modificationsWithoutDatawallet.GroupBy(m => m.CreatedBy))
    {
        var datawallet = new Datawallet(new Datawallet.DatawalletVersion(0), modificationsByOwner.Key);

        foreach (var modification in modificationsByOwner)
        {
            datawallet.AddModification(modification);
        }

        dbContext.Datawallets.Add(datawallet);
    }
}

static IWebHostBuilder CreateWebHostBuilder(string[] args)
{
    return WebHost.CreateDefaultBuilder(args)
        .UseKestrel(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 128.Kibibytes();
        })
        .ConfigureAppConfiguration(AddAzureAppConfiguration)
        .UseStartup<Startup>();
}

static void AddAzureAppConfiguration(WebHostBuilderContext hostingContext, IConfigurationBuilder builder)
{
    var configuration = builder.Build();

    var azureAppConfigurationConfiguration = configuration.GetAzureAppConfigurationConfiguration();

    if (azureAppConfigurationConfiguration.Enabled)
        builder.AddAzureAppConfiguration(appConfigurationOptions =>
        {
            var credentials = new ManagedIdentityCredential();

            appConfigurationOptions
                .Connect(new Uri(azureAppConfigurationConfiguration.Endpoint), credentials)
                .ConfigureKeyVault(vaultOptions => { vaultOptions.SetCredential(credentials); })
                .Select("*", null)
                .Select("*", "Synchronization");
        });
}
