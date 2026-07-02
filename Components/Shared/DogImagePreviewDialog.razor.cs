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

namespace PawConnect.Components.Shared;

public partial class DogImagePreviewDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    public string DogName { get; set; } = "Selected dog";

    [Parameter]
    public string FormattedBreed { get; set; } = "Unknown";

    [Parameter]
    public IReadOnlyList<DogImage> Images { get; set; } = [];

    [Parameter]
    public int InitialIndex { get; set; }

    private int _selectedIndex;
    private bool _hasInitialized;
    private readonly HashSet<string> _failedImageUrls = new(StringComparer.OrdinalIgnoreCase);

    protected override void OnParametersSet()
    {
        if (_hasInitialized)
        {
            return;
        }

        var displayImages = GetDisplayImages();
        _selectedIndex = displayImages.Count == 0
            ? 0
            : Math.Clamp(InitialIndex, 0, displayImages.Count - 1);
        _hasInitialized = true;
    }

    private void SelectImage(int index)
    {
        var imageCount = GetDisplayImages().Count;
        if (imageCount == 0)
        {
            return;
        }

        _selectedIndex = Math.Clamp(index, 0, imageCount - 1);
    }

    private void ShowPreviousImage()
    {
        var imageCount = GetDisplayImages().Count;
        if (imageCount <= 1)
        {
            return;
        }

        _selectedIndex = (_selectedIndex - 1 + imageCount) % imageCount;
    }

    private void ShowNextImage()
    {
        var imageCount = GetDisplayImages().Count;
        if (imageCount <= 1)
        {
            return;
        }

        _selectedIndex = (_selectedIndex + 1) % imageCount;
    }

    private void HandleKeyDown(KeyboardEventArgs args)
    {
        switch (args.Key)
        {
            case "ArrowLeft":
                ShowPreviousImage();
                break;
            case "ArrowRight":
                ShowNextImage();
                break;
            case "Escape":
                Close();
                break;
        }
    }

    private void Close()
    {
        MudDialog.Close();
    }

    private List<DogImage> GetDisplayImages()
    {
        return DogImageUrlValidator.GetRealDogImages(Images, _failedImageUrls);
    }

    private int GetSafeSelectedImageIndex(int imageCount)
    {
        return imageCount <= 0 ? 0 : Math.Clamp(_selectedIndex, 0, imageCount - 1);
    }

    private void MarkImageUnavailable(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }

        _failedImageUrls.Add(imageUrl.Trim());
        _selectedIndex = GetSafeSelectedImageIndex(GetDisplayImages().Count);
    }

    private string GetSelectedImageAlt(int photoNumber)
    {
        return $"Photo {photoNumber} of {DogName}, {FormattedBreed}";
    }
}


