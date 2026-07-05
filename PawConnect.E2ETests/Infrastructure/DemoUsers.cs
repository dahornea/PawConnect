using PawConnect.Data;

namespace PawConnect.E2ETests.Infrastructure;

public sealed record DemoUser(string Email, string Password, string DashboardPath, string DashboardHeading);

public static class DemoUsers
{
    public static readonly DemoUser Adopter = new(
        IdentitySeedData.AdopterDemoEmail,
        IdentitySeedData.AdopterDemoPassword,
        "/adopter/dashboard",
        "Welcome, Ana Ionescu");

    public static readonly DemoUser Shelter = new(
        IdentitySeedData.ShelterDemoEmail,
        IdentitySeedData.ShelterDemoPassword,
        "/shelter/dashboard",
        "Shelter Dashboard");

    public static readonly DemoUser Admin = new(
        IdentitySeedData.AdminDemoEmail,
        IdentitySeedData.AdminDemoPassword,
        "/admin/dashboard",
        "Admin Dashboard");
}
