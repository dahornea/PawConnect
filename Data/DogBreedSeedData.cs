using PawConnect.Entities;

namespace PawConnect.Data;

public static class DogBreedSeedData
{
    public const int MixedBreedId = 1;
    public const int UnknownBreedId = 2;

    private static readonly DateTime SeedCreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyDictionary<string, BreedNote> Notes = new Dictionary<string, BreedNote>(StringComparer.OrdinalIgnoreCase)
    {
        ["Mixed Breed"] = new(
            "Mixed-breed dogs can combine traits from several backgrounds, so their behavior may be less predictable from breed labels alone.",
            "Traits can vary widely between individual dogs.",
            "Use the dog's shelter notes, observed behavior, and history as the main guide.",
            "Mixed-breed dogs can have varied traits and health backgrounds. Individual medical records and shelter observations are the most reliable source of information."),
        ["Labrador Retriever"] = new(
            "Labrador Retriever-type dogs are often social, trainable, and people-oriented.",
            "Often sociable, food-motivated, and eager to learn.",
            "They may enjoy regular exercise and enrichment; check the dog's own activity level before assuming fit.",
            "Labrador-type dogs may be more prone to joint issues, weight gain, and ear problems. This does not mean this dog has these conditions; review the medical records and ask the shelter or veterinarian."),
        ["Border Collie"] = new(
            "Border Collie-type dogs are commonly attentive, quick learners, and responsive to structure.",
            "Often alert, intelligent, and motivated by activity or training games.",
            "Some may need more mental stimulation than a casual household expects, so individual energy level matters.",
            "Border Collie-type dogs can be associated with high exercise needs and may benefit from mental enrichment. Some lines may be prone to eye or joint issues, so medical records should be reviewed."),
        ["Poodle"] = new(
            "Poodle-type dogs are often bright, people-focused, and adaptable when their routine is clear.",
            "Commonly trainable, observant, and responsive to gentle guidance.",
            "Coat care can be important, and individual confidence around new people can vary.",
            "Poodle-type dogs may need regular coat and ear care. Some may be prone to dental, eye, or joint issues depending on size and background."),
        ["Bichon"] = new(
            "Bichon-type dogs are often companionable and may enjoy being close to people.",
            "Commonly cheerful, social, and suited to steady indoor routines.",
            "Regular coat care is usually needed, and shelter behavior should guide expectations.",
            "Bichon-type dogs may need regular coat care and can be prone to dental or skin sensitivities. Medical records and shelter observations are more important than breed expectations."),
        ["Corgi"] = new(
            "Corgi-type dogs are often expressive, sturdy small dogs with a confident personality.",
            "May be alert, people-oriented, and responsive to routine.",
            "Some can be vocal or opinionated, so check the dog's individual notes and handling style.",
            "Corgi-type dogs may be more prone to back or joint strain because of their body shape. Weight management and appropriate exercise can be important."),
        ["Spaniel"] = new(
            "Spaniel-type dogs are often affectionate, people-oriented, and interested in gentle activity.",
            "Commonly friendly, curious, and responsive to positive handling.",
            "Energy and confidence vary by dog, so shelter observations are more useful than breed expectations alone.",
            "Spaniel-type dogs may be more prone to ear issues because of ear shape. Coat and ear care should be discussed with the shelter or veterinarian."),
        ["Setter"] = new(
            "Setter-type dogs are often gentle, social, and interested in outdoor exploration.",
            "May be friendly, active, and affectionate with familiar people.",
            "They can need regular movement, but the individual dog's age and shelter behavior are more important.",
            "Setter-type dogs can be associated with higher exercise needs and may be prone to ear or joint considerations. Review this dog's medical records for actual history."),
        ["German Shepherd"] = new(
            "German Shepherd-type dogs are often loyal, observant, and responsive to consistent training.",
            "Commonly intelligent, attentive, and protective of familiar routines.",
            "They may need structure and confident handling, so individual temperament should be reviewed carefully.",
            "German Shepherd-type dogs may be more prone to hip or elbow issues and may benefit from structured exercise and weight management."),
        ["Unknown"] = new(
            "This dog's breed background is not known.",
            "Breed-based expectations are limited.",
            "Rely on the dog's description, behavior notes, and shelter observations.",
            "No breed-specific health note is available. Please rely on this dog's medical records and shelter or veterinary information.")
    };

    public static readonly IReadOnlyList<(int Id, string Name)> Breeds =
    [
        (MixedBreedId, "Mixed Breed"),
        (UnknownBreedId, "Unknown"),
        (3, "Labrador Retriever"),
        (4, "German Shepherd"),
        (5, "Golden Retriever"),
        (6, "Border Collie"),
        (7, "Poodle"),
        (8, "Bichon"),
        (9, "Corgi"),
        (10, "Spaniel"),
        (11, "Setter"),
        (12, "Beagle"),
        (13, "Husky"),
        (14, "Rottweiler"),
        (15, "Dachshund"),
        (16, "Chihuahua"),
        (17, "Terrier"),
        (18, "Boxer"),
        (19, "Cane Corso"),
        (20, "Belgian Malinois"),
        (21, "Australian Shepherd"),
        (22, "Yorkshire Terrier"),
        (23, "Shih Tzu"),
        (24, "Maltese"),
        (25, "French Bulldog"),
        (26, "Romanian Mioritic Shepherd"),
        (27, "Romanian Carpathian Shepherd"),
        (28, "Romanian Raven Shepherd"),
        (29, "Akita"),
        (30, "Alaskan Malamute"),
        (31, "American Bulldog"),
        (32, "American Staffordshire Terrier"),
        (33, "Basenji"),
        (34, "Basset Hound"),
        (35, "Bernese Mountain Dog"),
        (36, "Bloodhound"),
        (37, "Boston Terrier"),
        (38, "Bulldog"),
        (39, "Cavalier King Charles Spaniel"),
        (40, "Chow Chow"),
        (41, "Cocker Spaniel"),
        (42, "Dalmatian"),
        (43, "Doberman Pinscher"),
        (44, "English Springer Spaniel"),
        (45, "Great Dane"),
        (46, "Greyhound"),
        (47, "Jack Russell Terrier"),
        (48, "Miniature Schnauzer"),
        (49, "Newfoundland"),
        (50, "Papillon"),
        (51, "Pekingese"),
        (52, "Pinscher"),
        (53, "Pit Bull Terrier"),
        (54, "Pointer"),
        (55, "Pomeranian"),
        (56, "Pug"),
        (57, "Samoyed"),
        (58, "Schnauzer"),
        (59, "Shar Pei"),
        (60, "Shetland Sheepdog"),
        (61, "Staffordshire Bull Terrier"),
        (62, "Vizsla"),
        (63, "Weimaraner"),
        (64, "West Highland White Terrier"),
        (65, "Whippet"),
        (66, "Romanian Bucovina Shepherd"),
        (67, "Street Dog"),
        (68, "Hound"),
        (69, "Shepherd Mix"),
        (70, "Retriever Mix"),
        (71, "Saint Bernard"),
        (72, "Rhodesian Ridgeback"),
        (73, "Collie"),
        (74, "Japanese Spitz"),
        (75, "Spitz"),
        (76, "English Cocker Spaniel"),
        (77, "Presa Canario"),
        (78, "Argentine Dogo"),
        (79, "Kangal Shepherd"),
        (80, "Afghan Hound"),
        (81, "Airedale Terrier"),
        (82, "Borzoi"),
        (83, "Brittany Spaniel"),
        (84, "Bull Terrier"),
        (85, "Bullmastiff"),
        (86, "Chinese Crested"),
        (87, "Irish Setter"),
        (88, "Irish Wolfhound"),
        (89, "Italian Greyhound"),
        (90, "Lhasa Apso"),
        (91, "Miniature Pinscher"),
        (92, "Old English Sheepdog"),
        (93, "Portuguese Water Dog"),
        (94, "Scottish Terrier"),
        (95, "Soft Coated Wheaten Terrier"),
        (96, "Welsh Corgi"),
        (97, "English Bulldog"),
        (98, "Siberian Husky"),
        (99, "Toy Poodle"),
        (100, "Miniature Poodle")
    ];

    public static DogBreed[] CreateSeedEntities()
    {
        return Breeds
            .Select(breed => new DogBreed
            {
                Id = breed.Id,
                Name = breed.Name,
                IsActive = true,
                CreatedAt = SeedCreatedAt,
                GeneralDescription = Notes.TryGetValue(breed.Name, out var note) ? note.GeneralDescription : null,
                TypicalTraits = note?.TypicalTraits,
                CareNotes = note?.CareNotes,
                CommonHealthConsiderations = note?.CommonHealthConsiderations
            })
            .ToArray();
    }

    private sealed record BreedNote(string GeneralDescription, string TypicalTraits, string CareNotes, string CommonHealthConsiderations);
}
