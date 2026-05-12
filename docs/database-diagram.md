# PawConnect Database Diagram

This diagram shows the main PawConnect domain tables and how they relate to ASP.NET Core Identity users.

```mermaid
erDiagram
    ASP_NET_USERS {
        string Id PK
        string UserName
        string Email
        string FullName
        string PasswordHash
    }

    ASP_NET_ROLES {
        string Id PK
        string Name
    }

    ASP_NET_USER_ROLES {
        string UserId FK
        string RoleId FK
    }

    SHELTERS {
        int Id PK
        string Name
        string Address
        string PhoneNumber
        string Email
        string Description
        string ApplicationUserId FK
    }

    DOGS {
        int Id PK
        string Name
        string Breed
        int Age
        int Size
        string Location
        int Status
        string Description
        string BehaviorDescription
        string MedicalStatus
        int PreferredFoodTypeId FK
        int DailyFoodAmountGrams
        int ShelterId FK
    }

    DOG_IMAGES {
        int Id PK
        string ImageUrl
        bool IsMainImage
        int DogId FK
    }

    MEDICAL_RECORDS {
        int Id PK
        datetime RecordDate
        string VaccineName
        string TreatmentDescription
        string Notes
        int DogId FK
    }

    ADOPTION_REQUESTS {
        int Id PK
        int Status
        string Message
        datetime CreatedAt
        datetime UpdatedAt
        int DogId FK
        string AdopterId FK
    }

    FAVORITE_DOGS {
        int Id PK
        datetime CreatedAt
        int DogId FK
        string AdopterId FK
    }

    RESOURCE_STOCKS {
        int Id PK
        string Name
        int Quantity
        string Unit
        int LowStockThreshold
        datetime LastUpdatedAt
        int ShelterId FK
        int ResourceCategoryId FK
        int FoodTypeId FK
    }

    RESOURCE_CATEGORIES {
        int Id PK
        string Name
        string Description
    }

    NOTIFICATIONS {
        int Id PK
        string UserId FK
        string Title
        string Message
        int Category
        int Type
        bool IsRead
        datetime CreatedAt
        datetime ReadAt
    }

    FOOD_TYPES {
        int Id PK
        string Name
        string Description
    }

    ASP_NET_USERS ||--o{ ASP_NET_USER_ROLES : has
    ASP_NET_ROLES ||--o{ ASP_NET_USER_ROLES : contains

    ASP_NET_USERS ||--o{ SHELTERS : owns
    SHELTERS ||--o{ DOGS : manages
    SHELTERS ||--o{ RESOURCE_STOCKS : stores
    RESOURCE_CATEGORIES ||--o{ RESOURCE_STOCKS : groups
    FOOD_TYPES ||--o{ RESOURCE_STOCKS : describes_food
    FOOD_TYPES ||--o{ DOGS : preferred_by

    DOGS ||--o{ DOG_IMAGES : has
    DOGS ||--o{ MEDICAL_RECORDS : has
    DOGS ||--o{ ADOPTION_REQUESTS : receives
    DOGS ||--o{ FAVORITE_DOGS : favorited_as

    ASP_NET_USERS ||--o{ ADOPTION_REQUESTS : submits
    ASP_NET_USERS ||--o{ FAVORITE_DOGS : saves
    ASP_NET_USERS ||--o{ NOTIFICATIONS : receives
```

## Relationship Summary

- One user can have many roles through `AspNetUserRoles`.
- One shelter can manage many dogs.
- One shelter can store many resource stock records.
- One resource category can group many resource stock records.
- One food type can be used by many food stock records.
- One food type can be selected as the preferred food type for many dogs.
- One dog can have many images.
- One dog can have many medical records.
- One dog can receive many adoption requests.
- One adopter user can submit many adoption requests.
- Favorite dogs are stored through `FavoriteDogs`, which connects users and dogs.
- `FavoriteDogs` has a unique rule for `AdopterId + DogId`, so a user cannot favorite the same dog twice.
- In-app notifications are stored in `Notifications` and belong to one Identity user.
