using Microsoft.AspNetCore.Mvc;
using PawConnect.DTOs.Api;
using PawConnect.Entities;
using PawConnect.Services;

namespace PawConnect.Controllers.Api.V1;

[ApiController]
[Route("api/v1/dogs")]
[Produces("application/json")]
public class DogsController(IDogService dogService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiPagedResult<DogListItemApiDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiPagedResult<DogListItemApiDto>>> GetDogs(
        [FromQuery] string? search = null,
        [FromQuery] string? breed = null,
        [FromQuery] int? maxAge = null,
        [FromQuery] DogSize? size = null,
        [FromQuery] string? location = null,
        [FromQuery] DogStatus? status = null,
        [FromQuery] DogSortOption sort = DogSortOption.NameAsc,
        [FromQuery] int? shelterId = null,
        [FromQuery] string? neighborhood = null,
        [FromQuery] string? coatColor = null,
        [FromQuery] CatCompatibility? catCompatibility = null,
        [FromQuery] ChildrenCompatibility? childrenCompatibility = null,
        [FromQuery] DogActivityLevel? activityLevel = null,
        [FromQuery] ApartmentSuitability? apartmentSuitability = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        var dogs = await dogService.SearchDogsPagedAsync(
            search,
            breed,
            maxAge,
            size,
            location,
            status,
            sort,
            shelterId,
            neighborhood,
            coatColor,
            catCompatibility,
            childrenCompatibility,
            activityLevel,
            apartmentSuitability,
            page,
            pageSize,
            cancellationToken);

        return Ok(new ApiPagedResult<DogListItemApiDto>(
            dogs.Items.Select(ApiDtoMapper.ToDogListItem).ToList(),
            dogs.Page,
            dogs.PageSize,
            dogs.TotalCount,
            dogs.TotalPages));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DogDetailsApiDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DogDetailsApiDto>> GetDog(int id)
    {
        var dog = await dogService.GetDogDetailsAsync(id);
        if (dog is null || !ApiDtoMapper.IsPublicSafeDog(dog))
        {
            return NotFound(new ApiErrorResponse("Dog was not found or is not available for public adoption."));
        }

        return Ok(ApiDtoMapper.ToDogDetails(dog));
    }
}
