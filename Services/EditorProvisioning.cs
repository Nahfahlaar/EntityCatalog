using EntityCatalog.Data;
using EntityCatalog.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EntityCatalog.Services;

public static class EditorProvisioning
{
    private const string EditorRole = "Editor";

    public static async Task CreateFirstEditorAsync(IServiceProvider services)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException("Editor provisioning requires interactive console input.");

        await using var scope = services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await db.Database.CanConnectAsync())
            throw new InvalidOperationException("The database is unavailable. Apply pending migrations before provisioning an Editor.");

        var role = await roleManager.FindByNameAsync(EditorRole);
        if (role is null)
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>
            {
                Id = Guid.NewGuid(),
                Name = EditorRole
            });
            EnsureSucceeded(roleResult, "Could not create the Editor role");
        }
        else if ((await userManager.GetUsersInRoleAsync(EditorRole)).Count > 0)
        {
            throw new InvalidOperationException(
                "An Editor account already exists. This command only provisions the first Editor.");
        }

        Console.Write("Editor username: ");
        var userName = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(userName))
            throw new InvalidOperationException("A username is required.");

        var existingUser = await userManager.FindByNameAsync(userName);
        if (existingUser is not null)
            throw new InvalidOperationException("That username already exists. This command does not promote existing users.");

        var password = ReadPassword("Password: ");
        var confirmation = ReadPassword("Confirm password: ");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            throw new InvalidOperationException("The passwords did not match.");

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        EnsureSucceeded(createResult, "Could not create the user");

        var assignmentResult = await userManager.AddToRoleAsync(user, EditorRole);
        EnsureSucceeded(assignmentResult, "The user was created, but assigning the Editor role failed");

        Console.WriteLine("Editor account created.");
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var password = new System.Text.StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                    Console.Write("\b \b");
                }
                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write('*');
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"{message}: {errors}");
    }
}
