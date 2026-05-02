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
        string OwnerUserId FK
    }

    DOGS {
        int Id PK
        string Name
        string Breed
        int Age
        int Size
        int Status
        string Description
        datetime CreatedAt
        int ShelterId FK
    }

    DOG_IMAGES {
        int Id PK
        string ImageUrl
        string Caption
        bool IsMainImage
        int DogId FK
    }

    MEDICAL_RECORDS {
        int Id PK
        datetime RecordDate
        string Title
        string Notes
        string VeterinarianName
        int DogId FK
    }

    ADOPTION_REQUESTS {
        int Id PK
        int Status
        string Message
        datetime RequestedAt
        int DogId FK
        string AdopterUserId FK
    }

    FAVORITE_DOGS {
        int Id PK
        datetime CreatedAt
        int DogId FK
        string UserId FK
    }

    RESOURCE_STOCKS {
        int Id PK
        string Name
        int Quantity
        string Unit
        int MinimumQuantity
        int ShelterId FK
    }

    ASP_NET_USERS ||--o{ ASP_NET_USER_ROLES : has
    ASP_NET_ROLES ||--o{ ASP_NET_USER_ROLES : contains

    ASP_NET_USERS ||--o{ SHELTERS : owns
    SHELTERS ||--o{ DOGS : manages
    SHELTERS ||--o{ RESOURCE_STOCKS : stores

    DOGS ||--o{ DOG_IMAGES : has
    DOGS ||--o{ MEDICAL_RECORDS : has
    DOGS ||--o{ ADOPTION_REQUESTS : receives
    DOGS ||--o{ FAVORITE_DOGS : favorited_as

    ASP_NET_USERS ||--o{ ADOPTION_REQUESTS : submits
    ASP_NET_USERS ||--o{ FAVORITE_DOGS : saves
```

## Relationship Summary

- One user can have many roles through `AspNetUserRoles`.
- One shelter can manage many dogs.
- One shelter can store many resource stock records.
- One dog can have many images.
- One dog can have many medical records.
- One dog can receive many adoption requests.
- One adopter user can submit many adoption requests.
- Favorite dogs are stored through `FavoriteDogs`, which connects users and dogs.
- `FavoriteDogs` has a unique rule for `UserId + DogId`, so a user cannot favorite the same dog twice.

