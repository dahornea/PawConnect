using PawConnect.Entities;

namespace PawConnect.Services;

public interface IFoodTypeService
{
    Task<List<FoodType>> GetAllAsync();

    Task<FoodType?> GetByIdAsync(int id);

    Task CreateAsync(FoodType foodType);

    Task UpdateAsync(FoodType foodType);

    Task DeleteAsync(int id);
}
