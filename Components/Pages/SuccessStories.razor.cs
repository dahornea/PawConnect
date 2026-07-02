using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
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

namespace PawConnect.Components.Pages;

public partial class SuccessStories
{
    [Inject] private IDogService DogService { get; set; } = default!;

private List<Dog> _adoptedDogs = [];
    private bool _isLoading = true;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _adoptedDogs = await DogService.GetAdoptedDogsAsync();
        }
        catch
        {
            _error = "Success stories could not be loaded right now.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string? GetDogImageUrl(Dog dog)
    {
        return DogImageUrlValidator.GetPrimaryRealDogImageUrl(dog.Images);
    }
}

