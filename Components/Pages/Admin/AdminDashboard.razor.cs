using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using MudBlazor;
using PawConnect.Components.Shared;
using PawConnect.Data;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.Admin;

public partial class AdminDashboard
{
    [Inject] private UserManager<ApplicationUser> UserManager { get; set; } = default!;
    [Inject] private IShelterService ShelterService { get; set; } = default!;
    [Inject] private IDogService DogService { get; set; } = default!;
    [Inject] private IAdoptionRequestService AdoptionRequestService { get; set; } = default!;
    [Inject] private IResourceStockService ResourceStockService { get; set; } = default!;

private bool _isLoading = true;
    private string? _error;
    private int _totalUsers;
    private int _totalShelters;
    private int _totalDogs;
    private int _availableDogs;
    private int _adoptedDogs;
    private int _pendingRequests;
    private int _lowStockResources;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var shelters = await ShelterService.GetAllSheltersAsync();
            var dogs = await DogService.GetAllDogsForAdminAsync();
            var requests = await AdoptionRequestService.GetAllAsync();
            var resources = await ResourceStockService.GetAllAsync();

            _totalUsers = await UserManager.Users.CountAsync();
            _totalShelters = shelters.Count;
            _totalDogs = dogs.Count;
            _availableDogs = dogs.Count(d => d.Status == DogStatus.Available);
            _adoptedDogs = dogs.Count(d => d.Status == DogStatus.Adopted);
            _pendingRequests = requests.Count(r => r.Status == AdoptionRequestStatus.Pending);
            _lowStockResources = resources.Count(r => r.Quantity <= r.LowStockThreshold);
        }
        catch
        {
            _error = "Admin dashboard data could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }
}

