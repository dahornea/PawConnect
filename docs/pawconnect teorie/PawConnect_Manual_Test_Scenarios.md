# PawConnect Manual Test Scenarios

This document is a practical pre-presentation checklist for PawConnect. It is based on the current Blazor pages, services, routes, seeded accounts, and implemented features in the codebase.

Primary files inspected:

| Area | Files |
| ---- | ----- |
| Startup and services | `Program.cs` |
| Seeded accounts/data | `Data/IdentitySeedData.cs` |
| Public pages | `Components/Pages/Home.razor`, `Components/Pages/Dogs.razor`, `Components/Pages/DogDetails.razor`, `Components/Pages/Shelters.razor`, `Components/Pages/ShelterDetails.razor`, `Components/Pages/SuccessStories.razor`, `Components/Pages/ShelterApply.razor` |
| Adopter pages | `Components/Pages/Adopter/AdopterDashboard.razor`, `Components/Pages/Adopter/MyAdopterProfile.razor`, `Components/Pages/Adopter/Recommendations.razor`, `Components/Pages/Adopter/AdoptionCopilot.razor`, `Components/Pages/Adopter/Favorites.razor`, `Components/Pages/Adopter/MyAdoptionRequests.razor` |
| Shelter pages | `Components/Pages/Shelter/ShelterDashboard.razor`, `Components/Pages/Shelter/ManageDogs.razor`, `Components/Pages/Shelter/CreateDog.razor`, `Components/Pages/Shelter/EditDog.razor`, `Components/Pages/Shelter/ShelterAdoptionRequests.razor`, `Components/Pages/Shelter/Resources.razor` |
| Admin pages | `Components/Pages/Admin/AdminDashboard.razor`, `Components/Pages/Admin/AdminUsers.razor`, `Components/Pages/Admin/AdminShelters.razor`, `Components/Pages/Admin/AdminDogs.razor`, `Components/Pages/Admin/AdminAdoptionRequests.razor`, `Components/Pages/Admin/AdminShelterRequests.razor`, `Components/Pages/Admin/AdminReportHistory.razor`, `Components/Pages/Admin/AdminActivityLog.razor` |
| Core services | `Services/DogService.cs`, `Services/AdoptionRequestService.cs`, `Services/DogImageService.cs`, `Services/ResourceStockService.cs`, `Services/CsvImportService.cs`, `Services/ExportService.cs`, `Services/AdoptionCopilotService.cs`, `Services/AdoptionCopilotToolService.cs`, `Services/SemanticDogSearchService.cs`, `Services/DogRecommendationService.cs`, `Services/VisitSchedulingHelper.cs` |

## 1. Testing Accounts

Seeded from `Data/IdentitySeedData.cs`.

| Email | Password | Role | Purpose | Pages to test |
| ----- | -------- | ---- | ------- | ------------- |
| `admin@pawconnect.local` | `PawConnect123!` | Admin | Platform-wide management | `/admin/dashboard`, `/admin/users`, `/admin/shelters`, `/admin/dogs`, `/admin/adoption-requests`, `/admin/shelter-requests`, `/admin/report-history`, `/admin/activity-log` |
| `happy-paws@pawconnect.local` | `PawConnect123!` | Shelter | Shelter workspace for Happy Paws Shelter | `/shelter/dashboard`, `/shelter/dogs`, `/shelter/dogs/create`, `/shelter/adoption-requests`, `/shelter/resources` |
| `ana.ionescu@pawconnect.local` | `PawConnect123!` | Adopter | Main adopter demo account | `/adopter/dashboard`, `/adopter/profile`, `/adopter/recommendations`, `/adopter/copilot`, `/favorites`, `/my-adoption-requests`, `/notifications` |
| `mihai.radu@pawconnect.local` | `PawConnect123!` | Adopter | Secondary adopter account for ownership checks | `/adopter/dashboard`, `/dogs`, `/my-adoption-requests` |
| `irina.pop@pawconnect.local` | `PawConnect123!` | Adopter | Secondary adopter account for ownership checks | `/adopter/dashboard`, `/adopter/profile`, `/adopter/copilot` |

## 2. Smoke Test Checklist

| Test | Priority | Steps | Expected result | Status |
| ---- | -------- | ----- | --------------- | ------ |
| ST-01 Home page loads | High | Open `/`. | Home page renders, featured dogs load, no exception page. | Not run |
| ST-02 Dogs page loads | High | Open `/dogs`. | Dog cards show only Available/Reserved dogs, filters are visible. | Not run |
| ST-03 Shelters page loads | High | Open `/shelters`. | Shelter cards/table loads with public shelter information. | Not run |
| ST-04 Success Stories loads | Medium | Open `/success-stories`. | Adopted dogs with story/adoption data appear, or friendly empty state appears. | Not run |
| ST-05 Login works | High | Open `/Account/Login`, log in as adopter. | User is authenticated and role navigation appears. | Not run |
| ST-06 Register page loads | Medium | Open `/Account/Register`. | Registration form appears for adopter accounts. | Not run |
| ST-07 Role dashboard routing | High | Log in as admin, shelter, adopter and open each dashboard. | Correct dashboard loads and wrong-role dashboards are blocked. | Not run |
| ST-08 Notifications page loads | Medium | Log in and open `/notifications`. | Notifications list, filters, and empty state work. | Not run |

## 3. Public User Scenarios

| Scenario | Priority | Preconditions | Steps | Expected result | Visual checks | Watch for |
| -------- | -------- | ------------- | ----- | --------------- | ------------- | --------- |
| PUB-01 Browse public dogs | High | Logged out. Database seeded. | Open `/dogs`. | Shows public-safe dogs only: Available and Reserved. | Cards have name, breed, age, size, shelter/location, status chip, image or placeholder. | Adopted/InTreatment dogs should not appear. |
| PUB-02 Filter dogs by common fields | High | Logged out. | Open filters on `/dogs`; try breed, size, location, neighborhood, status, coat color. | Filters combine together and update results after Apply Filters. | Active filter chips appear and can be cleared. | Empty state should be friendly, not a blank page. |
| PUB-03 Search and sort dogs | Medium | Logged out. | Search by dog name, then use sort menu: name, age, breed, location, status, newest. | Dog list changes according to search/sort. | Result count text stays accurate. | Search currently targets dog name; do not expect full-text search on descriptions here. |
| PUB-04 Nearby filter | Medium | Logged out. | On `/dogs`, type an address/city in Near, choose radius, use suggestion or location button if available. | Dogs filter by distance when shelter coordinates exist. | Nearby summary and distance text appear. | Browser location permission may be denied; app should show warning. |
| PUB-05 View dog details | High | Dog list has at least one public dog. | Click `View Details` on a dog. | `/dogs/{id}` opens with dog summary, breed, age, status, size, location, description, behavior, medical status. | Back button returns to source when `returnUrl` is used; public default returns to `/dogs`. | No broken image icons. |
| PUB-06 Dog image gallery and lightbox | High | Dog has multiple valid image URLs. | Open dog details, click main image, use previous/next arrows. | Lightbox opens, centered image displays, arrows navigate, close works. | Counter uses valid real images only. Thumbnail highlight works. | If no real images exist, no empty lightbox should open. |
| PUB-07 Breed Information card | High | Dog has DogBreed data. | Open dog details and scroll to Breed Information. | Shows formatted breed, mixed breed chip if needed, overview, traits, care context, health considerations, important note. | Full-width card is readable and not too dense. | Breed notes must not look like diagnosis or guarantee. |
| PUB-08 Food, medical, and shelter info | High | Dog has details. | Open dog details and inspect lower cards. | Shelter, Food, and Medical Records sections display available data or friendly fallback. | Shelter link goes to `/shelters/{id}`. | Medical Records remain separate from breed health notes. |
| PUB-09 Shelter profile and map | Medium | Shelters have coordinates. | Open `/shelters`, then `View Details` for a shelter. | Shelter details load with read-only Leaflet/OpenStreetMap map when coordinates exist. | Map pin appears, dog list for that shelter shows public-safe dogs. | If coordinates missing, page should not fail. |
| PUB-10 Submit shelter application | Medium | Use logged-out visitor or adopter, not shelter/admin. | Open `/shelters/apply`, fill required fields, optionally use Find location, submit. | Request is submitted for admin review. | Map location card can select/edit pin; success message appears. | Admin and shelter users should see role-specific blocked messages. |

## 4. Adopter Scenarios

| Scenario | Priority | Preconditions | Steps | Expected result | Visual checks | Watch for |
| -------- | -------- | ------------- | ----- | --------------- | ------------- | --------- |
| AD-01 Login as adopter | High | Use `ana.ionescu@pawconnect.local`. | Log in via `/Account/Login`. | Adopter navigation appears. | Links include Dashboard, Profile, Recommendations, Copilot, Favorites. | Wrong role menu items should not appear. |
| AD-02 Adopter dashboard | High | Logged in as adopter. | Open `/adopter/dashboard`. | Dashboard shows profile/recommendation/request/favorite/recent-view information. | Cards align and no empty technical text appears. | Recommendations should not show Adopted/InTreatment dogs. |
| AD-03 Edit adopter profile | High | Logged in as adopter. | Open `/adopter/profile`, update city/housing/children/pets/experience, save. | Profile saves and later affects recommendations. | Validation messages are clear. | Phone/profile optional fields should not break save. |
| AD-04 Recommended dogs | High | Logged in as adopter with profile. | Open `/adopter/recommendations`. | Personalized dog cards appear with match percentage, label, reasons, optional AI-assisted chip. | Reasons are understandable and public-safe. | Scores are heuristic, not guarantees. |
| AD-05 Adoption Copilot basic use | High | Logged in as adopter. | Open `/adopter/copilot`, enter a prompt, click Ask Copilot. | Response card shows summary, source chips, applied constraints, dog result cards. | Results have status chip, score/label or filter label, tags/cautions, View Details, Save. | No private data should appear. |
| AD-06 Favorite/unfavorite dog | High | Logged in as adopter. | On `/dogs`, `/favorites`, recommendations, or Copilot, click save/heart. | Favorite toggles and `/favorites` updates. | Saved state is consistent across pages after refresh. | Public users should be prompted to log in. |
| AD-07 Recently viewed dogs | Medium | Logged in as adopter. | Open several dog details pages, then dashboard. | Recently viewed list updates. | Dog names/images match opened dogs. | Views of non-public dogs should not be tracked from public UI. |
| AD-08 Submit adoption request | High | Logged in as adopter. Dog is Available/Reserved. | Open dog details, click Submit Adoption Request, choose future visit time, fill reason/hours/note, submit. | Request is created with Pending status and Visit requested status. Shelter notification/email may be generated. | Preferred visit validation uses shelter schedule. | Duplicate pending request for same dog should be blocked. |
| AD-09 My Adoption Requests | High | Adopter has requests. | Open `/my-adoption-requests`. | Clean cards show dog identity, reason, hours alone, preferred visit, notes, status chips, created/updated dates. | No raw enum labels like `VisitConfirmed`. | Cancel button only appears when allowed. |
| AD-10 Cancel pending request | High | Adopter has a pending request. | Click Cancel Request on `/my-adoption-requests` and confirm. | Request becomes Cancelled. | Status chip updates and cancel button disappears. | Cannot cancel another adopter's request. |
| AD-11 View notifications | Medium | Logged in as adopter. | Open `/notifications`, filter by category/unread, mark as read, open linked notification. | Notifications update and link navigates correctly. | Unread chip disappears after mark read. | Delete should remove only own notification. |

### Adoption Copilot Prompt Tests

| Prompt | Priority | Expected intent/chips | Expected dogs/result type | Correct tags/cautions | Bad result examples |
| ------ | -------- | --------------------- | ------------------------- | --------------------- | ------------------- |
| `I live in an apartment and want a dog that does not need too much activity.` | High | Home: Apartment; Lifestyle: Low activity; Status: Available, Reserved. | Small/medium calm dogs, short-walk dogs, dogs that settle indoors. | Short walks, Indoor rest, Quiet routine, Settles quickly, Small/Medium size, Reserved caution if needed. | Very active/yard-only dogs ranked high; cat/child tags shown as main reasons. |
| `I have a cat at home.` | High | Compatibility: Cats; Status: Available, Reserved. | Dogs with direct cat evidence or cautious cat introduction evidence. | Calm near cats, Redirectable around cats, Needs slow cat introductions, Ask shelter about cats, No cat history found. | Apartment tags like Short walks/Indoor rest as visible primary tags; high score for dogs with no cat evidence. |
| `I have young children at home.` | High | Compatibility: Children, stricter for young children. | Dogs with child/family evidence or cautious family fit. | Gentle handling, Family routine fit, Better with older children as caution, Needs calm children, Ask shelter about children. | Excellent/high score when only generic friendly evidence exists. |
| `I have an older dog at home.` | High | Compatibility: SeniorDog or OtherDogs; Lifestyle: Calm. | Dogs respectful around other dogs and not pushy. | Calm dog company, Respectful around dogs, Needs slow dog introductions, Not too energetic. | Dogs with rough/pushy play ranked as strong matches. |
| `I have a sick dog recovering at home.` | High | Compatibility: SensitiveDog; Lifestyle: Calm. | Calm, respectful, low-pressure dog-to-dog candidates. | Calm dog company, Respectful around dogs, Needs slow dog introductions, Not too energetic, Ask shelter about sensitive dog fit. | Generic "friendly" dogs marked Excellent without dog-to-dog evidence. |
| `I want an active dog for a house with a yard.` | High | Home: House/Yard; Lifestyle: High activity or Active. | Larger or energetic dogs that enjoy longer walks, training games, outdoor activity. | Longer walks, Outdoor activity, Training games, Yard/space fit. | Low-activity short-walk dogs ranked at top. |
| `I'm a first-time adopter.` | Medium | ExperienceLevel: Beginner or secondary routine/guidance need. | Dogs that respond well to routine/gentle guidance. | Routine, Gentle guidance, Easy to redirect; cautions for patient/experienced adopter. | Patient/experienced-only dog shown as top without caution. |
| `Find me a calm dog in Zorilor.` | Medium | Location: Zorilor; Temperament: Calm; Status: Available, Reserved. | Dogs from Zorilor shelters/neighborhoods with calm evidence. | Location chip, calm/gentle/routine tags. | Dogs from other neighborhoods if strict location was detected. |
| `black and tan dogs` | High | Coat color filter: Black and tan. | Exact filter matches only. | Coat color: Black and tan; label should be Exact match or Matches request. | "Status filter" explanation; 50-60% Possible match for exact coat color matches. |
| `I live in an apartment but enjoy longer walks.` | High | Home: Apartment; Activity: Longer walks; Lifestyle: Moderate activity. | Dogs with longer-walk/moderate-activity evidence plus manageable apartment fit. | Longer walks only when dog data supports it, Medium/Small size, Settles quickly; cautions for Needs more space or Ask shelter about apartment fit. | Short walks rewarded as longer walks; duplicate Activity chips; dogs with only short walks ranked above longer-walk dogs. |

## 5. Shelter User Scenarios

| Scenario | Priority | Preconditions | Steps | Expected result | Business behavior | Watch for |
| -------- | -------- | ------------- | ----- | --------------- | ----------------- | --------- |
| SH-01 Login as shelter | High | Use `happy-paws@pawconnect.local`. | Log in. | Shelter navigation appears. | User is linked to one shelter profile. | Admin/adopter links should not appear. |
| SH-02 Shelter dashboard | High | Logged in as shelter. | Open `/shelter/dashboard`. | Dashboard summarizes own shelter dogs, requests, resources, alerts. | Only own shelter data should appear. | No platform-wide data. |
| SH-03 Manage own dogs | High | Logged in as shelter. | Open `/shelter/dogs`. | Table/cards show only dogs belonging to the shelter. | Uses `DogService.GetDogsForShelterAsync`. | Should not list dogs from other shelters. |
| SH-04 Create dog profile | High | Logged in as shelter. | Open `/shelter/dogs/create`, fill required fields, save. | Dog is created and appears in shelter dog list. | Dog is assigned to current shelter. | Required fields and age validation should work. |
| SH-05 Breed autocomplete and mixed breed | High | Create/edit dog. | Select primary breed, check Mixed breed, optionally select secondary breed or custom breed. | Displayed breed formats correctly, for example `Labrador Retriever Mix` or `Labrador Retriever x Border Collie Mix` in UI. | DogBreed lookup is used instead of free text-only breed. | Avoid `Unknown Mix` or `Mixed Breed Mix`. |
| SH-06 Coat color field | Medium | Create/edit dog. | Select Coat color and save. | Dog details displays coat color when present; public filter can find it. | CoatColor remains optional. | Blank coat color should not break save. |
| SH-07 Edit dog profile | High | Own dog exists. | Open `/shelter/dogs/edit/{id}`, edit profile fields, use bottom Save Changes bar. | Changes save and return to dog list. | Update refreshes search embeddings best effort if configured. | Save bar should align and not cover content. |
| SH-08 Add dog image URL | High | Own non-adopted dog exists. | In Edit Dog, add a valid direct image URL and optionally mark Main image. | Image is saved, appears in image grid and public cards/details. | Invalid/duplicate URLs are rejected by `DogImageService`. | After successful add, Image URL required error should not remain. |
| SH-09 Set main image and gallery | Medium | Dog has multiple images. | Set a different main image, open public details. | Public card uses main valid real image; details gallery shows valid images only. | Placeholder is not stored as a DogImage. | No duplicated placeholder or broken thumbnails. |
| SH-10 Add medical record | High | Own non-adopted dog exists. | In Edit Dog, add vaccine/treatment/date/notes. | Medical record appears on edit page and public dog details. | Adopted dogs are read-only for medical changes. | Date is required. |
| SH-11 View dog status history | High | Dog has status changes. | In Edit Dog or Admin Dogs, view status history. | Old/new status, changed date, user, notes display; empty state if none. | Status history is created by status transitions. | No silent failure. |
| SH-12 Confirm visit | High | Pending request exists for own shelter dog. | Open `/shelter/adoption-requests`, choose Manage > Confirm Visit. | Request becomes Visit confirmed; dog becomes Reserved. | Email/PDF/calendar invite and notification may be generated. | Visit action must only affect own shelter requests. |
| SH-13 Reject request | High | Pending or visit-confirmed request exists. | Use Manage > Reject or Reject after Visit. | Request becomes Rejected; if it was visit-confirmed and dog was Reserved, dog may return to Available. | `AdoptionRequestService.RejectRequestAsync` enforces ownership. | Rejected request should no longer have manage actions. |
| SH-14 Mark adoption as completed | High | Visit-confirmed request exists. | Use Manage > Mark as Adopted. | Request becomes Accepted/Adopted; dog becomes Adopted with AdoptedAt. | Other pending requests for the same dog are rejected. | Adopted dog should no longer appear publicly. |
| SH-15 Resource stock management | High | Logged in as shelter. | Open `/shelter/resources`, add/edit/delete resource, choose category, quantity, unit, threshold. | Valid resources save; low-stock rows are highlighted/warned. | Category is required and should not show `0`. | Quantity must be > 0; unit/name required. |
| SH-16 Resource low stock warning/report | Medium | Resource quantity <= threshold. | Save low-stock resource or trigger low-stock flow. | Notification/email/PDF may be generated depending on settings. | Low-stock report history may be recorded. | Email failure should not break resource save. |
| SH-17 Shelter exports | Medium | Shelter has data. | Export shelter dogs, adoption requests, and resources as CSV/PDF where available. | Files download and contain only current shelter data. | Report history/audit log may record export. | No other shelter data in export. |
| SH-18 Shelter CSV imports | Medium | Have CSV template. | Download dog/resource template, upload CSV, preview, confirm valid import. | Preview shows valid/invalid rows; confirm only enabled when valid. | Import scoped to current shelter. | Invalid rows should show row-level errors. |
| SH-19 Shelter summary report | Low | Email settings available. | Trigger manual shelter summary if visible in dashboard/resources, or rely on scheduled job if enabled. | Summary report email/PDF is generated when configured. | Uses `ShelterSummaryReportService`. | Scheduled jobs may be disabled in development. |

## 6. Admin Scenarios

| Scenario | Priority | Preconditions | Steps | Expected result | Business behavior | Watch for |
| -------- | -------- | ------------- | ----- | --------------- | ----------------- | --------- |
| ADM-01 Login as admin | High | Use `admin@pawconnect.local`. | Log in. | Admin navigation appears. | Admin can view platform-wide pages. | Shelter/adopter-only actions should remain role-specific. |
| ADM-02 Admin dashboard | High | Logged in as admin. | Open `/admin/dashboard`. | Platform summary counts load. | Data comes from services and all shelters. | No broken dashboard cards. |
| ADM-03 Manage users | Medium | Logged in as admin. | Open `/admin/users`; export CSV. | Users list loads; export excludes Identity security fields. | Uses `ExportService.GenerateUsersCsvAsync`. | Password hashes/tokens must not be exposed. |
| ADM-04 Manage shelters | High | Logged in as admin. | Open `/admin/shelters`. | Shelter list displays city, neighborhood, address, email, phone, map status, dog count. | Admin can edit shelter profile. | Import preview should not auto-create users. |
| ADM-05 Edit shelter map/location | High | Admin on `/admin/shelters`. | Click Edit, change address/city/neighborhood, Find location, move map pin, save. | Coordinates/address update; Revert only appears/enables when location changed. | Uses `NominatimGeocodingService` and `ShelterMap`. | Lookup failure should not overwrite saved coordinates. |
| ADM-06 Admin dogs | High | Logged in as admin. | Open `/admin/dogs`. | Shows all dogs from all shelters with status, breed, shelter, success story info. | Admin can view details/status history/delete where allowed. | Dog details return URL should go back to `/admin/dogs`. |
| ADM-07 Rebuild dog search index | High | OpenAI embeddings configured or intentionally disabled. | Click `Rebuild Dog Search Index` on `/admin/dogs`. | Success/warning message shows created/updated/unchanged/removed/failed, or warning if OpenAI disabled. | Uses `DogSearchEmbeddingService.RebuildDogSearchIndexAsync`. | No crash if OpenAI is disabled. |
| ADM-08 View dog status history | High | Dog has status history. | Click history icon on `/admin/dogs`. | Dialog opens with old/new status, changed at, changed by, notes. | Empty state shown when no history. | Button should not silently do nothing. |
| ADM-09 Admin adoption requests | High | Requests seeded/exist. | Open `/admin/adoption-requests`, click Details. | Details dialog opens with dog, shelter, adopter, request, questionnaire, visit, dates. | Admin view is read-only oversight. | Details button must open a dialog. |
| ADM-10 Shelter application review | High | Pending shelter request exists. | Open `/admin/shelter-requests`, approve/reject a request. | Approve creates shelter user/profile; reject marks request rejected. | Service validates duplicate emails/shelters. | Avoid duplicate account creation. |
| ADM-11 Report history | Medium | Reports/exports generated. | Open `/admin/report-history`. | Report records show type, trigger, recipient, status/date. | Exports/email reports record history where implemented. | Empty state should be friendly. |
| ADM-12 Activity log | Medium | Actions have happened. | Open `/admin/activity-log`; filter if available. | Audit entries display important actions. | Import/export/dog/request actions log. | No sensitive tokens/passwords. |
| ADM-13 Admin exports | Medium | Admin pages available. | Export users, shelters, dogs, adoption requests, shelter requests. | CSV/PDF files download where buttons exist. | Uses `ExportService`. | Export should not leak Identity secrets. |

## 7. Authorization and Security Scenarios

| Scenario | Priority | Steps | Expected result | Watch for |
| -------- | -------- | ----- | --------------- | --------- |
| SEC-01 Public cannot access adopter pages | High | Log out, open `/adopter/dashboard`, `/adopter/copilot`, `/favorites`, `/my-adoption-requests`. | Redirects to login or access denied. | No page data rendered before redirect. |
| SEC-02 Public cannot access shelter/admin pages | High | Log out, open `/shelter/dogs`, `/admin/dogs`. | Redirects/blocks access. | No management UI visible. |
| SEC-03 Adopter cannot access shelter/admin pages | High | Log in as adopter, open `/shelter/dogs`, `/admin/dashboard`. | Access denied or redirect. | No shelter/admin data. |
| SEC-04 Shelter cannot access admin pages | High | Log in as shelter, open `/admin/users`, `/admin/dogs`. | Access denied. | No platform-wide data. |
| SEC-05 Shelter ownership checks | High | As shelter, try editing a dog/resource/request ID from another shelter by changing URL if known. | Service returns not found/blocked. | Should not expose another shelter's data. |
| SEC-06 Adopter request ownership | High | As adopter B, try canceling adopter A's request if visible/URL known. | Request cannot be cancelled. | Service must check adopter ID. |
| SEC-07 Public-safe dog visibility | High | Browse `/dogs`, Copilot, recommendations. | Only Available/Reserved dogs appear. | Adopted/InTreatment dogs excluded from public/Copilot/recommendations. |
| SEC-08 AI private data safety | High | Use Copilot prompts asking for adopter emails, shelter internal notes, passwords, or audit logs. | Copilot should return dog search/advice only and not expose private data. | No raw private adopter/shelter/admin data in result cards. |

## 8. AI/Copilot Scenarios

| Scenario | Priority | Prompt/Setup | Expected intent | Expected chips/result type | Correct tags | Incorrect tags/results |
| -------- | -------- | ------------ | --------------- | -------------------------- | ------------ | ---------------------- |
| AI-01 OpenAI enabled path | High | Configure `OpenAI:Enabled` and API key, then ask Copilot. | Natural-language query is interpreted with optional AI enhancement/tool usage. | Source chips may show AI-assisted explanation and Used PawConnect data. | Real dog IDs only, short explanations. | Unknown dog names/IDs, private data, invented evidence. |
| AI-02 OpenAI fallback path | High | Disable OpenAI or remove API key, then ask Copilot. | Deterministic/rule-based fallback. | Source chip says Rule-based fallback; results still appear when matching dogs exist. | Public-safe dogs and evidence-backed tags. | Empty results only because AI is disabled. |
| AI-03 Unknown dog ID defense | High | If testing with mocked OpenAI response, include unknown dog ID. | Backend validates results. | Unknown dog ignored. | Only real candidate dogs appear. | UI card for nonexistent dog. |
| AI-04 Public-safe filtering | High | Prompt: `show me adopted dogs` or `dogs in treatment`. | Public-safe statuses remain Available/Reserved by default for Copilot. | Either no result or safe explanation; no Adopted/InTreatment public cards. | Reserved caution if Reserved included. | InTreatment/Adopted dog appears in Copilot results. |
| AI-05 Reserved dog caution | High | Prompt: `Show me reserved dogs that could still be a good match.` | Status filter Reserved plus suitability. | Reserved dogs shown with Reserved caution. | `Reserved - availability may change`. | Reserved dog shown as if fully available. |
| AI-06 Strict neighborhood no results | Medium | Prompt: `Find me a calm dog in a neighborhood with no matching dogs.` | Location/neighborhood hard filter. | Empty state or broader suggestion. | Explanation mentions no matching dogs. | Dogs from other neighborhood shown as exact matches. |
| AI-07 Cat query tag filtering | High | Prompt: `I have a cat at home.` | Compatibility: Cats. | Cat-specific tags only. | Calm near cats, Redirectable around cats, Needs slow cat introductions, Ask shelter about cats. | Short walks, Indoor rest, Apartment tags as visible primary tags. |
| AI-08 Apartment query tags | High | Prompt: `I live in an apartment and want a dog that does not need too much activity.` | Home: Apartment, low activity. | Apartment/lifestyle tags allowed. | Short walks, Indoor rest, Settles quickly, Quiet routine, Small/Medium size. | Cat/child/sensitive-dog tags as primary display tags. |
| AI-09 Sensitive dog query | High | Prompt: `I have a sick dog recovering at home.` | Compatibility: SensitiveDog. | Evidence-backed compatibility results. | Calm dog company, Respectful around dogs, Needs slow dog introductions, Ask shelter about sensitive dog fit. | Excellent/high score with only generic friendly evidence. |
| AI-10 Children query | High | Prompt: `I have young children at home.` | Compatibility: Children, strict for young children. | Child/family tags, cautious scoring. | Gentle handling, Family routine fit, Needs calm children, Ask shelter about children. | Apartment-only tags, high score with no child evidence. |
| AI-11 Coat color filter query | High | Prompt: `black and tan dogs`. | Deterministic CoatColor filter. | Exact match/Matches request, no low percentage needed. | Coat color: Black and tan. | Explanation says "status filter"; result fails coat color. |
| AI-12 Walk preference precision | High | Prompt: `I live in an apartment but enjoy longer walks.` | Home: Apartment; Activity: Longer walks; Lifestyle: Moderate activity. | Longer-walk/moderate candidates score above short-walk-only dogs. | Longer walks only when dog data supports it; Needs more space caution if present. | Short walks shown as positive longer-walk evidence. |

## 9. UI/UX Scenarios

| Scenario | Priority | Steps | Expected result | Watch for |
| -------- | -------- | ----- | --------------- | --------- |
| UX-01 Desktop responsive layout | High | Test `/dogs`, `/dogs/{id}`, `/adopter/copilot`, `/shelter/dogs/edit/{id}`, `/admin/adoption-requests` at desktop width. | Cards/tables align and actions are reachable. | Overlapping buttons, clipped text, excessive empty space. |
| UX-02 Mobile responsive layout | High | Use browser mobile viewport for public dogs, dog details, My Adoption Requests, Copilot. | Layout stacks cleanly; buttons remain tappable. | Horizontal overflow. |
| UX-03 Dog details layout | High | Open dog details with/without images. | Gallery left, details right on desktop; stacked on mobile. | Placeholder should appear only when no real valid image exists. |
| UX-04 Breed Information readability | High | Open dog details. | Full-width Breed Information card is scan-friendly. | Repeated disclaimers or alarming medical wording. |
| UX-05 Common health considerations | Medium | Open a dog whose breed has health notes. | Health notes display as general education. | Notes must not sound like actual diagnosis. |
| UX-06 Lightbox controls | High | Click main dog photo; use arrows and close. | Centered image, no huge empty white area, arrows wrap or navigate cleanly. | Counter should count valid images only. |
| UX-07 Empty states | Medium | Force empty filters/no notifications/no resources if possible. | Friendly empty state with icon and helpful action. | Blank tables or raw errors. |
| UX-08 Status chips | High | Inspect dog, request, visit, resource statuses. | Chips are compact and readable. | Raw enum names like `VisitConfirmed`. |
| UX-09 Adoption request card style | Medium | Open `/my-adoption-requests`. | Cards have dog identity, details, status/actions in clean sections. | Cancel button too far from status or huge disabled bars. |
| UX-10 Admin adoption request styling | Medium | Open `/admin/adoption-requests`. | Header, summary, table, actions are aligned. | Details button must visibly look clickable and open dialog. |

## 10. Email/PDF/Notification Scenarios

Local development may use a local SMTP catcher depending on `EmailSettings`. Email failures are logged and should not roll back the main business action.

| Scenario | Priority | Steps | Expected result | Files/services involved | Watch for |
| -------- | -------- | ----- | --------------- | ----------------------- | --------- |
| EMAIL-01 Adoption request notification | High | Submit adoption request as adopter. | Shelter receives notification and configured email/PDF report if SMTP is set. | `AdoptionRequestService`, `PdfReportService`, `SmtpEmailService`. | Request should still save if email fails. |
| EMAIL-02 Visit confirmation email and calendar invite | High | As shelter, confirm a pending visit. | Adopter email can include PDF and `.ics` calendar invite. | `AdoptionRequestService`, `VisitSchedulingHelper.CreateCalendarInviteAttachment`. | `.ics` contains dog, shelter, date/time, location; no external calendar API. |
| EMAIL-03 Adoption completed report | Medium | Mark confirmed request as adopted. | Status email/report/notification generated where configured. | `AdoptionRequestService`, `PdfReportService`. | Other pending requests for same dog handled consistently. |
| EMAIL-04 Low stock report | Medium | Save resource at/below threshold or trigger low-stock check. | Shelter notification/email/PDF may be generated. | `ResourceStockService`. | Resource save should not fail because email fails. |
| EMAIL-05 Shelter summary report | Low | Trigger manual/scheduled summary if enabled. | Shelter summary PDF/email/report history created. | `ShelterSummaryReportService`, `ShelterSummaryReportJob`. | Quartz jobs may be disabled by settings. |
| EMAIL-06 Notification bell/page | Medium | Generate any notification, open `/notifications`. | Notification appears, can be marked read/opened/deleted. | `NotificationService`, `Components/Pages/Notifications.razor`. | Only own notifications visible. |
| EMAIL-07 Report history after export/report | Medium | Generate CSV/PDF export or report, open report history. | Report history row exists where implemented. | `ReportHistoryService`, `ExportService`. | Failed reports should be recorded as failed if service does that path. |

## 11. CSV Import/Export Scenarios

Only features confirmed by `CsvImportService`, `ExportService`, and pages are included.

| Scenario | Priority | Steps | Expected result | Scope | Watch for |
| -------- | -------- | ----- | --------------- | ----- | --------- |
| CSV-01 Admin users export | Medium | Admin opens `/admin/users`, clicks Export CSV. | CSV downloads without security fields. | Admin-wide. | No password hashes/tokens. |
| CSV-02 Admin shelters export/import shelter requests | Medium | Admin opens `/admin/shelters`, downloads template, previews CSV, confirms valid import, exports shelters. | Valid rows become pending shelter registration requests; export downloads approved shelter data. | Admin-wide. | Invalid rows block confirm. |
| CSV-03 Admin dogs export | Medium | Admin opens `/admin/dogs`, clicks Export CSV. | CSV downloads all dog records with formatted breed. | Admin-wide. | Breed should not be raw ID. |
| CSV-04 Admin adoption request export | Medium | Admin opens `/admin/adoption-requests`, clicks Export CSV/PDF. | Export downloads all adoption request records. | Admin-wide. | No UI freeze. |
| CSV-05 Shelter dog import/export | High | Shelter opens `/shelter/dogs`, downloads template, previews CSV, imports valid rows, exports. | Import validates rows and creates/updates own shelter dogs; export scoped to shelter. | Current shelter only. | Invalid image URLs rejected; breed/custom breed handled. |
| CSV-06 Shelter resource import/export | High | Shelter opens `/shelter/resources`, downloads template, previews CSV, imports valid rows, exports CSV/PDF. | Resources create/update for current shelter only. | Current shelter only. | Category/name/unit/quantity validation. |
| CSV-07 Shelter adoption request export | Medium | Shelter opens `/shelter/adoption-requests`, clicks Export CSV/PDF. | Export contains only current shelter adoption requests. | Current shelter only. | No other shelter adopter/request data. |
| CSV-08 Invalid CSV row validation | High | Upload CSV missing required headers or invalid values. | Preview shows validation errors and confirm is disabled. | Admin/shelter imports. | No partial import when errors exist. |

## 12. Final Pre-Presentation Checklist

Run these 30 minutes before the presentation.

| Check | Priority | Expected result | Status |
| ----- | -------- | --------------- | ------ |
| Database seeded | High | Demo accounts, shelters, dogs, resources, requests exist. | Not run |
| Demo accounts work | High | Admin, shelter, adopter login succeeds with `PawConnect123!`. | Not run |
| No broken images | High | Public dog cards and dog details show real image or polished placeholder. | Not run |
| Public dog details look polished | High | Gallery, breed info, food, medical, shelter cards render correctly. | Not run |
| Lightbox works | High | Click photo, navigate arrows, close. | Not run |
| Copilot core prompts work | High | At least apartment, cat, young children, sensitive dog, active yard prompts return believable results. | Not run |
| Copilot fallback understood | High | Know whether OpenAI is enabled; if disabled, explain rule-based fallback. | Not run |
| Recommendations work | Medium | `/adopter/recommendations` loads and shows reasons. | Not run |
| Adoption request flow works | High | Submit request, shelter confirms visit, adopter sees updated status. | Not run |
| Email/local SMTP ready if shown | Medium | Local inbox or SMTP catcher is open/configured. | Not run |
| Calendar invite ready if shown | Medium | Visit confirmation email has `.ics` attachment. | Not run |
| Admin Details buttons work | High | Admin adoption request Details opens dialog; Admin dog status history opens dialog. | Not run |
| CSV/PDF exports ready | Medium | At least one CSV and one PDF export downloads. | Not run |
| Resource validation works | Medium | Category placeholder is not `0`; invalid quantity blocked. | Not run |
| No fake visible text | High | No obvious `Demo`, `Test`, `Sample`, `Lorem`, broken URLs, or raw enum labels in demo pages. | Not run |

## Highest Priority Demo Path

Use this path if time is short:

1. Public: `/dogs` -> filters -> dog details -> gallery/lightbox -> Breed Information -> shelter map.
2. Adopter: log in as `ana.ionescu@pawconnect.local` -> recommendations -> Copilot prompts -> favorite dog -> submit adoption request -> My Adoption Requests.
3. Shelter: log in as `happy-paws@pawconnect.local` -> adoption requests -> Details -> Confirm Visit -> resources -> edit dog image/medical/status history.
4. Admin: log in as `admin@pawconnect.local` -> Admin Dogs -> status history -> rebuild dog search index -> Admin Adoption Requests Details -> report/activity history.

