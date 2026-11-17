using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace Platform.Host;

public class OpenIddictDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly IdentityUserManager _userManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IConfiguration _configuration;

    public OpenIddictDataSeedContributor(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        IdentityUserManager userManager,
        IGuidGenerator guidGenerator,
        IConfiguration configuration)
    {
        _applicationManager = applicationManager;
        _scopeManager = scopeManager;
        _userManager = userManager;
        _guidGenerator = guidGenerator;
        _configuration = configuration;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        await SeedScopesAsync();
        await SeedApplicationsAsync();
        await SeedDemoUserAsync();
    }

    private async Task SeedScopesAsync()
    {
        var scopes = new[] { "openid", "profile", "email", "roles", "npi" };
        foreach (var scope in scopes)
        {
            if (await _scopeManager.FindByNameAsync(scope) == null)
            {
                await _scopeManager.CreateAsync(new OpenIddictScopeDescriptor
                {
                    Name = scope,
                    DisplayName = $"{scope} scope"
                });
            }
        }
    }

    private async Task SeedApplicationsAsync()
    {
        const string clientId = "npi-web";
        if (await _applicationManager.FindByClientIdAsync(clientId) == null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = "NPI Web Frontend",
                ClientType = OpenIddictConstants.ClientTypes.Public,
                ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
                RedirectUris =
                {
                    new Uri("http://localhost:5173/callback"),
                    new Uri("http://localhost:5173")
                },
                PostLogoutRedirectUris =
                {
                    new Uri("http://localhost:5173"),
                    new Uri("http://localhost:5173/login")
                },
                Permissions =
                {
                    OpenIddictConstants.Permissions.Endpoints.Authorization,
                    OpenIddictConstants.Permissions.Endpoints.Token,
                    OpenIddictConstants.Permissions.Endpoints.EndSession,
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                    OpenIddictConstants.Permissions.ResponseTypes.Code,
                    OpenIddictConstants.Permissions.Scopes.Email,
                    OpenIddictConstants.Permissions.Scopes.Profile,
                    OpenIddictConstants.Permissions.Scopes.Roles,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddictConstants.Permissions.Prefixes.Scope + "npi"
                },
                Requirements =
                {
                    OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange
                }
            };

            await _applicationManager.CreateAsync(descriptor);
        }
    }

    private async Task SeedDemoUserAsync()
    {
        const string username = "demo";
        var user = await _userManager.FindByNameAsync(username);
        if (user == null)
        {
            user = new IdentityUser(_guidGenerator.Create(), username, "demo@npi.local")
            {
                Name = "demo"
            };
            user.SetEmailConfirmed(true);
            user.SetIsActive(true);
            var password = _configuration["DEMO_USER_PASSWORD"];
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Set DEMO_USER_PASSWORD to a local demo-only value before starting the showcase.");
            }

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create demo user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
    }
}





