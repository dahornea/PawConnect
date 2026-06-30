# Limitations and Future Work

## Technical Limitations

| Limitation | Why it is acceptable for a thesis | Future improvement |
| --- | --- | --- |
| Blazor Server depends on a live server connection. | It is appropriate for a C# full-stack thesis and keeps UI/backend logic unified. | Add load testing, scaling strategy, or consider API + SPA/mobile client for larger deployments. |
| Core flows are not REST API-first. | Blazor Server can call services directly, which is valid architecture. | Expose selected REST endpoints for mobile apps or external integrations. |
| External images are stored as URLs. | Simpler for demo data and shelter-managed images. | Add managed image upload/storage, thumbnail generation, and image moderation. |
| Scheduled jobs use application-hosted Quartz configuration. | Good for one-app thesis deployment. | Use persistent Quartz storage or external job scheduler for production. |
| SQL Server configuration is local/development oriented. | Fine for local thesis demo. | Add production deployment configuration, managed secrets, backups, and monitoring. |

## UX Limitations

| Limitation | Why acceptable | Future improvement |
| --- | --- | --- |
| Some pages are data-heavy. | Admin/shelter pages need operational density. | Add more pagination, saved filters, and improved mobile layouts. |
| Public dog cards depend on quality of images and descriptions. | Shelters provide varied content in real life. | Add image upload guidance, required description quality checks, and content preview scoring. |
| Breed information is educational only. | Correct and safe for adopters. | Add links to verified veterinary/breed resources. |

## AI / Copilot Limitations

| Limitation | Why acceptable | Future improvement |
| --- | --- | --- |
| Copilot quality depends on dog descriptions and behavior text. | The app uses public-safe text because it avoids private data. | Add structured compatibility fields for cats, children, other dogs, senior/sensitive dogs, activity level, and experience needed. |
| OpenAI is optional and depends on API availability. | The fallback logic means the feature still works without OpenAI. | Add queueing/caching and model monitoring in production. |
| Embeddings must be refreshed when dog content changes. | `DogService` and adoption status changes trigger best-effort refresh; admin can rebuild index. | Add background stale-index monitoring and automatic retry jobs. |
| AI can still misunderstand ambiguous user wording. | Backend filters and validation reduce risk. | Add clarification questions for ambiguous queries and AI evaluation datasets. |
| Copilot is advisory, not a guarantee of compatibility. | Adoption decisions require shelter confirmation. | Add explicit shelter verification workflows for compatibility criteria. |

## Testing Limitations

| Limitation | Why acceptable | Future improvement |
| --- | --- | --- |
| Tests are mostly service/domain tests, not full browser UI tests. | Service layer contains most business rules and is easier to test reliably. | Add Playwright or bUnit tests for key Blazor UI flows. |
| OpenAI tests use fake clients rather than live API. | Deterministic tests should not depend on external API/network. | Add optional manual/live integration checks outside normal CI. |
| EF Core InMemory does not exactly match SQL Server behavior. | Good for fast domain tests. | Add selected SQL Server integration tests for constraints/migrations. |

## Security Limitations

| Limitation | Why acceptable | Future improvement |
| --- | --- | --- |
| No advanced rate limiting was observed. | Thesis demo is not exposed as high-traffic production. | Add ASP.NET rate limiting and bot protection for public forms. |
| External API keys depend on configuration hygiene. | `OpenAiSettings` and appsettings support configuration, but deployment must manage secrets. | Use user secrets/Azure Key Vault/environment secrets in production. |
| External image URLs may leak client requests to remote hosts. | URL-based images are simple for demo. | Proxy/cache images through application-controlled storage. |
| AI provider receives sanitized candidate data. | This is minimized and validated. | Add explicit data retention policy and provider configuration review. |

## Scalability Limitations

| Limitation | Why acceptable | Future improvement |
| --- | --- | --- |
| Blazor Server circuits consume server resources per connected user. | Good for thesis-scale usage. | Scale out with SignalR backplane or redesign public pages as API/static-rendered where needed. |
| Semantic search stores embeddings as JSON in SQL. | Simple and explainable. | Use a vector database or SQL vector support for larger datasets. |
| Search/filter queries are EF Core based without advanced indexing everywhere. | Dataset is small in thesis/demo. | Add database indexes and full-text/vector search for larger datasets. |

## Future Work Roadmap

1. Add structured compatibility fields to dog profiles.
2. Add shelter-facing "Copilot evidence preview" to show how each dog is interpreted.
3. Add browser end-to-end tests for adoption request and Copilot flows.
4. Add managed image upload instead of only image URLs.
5. Add production deployment plan with secrets, HTTPS, logging, monitoring, backups.
6. Add AI evaluation tests with expected query/result rankings.
7. Add more analytics for adoption funnel and shelter response times.
8. Add adopter/shelter messaging if needed.
9. Add multilingual UI support for Romanian/English.
10. Add stronger accessibility audit and keyboard navigation checks.

## How to Explain Limitations Professionally

Say:

"For a bachelor thesis, I focused on a complete, working, role-based adoption platform with reliable service logic and AI safeguards. Some production concerns such as full browser test coverage, managed image storage, large-scale vector search, and deployment hardening are intentionally future work. The important point is that the current architecture already separates these concerns so they can be improved later."
