using Microsoft.AspNetCore.Mvc;
using PawConnect.DTOs.Api;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/shelters")]
[Produces("application/json")]
public class SheltersController(
    IShelterService shelterService,
    IDogService dogService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiPagedResult<ShelterListItemApiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiPagedResult<ShelterListItemApiDto>>> GetShelters(
        [FromQuery] string? city = null,
        [FromQuery] string? neighborhood = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24)
    {
        var shelters = await shelterService.GetAllSheltersAsync();

        if (!string.IsNullOrWhiteSpace(city))
        {
            shelters = shelters
                .Where(shelter => string.Equals(shelter.City, city.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(neighborhood))
        {
            shelters = shelters
                .Where(shelter => string.Equals(shelter.Neighborhood, neighborhood.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Ok(ApiPagedResult<ShelterListItemApiDto>.Create(
            shelters.Select(ApiDtoMapper.ToShelterListItem),
            page,
            pageSize));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ShelterDetailsApiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShelterDetailsApiDto>> GetShelter(int id)
    {
        var shelter = await shelterService.GetByIdAsync(id);
        if (shelter is null)
        {
            return NotFound(new ApiErrorResponse("Shelter was not found."));
        }

        shelter.Dogs = await dogService.SearchDogsAsync(
            searchTerm: null,
            breed: null,
            maxAge: null,
            size: null,
            location: null,
            status: null,
            shelterId: id);

        return Ok(ApiDtoMapper.ToShelterDetails(shelter));
    }
}
