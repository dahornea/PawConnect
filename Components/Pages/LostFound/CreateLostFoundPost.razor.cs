using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Components.Pages.LostFound;

public partial class CreateLostFoundPost
{
    [Inject] private ILostFoundPostService LostFoundPostService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "type")]
    public string? Type { get; set; }

    private readonly List<LostFoundPostImageInputDto> _images = [];
    private LostFoundPostType _postType = LostFoundPostType.Lost;
    private string? _title;
    private string? _description;
    private string? _dogName;
    private string? _breedText;
    private DogSize? _size;
    private string? _coatColor;
    private string? _distinctiveMarks;
    private DateTime? _lastSeenOrFoundDate = DateTime.Today;
    private string? _city = "Cluj-Napoca";
    private string? _neighborhood;
    private string? _addressOrAreaDescription;
    private double? _latitude;
    private double? _longitude;
    private string? _contactName;
    private string? _contactEmail;
    private string? _contactPhone;
    private bool _contactInfoPublic;
    private string? _newImageUrl;
    private string? _imageError;
    private string? _error;
    private bool _isSubmitting;

    protected override async Task OnInitializedAsync()
    {
        if (Enum.TryParse<LostFoundPostType>(Type, ignoreCase: true, out var type))
        {
            _postType = type;
        }

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _contactName = user.FindFirst("name")?.Value ?? user.Identity?.Name;
        _contactEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name;
    }

    private void AddImage()
    {
        _imageError = null;
        if (_images.Count >= PawConnect.Services.LostFoundPostService.MaxImagesPerPost)
        {
            _imageError = $"You can add at most {PawConnect.Services.LostFoundPostService.MaxImagesPerPost} images.";
            return;
        }

        if (!DogImageUrlValidator.TryNormalizeImageReference(_newImageUrl, out var normalizedImageUrl))
        {
            _imageError = DogImageUrlValidator.ValidationMessage;
            return;
        }

        if (_images.Any(image => string.Equals(image.ImageUrlOrPath, normalizedImageUrl, StringComparison.OrdinalIgnoreCase)))
        {
            _imageError = "This image is already added.";
            return;
        }

        _images.Add(new LostFoundPostImageInputDto(normalizedImageUrl, IsMain: _images.Count == 0));
        _newImageUrl = null;
    }

    private void RemoveImage(int index)
    {
        if (index < 0 || index >= _images.Count)
        {
            return;
        }

        var removedWasMain = _images[index].IsMain;
        _images.RemoveAt(index);
        if (removedWasMain && _images.Count > 0)
        {
            SetMainImage(0);
        }
    }

    private void SetMainImage(int index)
    {
        for (var i = 0; i < _images.Count; i++)
        {
            _images[i] = _images[i] with { IsMain = i == index };
        }
    }

    private async Task SubmitAsync()
    {
        if (_isSubmitting)
        {
            return;
        }

        _isSubmitting = true;
        _error = null;

        try
        {
            var userId = await GetCurrentUserIdAsync();
            await LostFoundPostService.CreatePostAsync(BuildRequest(), userId);
            Snackbar.Add("Lost and found post submitted for admin review.", Severity.Success);
            NavigationManager.NavigateTo("/lost-found");
        }
        catch (InvalidOperationException ex)
        {
            _error = ex.Message;
        }
        catch
        {
            _error = "The post could not be submitted right now.";
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private LostFoundPostCreateRequest BuildRequest()
    {
        return new LostFoundPostCreateRequest(
            _postType,
            _title ?? string.Empty,
            _description ?? string.Empty,
            _dogName,
            _breedText,
            _size,
            _coatColor,
            _distinctiveMarks,
            _lastSeenOrFoundDate,
            _city ?? string.Empty,
            _neighborhood,
            _addressOrAreaDescription,
            _latitude,
            _longitude,
            _contactName ?? string.Empty,
            _contactEmail ?? string.Empty,
            _contactPhone,
            _contactInfoPublic,
            _images);
    }

    private async Task<string> GetCurrentUserIdAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        return authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Current user could not be found.");
    }
}
