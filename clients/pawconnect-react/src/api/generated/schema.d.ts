export interface paths {
    "/api/v1/admin/summary": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdminPlatformSummaryApiDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/admin/analytics": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    days?: number;
                    shelterId?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdminAnalyticsSummaryApiDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/antiforgery": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AntiforgeryTokenApiDto"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/login": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["AdopterPortalLoginRequest"];
                    "text/json": components["schemas"]["AdopterPortalLoginRequest"];
                    "application/*+json": components["schemas"]["AdopterPortalLoginRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdopterPortalUserApiDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/me": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdopterPortalUserApiDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/auth/logout": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/adopter/profile": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdopterProfileApiDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["UpdateAdopterProfileApiRequest"];
                    "text/json": components["schemas"]["UpdateAdopterProfileApiRequest"];
                    "application/*+json": components["schemas"]["UpdateAdopterProfileApiRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdopterProfileApiDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/adoption-applications": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    page?: number;
                    pageSize?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdoptionApplicationApiDtoApiPagedResult"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/adoption-applications/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdoptionApplicationApiDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Not Found */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/dogs/{dogId}/adoption-applications": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    dogId: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["CreateAdoptionApplicationApiRequest"];
                    "text/json": components["schemas"]["CreateAdoptionApplicationApiRequest"];
                    "application/*+json": components["schemas"]["CreateAdoptionApplicationApiRequest"];
                };
            };
            responses: {
                /** @description Created */
                201: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdoptionApplicationApiDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/adoption-copilot/search": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["AdoptionCopilotApiRequest"];
                    "text/json": components["schemas"]["AdoptionCopilotApiRequest"];
                    "application/*+json": components["schemas"]["AdoptionCopilotApiRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["AdoptionCopilotResponseApiDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/bulk/shelter/dogs/status": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["BulkDogStatusUpdateRequest"];
                    "text/json": components["schemas"]["BulkDogStatusUpdateRequest"];
                    "application/*+json": components["schemas"]["BulkDogStatusUpdateRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["BulkActionResultDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/bulk/admin/notification-outbox": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["BulkNotificationOutboxRequest"];
                    "text/json": components["schemas"]["BulkNotificationOutboxRequest"];
                    "application/*+json": components["schemas"]["BulkNotificationOutboxRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["BulkActionResultDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/dogs": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    search?: string;
                    breed?: string;
                    maxAge?: number;
                    size?: components["schemas"]["DogSize"];
                    location?: string;
                    status?: components["schemas"]["DogStatus"];
                    sort?: components["schemas"]["DogSortOption"];
                    shelterId?: number;
                    neighborhood?: string;
                    coatColor?: string;
                    catCompatibility?: components["schemas"]["CatCompatibility"];
                    childrenCompatibility?: components["schemas"]["ChildrenCompatibility"];
                    activityLevel?: components["schemas"]["DogActivityLevel"];
                    apartmentSuitability?: components["schemas"]["ApartmentSuitability"];
                    page?: number;
                    pageSize?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogListItemApiDtoApiPagedResult"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/dogs/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogDetailsApiDto"];
                    };
                };
                /** @description Not Found */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/favorites": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogListItemApiDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/favorites/ids": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": number[];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/favorites/{dogId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    dogId: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        post?: never;
        delete: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    dogId: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/intelligence/summary": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["IntelligenceSummaryDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/intelligence/insights": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    severity?: components["schemas"]["IntelligenceSeverity"];
                    category?: components["schemas"]["IntelligenceCategory"];
                    status?: components["schemas"]["IntelligenceInsightStatus"];
                    entityType?: string;
                    search?: string;
                    includeSnoozed?: boolean;
                    page?: number;
                    pageSize?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["OperationalInsightDtoPagedResult"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/intelligence/insights/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["OperationalInsightDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Not Found */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/intelligence/insights/{id}/acknowledge": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/intelligence/insights/{id}/snooze": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["SnoozeInsightRequest"];
                    "text/json": components["schemas"]["SnoozeInsightRequest"];
                    "application/*+json": components["schemas"]["SnoozeInsightRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/intelligence/insights/{id}/resolve": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["ResolveInsightRequest"];
                    "text/json": components["schemas"]["ResolveInsightRequest"];
                    "application/*+json": components["schemas"]["ResolveInsightRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/intelligence/insights/{id}/reopen": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/intelligence/refresh": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["IntelligenceEvaluationResult"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Too Many Requests */
                429: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/shelter/intelligence": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["OperationalInsightDtoPagedResult"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/admin/intelligence": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["OperationalInsightDtoPagedResult"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/adopter/insights": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["OperationalInsightDtoPagedResult"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/notification-preferences": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["NotificationPreferenceApiDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["UpdateNotificationPreferenceApiRequest"][];
                    "text/json": components["schemas"]["UpdateNotificationPreferenceApiRequest"][];
                    "application/*+json": components["schemas"]["UpdateNotificationPreferenceApiRequest"][];
                };
            };
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/notifications": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    category?: components["schemas"]["NotificationCategory"];
                    readState?: components["schemas"]["NotificationReadState"];
                    search?: string;
                    count?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["NotificationCenterResultDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/notifications/unread-count": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["NotificationUnreadCountApiDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/notifications/{id}/read": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/notifications/{id}/unread": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/notifications/read-all": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/notifications/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/health": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/message-attachments/{attachmentId}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    attachmentId: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/Account/PerformExternalLogin": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "multipart/form-data": {
                        provider: string;
                        returnUrl: string;
                    };
                    "application/x-www-form-urlencoded": {
                        provider: string;
                        returnUrl: string;
                    };
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/Account/Logout": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "multipart/form-data": {
                        returnUrl: string;
                    };
                    "application/x-www-form-urlencoded": {
                        returnUrl: string;
                    };
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/Account/PasskeyCreationOptions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/Account/PasskeyRequestOptions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: {
                    username?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/Account/Manage/LinkExternalLogin": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "multipart/form-data": {
                        provider: string;
                    };
                    "application/x-www-form-urlencoded": {
                        provider: string;
                    };
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/Account/Manage/DownloadPersonalData": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/saved-searches": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedDogSearchDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["SavedDogSearchCreateRequest"];
                    "text/json": components["schemas"]["SavedDogSearchCreateRequest"];
                    "application/*+json": components["schemas"]["SavedDogSearchCreateRequest"];
                };
            };
            responses: {
                /** @description Created */
                201: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedDogSearchDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/saved-searches/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedDogSearchDetailsDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Not Found */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["SavedDogSearchUpdateRequest"];
                    "text/json": components["schemas"]["SavedDogSearchUpdateRequest"];
                    "application/*+json": components["schemas"]["SavedDogSearchUpdateRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedDogSearchDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        post?: never;
        delete: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/saved-searches/{id}/evaluate": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedDogSearchDetailsDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/saved-searches/{id}/alerts": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["SetSavedSearchAlertsRequest"];
                    "text/json": components["schemas"]["SetSavedSearchAlertsRequest"];
                    "application/*+json": components["schemas"]["SetSavedSearchAlertsRequest"];
                };
            };
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/saved-searches/matches/{matchId}/seen": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    matchId: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/saved-searches/matches/{matchId}/dismiss": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    matchId: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/saved-views": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    pageKey?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedViewDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["SavedViewCreateRequest"];
                    "text/json": components["schemas"]["SavedViewCreateRequest"];
                    "application/*+json": components["schemas"]["SavedViewCreateRequest"];
                };
            };
            responses: {
                /** @description Created */
                201: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedViewDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/saved-views/pinned": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedViewDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/saved-views/default": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    pageKey?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedViewDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Not Found */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/saved-views/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["SavedViewUpdateRequest"];
                    "text/json": components["schemas"]["SavedViewUpdateRequest"];
                    "application/*+json": components["schemas"]["SavedViewUpdateRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedViewDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        post?: never;
        delete: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/saved-views/{id}/rename": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["SavedViewRenameRequest"];
                    "text/json": components["schemas"]["SavedViewRenameRequest"];
                    "application/*+json": components["schemas"]["SavedViewRenameRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SavedViewDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/saved-views/{id}/default": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/saved-views/{id}/pin": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/saved-views/{id}/unpin": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description No Content */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/simulations/templates": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SimulationTemplateDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/simulations/scenarios": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SimulationScenarioListItemDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/simulations/scenarios/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SimulationScenarioListItemDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Not Found */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["UpdateSimulationScenarioRequest"];
                    "text/json": components["schemas"]["UpdateSimulationScenarioRequest"];
                    "application/*+json": components["schemas"]["UpdateSimulationScenarioRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/simulations/run": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["SimulationRunRequestDto"];
                    "text/json": components["schemas"]["SimulationRunRequestDto"];
                    "application/*+json": components["schemas"]["SimulationRunRequestDto"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SimulationResultDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/simulations/save-and-run": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["SimulationSaveRequestDto"];
                    "text/json": components["schemas"]["SimulationSaveRequestDto"];
                    "application/*+json": components["schemas"]["SimulationSaveRequestDto"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SimulationSavedRunDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/simulations/scenarios/{id}/rerun": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SimulationSavedRunDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/simulations/compare": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    firstScenarioId?: number;
                    secondScenarioId?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["SimulationComparisonDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/shelters": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    city?: string;
                    neighborhood?: string;
                    page?: number;
                    pageSize?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ShelterListItemApiDtoApiPagedResult"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/shelters/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ShelterDetailsApiDto"];
                    };
                };
                /** @description Not Found */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transfers/incoming": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferRequestDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transfers/outgoing": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferRequestDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transfers": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    Status?: components["schemas"]["DogTransferStatus"];
                    Priority?: components["schemas"]["DogTransferPriority"];
                    SourceShelterId?: number;
                    DestinationShelterId?: number;
                    Search?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferRequestDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["DogTransferCreateRequest"];
                    "text/json": components["schemas"]["DogTransferCreateRequest"];
                    "application/*+json": components["schemas"]["DogTransferCreateRequest"];
                };
            };
            responses: {
                /** @description Created */
                201: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferRequestDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transfers/stats": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferStatsDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transfers/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferDetailsDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Not Found */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/transfers/{id}/approve": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["DogTransferDecisionRequest"];
                    "text/json": components["schemas"]["DogTransferDecisionRequest"];
                    "application/*+json": components["schemas"]["DogTransferDecisionRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        trace?: never;
    };
    "/api/v1/transfers/{id}/reject": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["DogTransferDecisionRequest"];
                    "text/json": components["schemas"]["DogTransferDecisionRequest"];
                    "application/*+json": components["schemas"]["DogTransferDecisionRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        trace?: never;
    };
    "/api/v1/transfers/{id}/cancel": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["DogTransferDecisionRequest"];
                    "text/json": components["schemas"]["DogTransferDecisionRequest"];
                    "application/*+json": components["schemas"]["DogTransferDecisionRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        trace?: never;
    };
    "/api/v1/transfers/{id}/complete": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["DogTransferCompleteRequest"];
                    "text/json": components["schemas"]["DogTransferCompleteRequest"];
                    "application/*+json": components["schemas"]["DogTransferCompleteRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        trace?: never;
    };
    "/api/v1/transfers/{id}/admin-note": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["DogTransferAdminNoteRequest"];
                    "text/json": components["schemas"]["DogTransferAdminNoteRequest"];
                    "application/*+json": components["schemas"]["DogTransferAdminNoteRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        trace?: never;
    };
    "/api/v1/dogs/{dogId}/transfers": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    dogId: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["DogTransferHistoryItemDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ProblemDetails"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/volunteer-tasks": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    Status?: components["schemas"]["VolunteerTaskStatus"];
                    Category?: components["schemas"]["VolunteerTaskCategory"];
                    Priority?: components["schemas"]["VolunteerTaskPriority"];
                    ShelterId?: number;
                    AssignedVolunteerProfileId?: number;
                    Search?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDto"][];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["VolunteerTaskCreateRequest"];
                    "text/json": components["schemas"]["VolunteerTaskCreateRequest"];
                    "application/*+json": components["schemas"]["VolunteerTaskCreateRequest"];
                };
            };
            responses: {
                /** @description Created */
                201: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/volunteer-tasks/available": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    Status?: components["schemas"]["VolunteerTaskStatus"];
                    Category?: components["schemas"]["VolunteerTaskCategory"];
                    Priority?: components["schemas"]["VolunteerTaskPriority"];
                    ShelterId?: number;
                    AssignedVolunteerProfileId?: number;
                    Search?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/volunteer-tasks/stats": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskStatsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/volunteer-tasks/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDetailsDto"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Not Found */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
            };
        };
        put: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["VolunteerTaskUpdateRequest"];
                    "text/json": components["schemas"]["VolunteerTaskUpdateRequest"];
                    "application/*+json": components["schemas"]["VolunteerTaskUpdateRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/volunteer-tasks/volunteers": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerProfileDto"][];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/v1/volunteer-tasks/{id}/assign": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["VolunteerTaskAssignRequest"];
                    "text/json": components["schemas"]["VolunteerTaskAssignRequest"];
                    "application/*+json": components["schemas"]["VolunteerTaskAssignRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/volunteer-tasks/{id}/accept": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "text/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "application/*+json": components["schemas"]["VolunteerTaskActionRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/volunteer-tasks/{id}/start": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "text/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "application/*+json": components["schemas"]["VolunteerTaskActionRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/volunteer-tasks/{id}/complete": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "text/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "application/*+json": components["schemas"]["VolunteerTaskActionRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/volunteer-tasks/{id}/cancel": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "text/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "application/*+json": components["schemas"]["VolunteerTaskActionRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        trace?: never;
    };
    "/api/v1/volunteer-tasks/{id}/comments": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: number;
                };
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "text/json": components["schemas"]["VolunteerTaskActionRequest"];
                    "application/*+json": components["schemas"]["VolunteerTaskActionRequest"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["VolunteerTaskDetailsDto"];
                    };
                };
                /** @description Bad Request */
                400: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["ApiErrorResponse"];
                    };
                };
                /** @description Unauthorized */
                401: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description Forbidden */
                403: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        AdminAnalyticsSummaryApiDto: {
            rangeLabel?: string | null;
            /** Format: int32 */
            totalSummaryCards?: number;
            summaryCards?: components["schemas"]["AdminAnalyticsSummaryCardApiDto"][] | null;
            /** Format: int32 */
            submittedRequests?: number;
            /** Format: int32 */
            acceptedRequests?: number;
            /** Format: int32 */
            pendingRequests?: number;
            /** Format: int32 */
            shelterCount?: number;
        };
        AdminAnalyticsSummaryCardApiDto: {
            label?: string | null;
            value?: string | null;
            helperText?: string | null;
            tone?: string | null;
        };
        AdminPlatformSummaryApiDto: {
            /** Format: date-time */
            generatedAtUtc?: string;
            /** Format: int32 */
            totalShelters?: number;
            /** Format: int32 */
            publicDogs?: number;
            /** Format: int32 */
            adoptedDogs?: number;
            /** Format: int32 */
            inTreatmentDogs?: number;
            /** Format: int32 */
            pendingAdoptionApplications?: number;
            notificationOutbox?: components["schemas"]["NotificationOutboxSummaryApiDto"];
        };
        AdopterPortalLoginRequest: {
            email?: string | null;
            password?: string | null;
            rememberMe?: boolean;
        };
        AdopterPortalUserApiDto: {
            id?: string | null;
            email?: string | null;
            displayName?: string | null;
            roles?: string[] | null;
        };
        AdopterProfileApiDto: {
            fullName?: string | null;
            profileImageUrl?: string | null;
            address?: string | null;
            city?: string | null;
            phoneNumber?: string | null;
            housingType?: components["schemas"]["HousingType"];
            hasYard?: boolean;
            hasOtherPets?: boolean;
            hasChildren?: boolean;
            experienceWithDogs?: string | null;
            additionalNotes?: string | null;
        };
        AdoptionApplicationApiDto: {
            /** Format: int32 */
            id?: number;
            /** Format: int32 */
            dogId?: number;
            dogName?: string | null;
            dogBreed?: string | null;
            /** Format: int32 */
            shelterId?: number;
            shelterName?: string | null;
            status?: components["schemas"]["AdoptionRequestStatus"];
            visitStatus?: components["schemas"]["AdoptionVisitStatus"];
            /** Format: date-time */
            preferredVisitDateTime?: string | null;
            /** Format: date-time */
            visitConfirmedAt?: string | null;
            reasonForAdoption?: string | null;
            /** Format: int32 */
            hoursAlonePerDay?: number | null;
            additionalInformation?: string | null;
            /** Format: date-time */
            createdAt?: string;
            /** Format: date-time */
            updatedAt?: string;
        };
        AdoptionApplicationApiDtoApiPagedResult: {
            items?: components["schemas"]["AdoptionApplicationApiDto"][] | null;
            /** Format: int32 */
            page?: number;
            /** Format: int32 */
            pageSize?: number;
            /** Format: int32 */
            totalCount?: number;
            /** Format: int32 */
            totalPages?: number;
        };
        AdoptionCopilotApiRequest: {
            message?: string | null;
        };
        AdoptionCopilotConstraintApiDto: {
            label?: string | null;
            value?: string | null;
        };
        AdoptionCopilotResponseApiDto: {
            assistantMessage?: string | null;
            results?: components["schemas"]["AdoptionCopilotResultApiDto"][] | null;
            appliedConstraints?: components["schemas"]["AdoptionCopilotConstraintApiDto"][] | null;
            usedAiEnhancement?: boolean;
            usedSemanticSearch?: boolean;
            fallbackReason?: string | null;
        };
        AdoptionCopilotResultApiDto: {
            dog?: components["schemas"]["DogListItemApiDto"];
            /** Format: int32 */
            scorePercent?: number;
            matchLabel?: string | null;
            reasons?: string[] | null;
            displayTags?: string[] | null;
            cautionTags?: string[] | null;
            suggestedNextAction?: string | null;
            /** Format: double */
            distanceKm?: number | null;
        };
        /** @enum {string} */
        AdoptionRequestStatus: "Pending" | "Accepted" | "Rejected" | "Cancelled" | "VisitConfirmed";
        /** @enum {string} */
        AdoptionVisitStatus: "NotScheduled" | "Requested" | "Confirmed" | "Completed" | "Cancelled";
        AntiforgeryTokenApiDto: {
            token?: string | null;
            headerName?: string | null;
        };
        /** @enum {string} */
        ApartmentSuitability: "Unknown" | "Suitable" | "MaybeWithRoutine" | "NotRecommended";
        ApiErrorResponse: {
            message?: string | null;
            detail?: string | null;
        };
        BulkActionItemResultDto: {
            /** Format: int32 */
            entityId?: number;
            entityName?: string | null;
            status?: components["schemas"]["BulkActionItemStatus"];
            message?: string | null;
        };
        /** @enum {string} */
        BulkActionItemStatus: "Succeeded" | "Failed" | "Skipped";
        BulkActionResultDto: {
            /** Format: int32 */
            totalRequested?: number;
            /** Format: int32 */
            succeeded?: number;
            /** Format: int32 */
            failed?: number;
            /** Format: int32 */
            skipped?: number;
            message?: string | null;
            items?: components["schemas"]["BulkActionItemResultDto"][] | null;
        };
        BulkDogStatusUpdateRequest: {
            dogIds?: number[] | null;
            newStatus?: components["schemas"]["DogStatus"];
        };
        BulkNotificationOutboxRequest: {
            messageIds?: number[] | null;
            action?: string | null;
        };
        /** @enum {string} */
        CatCompatibility: "Unknown" | "Yes" | "SlowIntroductions" | "No";
        /** @enum {string} */
        ChildrenCompatibility: "Unknown" | "Yes" | "OlderChildrenOnly" | "No";
        CreateAdoptionApplicationApiRequest: {
            reasonForAdoption?: string | null;
            /** Format: int32 */
            hoursAlonePerDay?: number | null;
            /** Format: date-time */
            preferredVisitDateTime?: string | null;
            additionalInformation?: string | null;
        };
        /** @enum {string} */
        DogActivityLevel: "Unknown" | "Low" | "Medium" | "High";
        DogBreedInfoApiDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            generalDescription?: string | null;
            typicalTraits?: string | null;
            careNotes?: string | null;
            commonHealthConsiderations?: string | null;
        };
        /** @enum {string} */
        DogCompatibility: "Unknown" | "Yes" | "CalmDogsOnly" | "SlowIntroductions" | "OnlyDog" | "No";
        DogDetailsApiDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            breed?: string | null;
            coatColor?: string | null;
            /** Format: int32 */
            ageYears?: number;
            /** Format: int32 */
            ageMonths?: number;
            ageDisplay?: string | null;
            size?: components["schemas"]["DogSize"];
            status?: components["schemas"]["DogStatus"];
            location?: string | null;
            description?: string | null;
            behaviorDescription?: string | null;
            medicalStatus?: string | null;
            catCompatibility?: components["schemas"]["CatCompatibility"];
            dogCompatibility?: components["schemas"]["DogCompatibility"];
            childrenCompatibility?: components["schemas"]["ChildrenCompatibility"];
            activityLevel?: components["schemas"]["DogActivityLevel"];
            experienceNeeded?: components["schemas"]["DogExperienceNeeded"];
            apartmentSuitability?: components["schemas"]["ApartmentSuitability"];
            compatibilityNotes?: string | null;
            preferredFood?: components["schemas"]["FoodInfoApiDto"];
            shelter?: components["schemas"]["ShelterSummaryApiDto"];
            images?: components["schemas"]["DogImageApiDto"][] | null;
            medicalRecords?: components["schemas"]["MedicalRecordApiDto"][] | null;
            breedInformation?: components["schemas"]["DogBreedInfoApiDto"][] | null;
        };
        /** @enum {string} */
        DogExperienceNeeded: "Unknown" | "Beginner" | "SomeExperience" | "Experienced";
        DogImageApiDto: {
            /** Format: int32 */
            id?: number;
            imageUrl?: string | null;
            isMainImage?: boolean;
        };
        DogListItemApiDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            breed?: string | null;
            coatColor?: string | null;
            /** Format: int32 */
            ageYears?: number;
            /** Format: int32 */
            ageMonths?: number;
            ageDisplay?: string | null;
            size?: components["schemas"]["DogSize"];
            status?: components["schemas"]["DogStatus"];
            location?: string | null;
            /** Format: int32 */
            shelterId?: number;
            shelterName?: string | null;
            shelterNeighborhood?: string | null;
            mainImageUrl?: string | null;
            shortDescription?: string | null;
        };
        DogListItemApiDtoApiPagedResult: {
            items?: components["schemas"]["DogListItemApiDto"][] | null;
            /** Format: int32 */
            page?: number;
            /** Format: int32 */
            pageSize?: number;
            /** Format: int32 */
            totalCount?: number;
            /** Format: int32 */
            totalPages?: number;
        };
        /** @enum {string} */
        DogSize: "Small" | "Medium" | "Large";
        /** @enum {string} */
        DogSortOption: "NameAsc" | "NameDesc" | "AgeAsc" | "AgeDesc" | "BreedAsc" | "LocationAsc" | "Status" | "NewestFirst" | "NearestFirst";
        /** @enum {string} */
        DogStatus: "Available" | "Reserved" | "Adopted" | "InTreatment";
        DogTransferAdminNoteRequest: {
            adminNotes?: string | null;
        };
        DogTransferCompleteRequest: {
            notes?: string | null;
        };
        DogTransferCreateRequest: {
            /** Format: int32 */
            dogId?: number;
            /** Format: int32 */
            destinationShelterId?: number;
            priority?: components["schemas"]["DogTransferPriority"];
            reason?: string | null;
            sourceShelterNotes?: string | null;
        };
        DogTransferDecisionRequest: {
            notes?: string | null;
        };
        DogTransferDetailsDto: {
            /** Format: int32 */
            id?: number;
            /** Format: int32 */
            dogId?: number;
            dogName?: string | null;
            dogBreed?: string | null;
            status?: components["schemas"]["DogTransferStatus"];
            priority?: components["schemas"]["DogTransferPriority"];
            /** Format: int32 */
            sourceShelterId?: number;
            sourceShelterName?: string | null;
            /** Format: int32 */
            destinationShelterId?: number;
            destinationShelterName?: string | null;
            reason?: string | null;
            sourceShelterNotes?: string | null;
            destinationShelterResponseNotes?: string | null;
            adminNotes?: string | null;
            requestedByDisplayName?: string | null;
            respondedByDisplayName?: string | null;
            completedByDisplayName?: string | null;
            /** Format: date-time */
            requestedAtUtc?: string;
            /** Format: date-time */
            respondedAtUtc?: string | null;
            /** Format: date-time */
            completedAtUtc?: string | null;
            /** Format: date-time */
            cancelledAtUtc?: string | null;
            /** Format: date-time */
            updatedAtUtc?: string;
            canApprove?: boolean;
            canReject?: boolean;
            canCancel?: boolean;
            canComplete?: boolean;
        };
        DogTransferHistoryItemDto: {
            /** Format: int32 */
            id?: number;
            /** Format: int32 */
            dogId?: number;
            dogName?: string | null;
            status?: components["schemas"]["DogTransferStatus"];
            priority?: components["schemas"]["DogTransferPriority"];
            sourceShelterName?: string | null;
            destinationShelterName?: string | null;
            reasonPreview?: string | null;
            /** Format: date-time */
            requestedAtUtc?: string;
            /** Format: date-time */
            completedAtUtc?: string | null;
            /** Format: date-time */
            respondedAtUtc?: string | null;
            /** Format: date-time */
            cancelledAtUtc?: string | null;
        };
        /** @enum {string} */
        DogTransferPriority: "Low" | "Normal" | "High" | "Urgent";
        DogTransferRequestDto: {
            /** Format: int32 */
            id?: number;
            /** Format: int32 */
            dogId?: number;
            dogName?: string | null;
            dogBreed?: string | null;
            status?: components["schemas"]["DogTransferStatus"];
            priority?: components["schemas"]["DogTransferPriority"];
            /** Format: int32 */
            sourceShelterId?: number;
            sourceShelterName?: string | null;
            /** Format: int32 */
            destinationShelterId?: number;
            destinationShelterName?: string | null;
            reasonPreview?: string | null;
            /** Format: date-time */
            requestedAtUtc?: string;
            /** Format: date-time */
            respondedAtUtc?: string | null;
            /** Format: date-time */
            completedAtUtc?: string | null;
            /** Format: date-time */
            cancelledAtUtc?: string | null;
            canApprove?: boolean;
            canReject?: boolean;
            canCancel?: boolean;
            canComplete?: boolean;
        };
        DogTransferStatsDto: {
            /** Format: int32 */
            incomingPending?: number;
            /** Format: int32 */
            outgoingPending?: number;
            /** Format: int32 */
            approvedWaitingCompletion?: number;
            /** Format: int32 */
            completed?: number;
            /** Format: int32 */
            urgentRequests?: number;
            /** Format: int32 */
            total?: number;
        };
        /** @enum {string} */
        DogTransferStatus: "Pending" | "Approved" | "Rejected" | "Cancelled" | "Completed";
        FoodInfoApiDto: {
            /** Format: int32 */
            foodTypeId?: number;
            foodTypeName?: string | null;
            /** Format: int32 */
            dailyAmountGrams?: number | null;
        };
        /** @enum {string} */
        HousingType: "Apartment" | "House" | "Other";
        /** @enum {string} */
        IntelligenceAudienceType: "Admin" | "Shelter" | "Adopter";
        /** @enum {string} */
        IntelligenceCategory: "Adoption" | "DogProfileQuality" | "ApplicationReview" | "Workload" | "Notifications" | "Transfer" | "Volunteer" | "PlatformHealth" | "Matching" | "UserNextStep";
        IntelligenceEvaluationResult: {
            audienceType?: components["schemas"]["IntelligenceAudienceType"];
            userId?: string | null;
            /** Format: int32 */
            shelterId?: number | null;
            /** Format: int32 */
            providersEvaluated?: number;
            /** Format: int32 */
            providerFailures?: number;
            /** Format: int32 */
            signalsCollected?: number;
            /** Format: int32 */
            created?: number;
            /** Format: int32 */
            updated?: number;
            /** Format: int32 */
            resolved?: number;
            /** Format: date-time */
            evaluatedAtUtc?: string;
            /** Format: date-span */
            duration?: string;
        };
        /** @enum {string} */
        IntelligenceInsightStatus: "Active" | "Acknowledged" | "Snoozed" | "Resolved" | "Expired";
        IntelligenceScoreFactor: {
            label?: string | null;
            /** Format: int32 */
            points?: number;
            explanation?: string | null;
        };
        /** @enum {string} */
        IntelligenceSeverity: "Informational" | "Low" | "Medium" | "High" | "Critical";
        IntelligenceSummaryDto: {
            /** Format: int32 */
            total?: number;
            /** Format: int32 */
            critical?: number;
            /** Format: int32 */
            high?: number;
            /** Format: int32 */
            medium?: number;
            /** Format: int32 */
            acknowledged?: number;
            /** Format: int32 */
            snoozed?: number;
            /** Format: int32 */
            resolved?: number;
            /** Format: date-time */
            lastRefreshedAtUtc?: string | null;
            workloadLabel?: string | null;
            byCategory?: {
                /** Format: int32 */
                Adoption?: number;
                /** Format: int32 */
                DogProfileQuality?: number;
                /** Format: int32 */
                ApplicationReview?: number;
                /** Format: int32 */
                Workload?: number;
                /** Format: int32 */
                Notifications?: number;
                /** Format: int32 */
                Transfer?: number;
                /** Format: int32 */
                Volunteer?: number;
                /** Format: int32 */
                PlatformHealth?: number;
                /** Format: int32 */
                Matching?: number;
                /** Format: int32 */
                UserNextStep?: number;
            } | null;
        };
        MedicalRecordApiDto: {
            /** Format: int32 */
            id?: number;
            vaccineName?: string | null;
            treatmentDescription?: string | null;
            notes?: string | null;
            /** Format: date-time */
            recordDate?: string;
        };
        /** @enum {string} */
        NotificationCategory: "Adoption" | "ShelterApplication" | "Resource" | "Report" | "System" | "Transfer" | "Volunteer" | "SavedSearch";
        NotificationCenterGroupDto: {
            label?: string | null;
            items?: components["schemas"]["NotificationCenterItemDto"][] | null;
        };
        NotificationCenterItemDto: {
            /** Format: int32 */
            id?: number;
            title?: string | null;
            message?: string | null;
            category?: components["schemas"]["NotificationCategory"];
            type?: components["schemas"]["NotificationType"];
            isRead?: boolean;
            /** Format: date-time */
            createdAtUtc?: string;
            /** Format: date-time */
            readAtUtc?: string | null;
            relatedEntityType?: string | null;
            relatedEntityId?: string | null;
            relatedEntityDisplayName?: string | null;
            relatedUrl?: string | null;
            categoryLabel?: string | null;
            severityLabel?: string | null;
            icon?: string | null;
            actionLabel?: string | null;
            metadataSummary?: string | null;
            timeGroup?: string | null;
            relativeTime?: string | null;
        };
        NotificationCenterResultDto: {
            groups?: components["schemas"]["NotificationCenterGroupDto"][] | null;
            /** Format: int32 */
            totalCount?: number;
            /** Format: int32 */
            unreadCount?: number;
            availableCategories?: components["schemas"]["NotificationCategory"][] | null;
        };
        /** @enum {string} */
        NotificationEventType: "AdoptionRequestUpdates" | "VisitReminders" | "Messages" | "ResourceAlerts" | "ReportUpdates" | "ShelterApplicationUpdates" | "LostFoundUpdates" | "SystemAnnouncements" | "DogTransferUpdates" | "VolunteerTaskUpdates" | "SavedSearchMatches";
        NotificationOutboxSummaryApiDto: {
            /** Format: int32 */
            total?: number;
            /** Format: int32 */
            pending?: number;
            /** Format: int32 */
            processing?: number;
            /** Format: int32 */
            sent?: number;
            /** Format: int32 */
            failed?: number;
            /** Format: int32 */
            deadLetter?: number;
            /** Format: int32 */
            cancelled?: number;
        };
        NotificationPreferenceApiDto: {
            notificationType?: components["schemas"]["NotificationEventType"];
            displayName?: string | null;
            description?: string | null;
            inAppEnabled?: boolean;
            emailEnabled?: boolean;
            defaultInAppEnabled?: boolean;
            defaultEmailEnabled?: boolean;
        };
        /** @enum {string} */
        NotificationReadState: "All" | "Unread" | "Read";
        /** @enum {string} */
        NotificationType: "Info" | "Success" | "Warning" | "Error";
        NotificationUnreadCountApiDto: {
            /** Format: int32 */
            count?: number;
        };
        OperationalInsightDto: {
            /** Format: int32 */
            id?: number;
            audienceType?: components["schemas"]["IntelligenceAudienceType"];
            userId?: string | null;
            /** Format: int32 */
            shelterId?: number | null;
            category?: components["schemas"]["IntelligenceCategory"];
            insightType?: string | null;
            sourceModule?: string | null;
            entityType?: string | null;
            entityId?: string | null;
            entityDisplayName?: string | null;
            title?: string | null;
            summary?: string | null;
            severity?: components["schemas"]["IntelligenceSeverity"];
            /** Format: int32 */
            priorityScore?: number;
            confidenceLabel?: string | null;
            explanation?: string | null;
            evidence?: string[] | null;
            scoreBreakdown?: components["schemas"]["IntelligenceScoreFactor"][] | null;
            recommendedActions?: components["schemas"]["RecommendedActionDto"][] | null;
            status?: components["schemas"]["IntelligenceInsightStatus"];
            /** Format: date-time */
            firstDetectedAtUtc?: string;
            /** Format: date-time */
            lastDetectedAtUtc?: string;
            /** Format: date-time */
            lastEvaluatedAtUtc?: string;
            /** Format: date-time */
            acknowledgedAtUtc?: string | null;
            /** Format: date-time */
            snoozedUntilUtc?: string | null;
            /** Format: date-time */
            resolvedAtUtc?: string | null;
            resolutionReason?: string | null;
        };
        OperationalInsightDtoPagedResult: {
            items?: components["schemas"]["OperationalInsightDto"][] | null;
            /** Format: int32 */
            page?: number;
            /** Format: int32 */
            pageSize?: number;
            /** Format: int32 */
            totalCount?: number;
            /** Format: int32 */
            totalPages?: number;
        };
        ProblemDetails: {
            type?: string | null;
            title?: string | null;
            /** Format: int32 */
            status?: number | null;
            detail?: string | null;
            instance?: string | null;
        } & {
            [key: string]: unknown;
        };
        RecommendedActionDto: {
            key?: string | null;
            label?: string | null;
            description?: string | null;
            actionType?: string | null;
            route?: string | null;
            requiredRole?: string | null;
            entityType?: string | null;
            entityId?: string | null;
            isPrimary?: boolean;
            requiresConfirmation?: boolean;
            isAvailable?: boolean;
            unavailableReason?: string | null;
        };
        ResolveInsightRequest: {
            reason?: string | null;
        };
        SavedDogSearchCreateRequest: {
            name?: string | null;
            criteria?: components["schemas"]["SavedDogSearchCriteriaDto"];
            alertsEnabled?: boolean;
            alertFrequency?: components["schemas"]["SavedSearchAlertFrequency"];
        };
        SavedDogSearchCriteriaDto: {
            searchText?: string | null;
            /** Format: int32 */
            shelterId?: number | null;
            breed?: string | null;
            coatColor?: string | null;
            /** Format: int32 */
            maxAgeYears?: number | null;
            size?: components["schemas"]["DogSize"];
            location?: string | null;
            neighborhood?: string | null;
            status?: components["schemas"]["DogStatus"];
            catCompatibility?: components["schemas"]["CatCompatibility"];
            childrenCompatibility?: components["schemas"]["ChildrenCompatibility"];
            activityLevel?: components["schemas"]["DogActivityLevel"];
            apartmentSuitability?: components["schemas"]["ApartmentSuitability"];
            sortOption?: components["schemas"]["DogSortOption"];
            nearbyLabel?: string | null;
            /** Format: double */
            nearbyLatitude?: number | null;
            /** Format: double */
            nearbyLongitude?: number | null;
            /** Format: int32 */
            radiusKm?: number | null;
        };
        SavedDogSearchDetailsDto: {
            search?: components["schemas"]["SavedDogSearchDto"];
            matches?: components["schemas"]["SavedSearchMatchDto"][] | null;
        };
        SavedDogSearchDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            alertsEnabled?: boolean;
            alertFrequency?: components["schemas"]["SavedSearchAlertFrequency"];
            /** Format: date-time */
            createdAtUtc?: string;
            /** Format: date-time */
            updatedAtUtc?: string;
            /** Format: date-time */
            lastEvaluatedAtUtc?: string | null;
            /** Format: date-time */
            lastMatchAtUtc?: string | null;
            criteriaLabels?: string[] | null;
            /** Format: int32 */
            totalMatches?: number;
            /** Format: int32 */
            newMatches?: number;
        };
        SavedDogSearchUpdateRequest: {
            name?: string | null;
            criteria?: components["schemas"]["SavedDogSearchCriteriaDto"];
            alertsEnabled?: boolean;
            alertFrequency?: components["schemas"]["SavedSearchAlertFrequency"];
        };
        /** @enum {string} */
        SavedSearchAlertFrequency: "Immediate" | "DailyDigest" | "Disabled";
        SavedSearchMatchDto: {
            /** Format: int32 */
            id?: number;
            /** Format: int32 */
            dogId?: number;
            dogName?: string | null;
            breedText?: string | null;
            ageText?: string | null;
            size?: components["schemas"]["DogSize"];
            location?: string | null;
            status?: components["schemas"]["DogStatus"];
            shelterName?: string | null;
            shelterNeighborhood?: string | null;
            mainImageUrl?: string | null;
            /** Format: int32 */
            matchScore?: number;
            statusInSearch?: components["schemas"]["SavedSearchMatchStatus"];
            matchReasons?: string[] | null;
            /** Format: date-time */
            firstMatchedAtUtc?: string;
            /** Format: date-time */
            lastMatchedAtUtc?: string;
        };
        /** @enum {string} */
        SavedSearchMatchStatus: "New" | "Seen" | "Dismissed" | "NoLongerMatching";
        SavedViewCreateRequest: {
            name?: string | null;
            pageKey?: string | null;
            roleScope?: components["schemas"]["SavedViewRoleScope"];
            description?: string | null;
            filterStateJson?: string | null;
            sortStateJson?: string | null;
            columnStateJson?: string | null;
            viewMode?: string | null;
            isPinned?: boolean;
            isDefault?: boolean;
            summaryLabels?: string[] | null;
        };
        SavedViewDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            pageKey?: string | null;
            roleScope?: components["schemas"]["SavedViewRoleScope"];
            description?: string | null;
            filterStateJson?: string | null;
            sortStateJson?: string | null;
            columnStateJson?: string | null;
            viewMode?: string | null;
            isPinned?: boolean;
            isDefault?: boolean;
            isSystemView?: boolean;
            /** Format: date-time */
            createdAtUtc?: string;
            /** Format: date-time */
            updatedAtUtc?: string;
            /** Format: date-time */
            lastUsedAtUtc?: string | null;
            summaryLabels?: string[] | null;
        };
        SavedViewRenameRequest: {
            name?: string | null;
        };
        /** @enum {string} */
        SavedViewRoleScope: "Global" | "Admin" | "Shelter" | "Adopter" | "Volunteer" | "Foster";
        SavedViewUpdateRequest: {
            name?: string | null;
            description?: string | null;
            filterStateJson?: string | null;
            sortStateJson?: string | null;
            columnStateJson?: string | null;
            viewMode?: string | null;
            isPinned?: boolean;
            isDefault?: boolean;
            summaryLabels?: string[] | null;
        };
        SetSavedSearchAlertsRequest: {
            enabled?: boolean;
        };
        ShelterDetailsApiDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            description?: string | null;
            address?: string | null;
            city?: string | null;
            neighborhood?: string | null;
            phoneNumber?: string | null;
            email?: string | null;
            /** Format: double */
            latitude?: number | null;
            /** Format: double */
            longitude?: number | null;
            visitSchedule?: components["schemas"]["ShelterVisitScheduleApiDto"];
            dogs?: components["schemas"]["DogListItemApiDto"][] | null;
        };
        ShelterListItemApiDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            description?: string | null;
            address?: string | null;
            city?: string | null;
            neighborhood?: string | null;
            phoneNumber?: string | null;
            email?: string | null;
            /** Format: double */
            latitude?: number | null;
            /** Format: double */
            longitude?: number | null;
            /** Format: int32 */
            publicDogCount?: number;
        };
        ShelterListItemApiDtoApiPagedResult: {
            items?: components["schemas"]["ShelterListItemApiDto"][] | null;
            /** Format: int32 */
            page?: number;
            /** Format: int32 */
            pageSize?: number;
            /** Format: int32 */
            totalCount?: number;
            /** Format: int32 */
            totalPages?: number;
        };
        ShelterSummaryApiDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            city?: string | null;
            neighborhood?: string | null;
            email?: string | null;
            phoneNumber?: string | null;
            /** Format: double */
            latitude?: number | null;
            /** Format: double */
            longitude?: number | null;
        };
        ShelterVisitScheduleApiDto: {
            startTime?: string | null;
            endTime?: string | null;
            visitDays?: string[] | null;
        };
        SimulationAssumptionDto: {
            type?: components["schemas"]["SimulationAssumptionType"];
            /** Format: int32 */
            quantity?: number;
            /** Format: int32 */
            effectiveDay?: number;
            notes?: string | null;
        };
        /** @enum {string} */
        SimulationAssumptionType: "DogIntake" | "VolunteerUnavailable" | "VolunteerAdded" | "IncomingTransfer" | "OutgoingTransfer" | "ApplicationsReviewed" | "NewApplications" | "ProfileImprovement" | "NotificationFailuresAdded" | "NotificationBacklogCleared" | "TemporaryCapacityChange" | "TemporaryCapacityUnavailable";
        SimulationBaselineDto: {
            /** Format: int32 */
            shelterId?: number | null;
            scopeName?: string | null;
            scopeType?: components["schemas"]["SimulationScopeType"];
            /** Format: int32 */
            dogCapacity?: number;
            /** Format: int32 */
            reservedEmergencySpaces?: number;
            /** Format: int32 */
            currentDogs?: number;
            /** Format: int32 */
            specialNeedsDogs?: number;
            /** Format: int32 */
            activeVolunteers?: number;
            /** Format: int32 */
            openVolunteerTasks?: number;
            /** Format: int32 */
            overdueVolunteerTasks?: number;
            /** Format: int32 */
            pendingApplications?: number;
            /** Format: int32 */
            incompleteProfiles?: number;
            /** Format: int32 */
            incomingTransfers?: number;
            /** Format: int32 */
            outgoingTransfers?: number;
            /** Format: int32 */
            failedNotifications?: number;
            /** Format: date-time */
            capturedAtUtc?: string;
        };
        SimulationComparisonDto: {
            first?: components["schemas"]["SimulationScenarioListItemDto"];
            second?: components["schemas"]["SimulationScenarioListItemDto"];
            firstResult?: components["schemas"]["SimulationResultDto"];
            secondResult?: components["schemas"]["SimulationResultDto"];
            summary?: string | null;
        };
        SimulationDimensionResultDto: {
            key?: string | null;
            label?: string | null;
            /** Format: int32 */
            baselineScore?: number;
            /** Format: int32 */
            projectedScore?: number;
            baselineLabel?: string | null;
            projectedLabel?: string | null;
            explanation?: string | null;
        };
        /** @enum {string} */
        SimulationImpactType: "NewRisk" | "EscalatedRisk" | "UnchangedRisk" | "ReducedRisk" | "ResolvedRisk" | "NewOpportunity";
        SimulationRecommendationDto: {
            key?: string | null;
            title?: string | null;
            explanation?: string | null;
            route?: string | null;
            requiredRole?: string | null;
            /** Format: int32 */
            priority?: number;
        };
        SimulationResultDto: {
            baseline?: components["schemas"]["SimulationBaselineDto"];
            projectedState?: components["schemas"]["SimulationStateDto"];
            /** Format: int32 */
            horizonDays?: number;
            /** Format: int32 */
            baselineWorkloadScore?: number;
            /** Format: int32 */
            projectedWorkloadScore?: number;
            dimensions?: components["schemas"]["SimulationDimensionResultDto"][] | null;
            workloadFactors?: components["schemas"]["SimulationWorkloadFactorDto"][] | null;
            baselineRisks?: components["schemas"]["SimulationRiskDto"][] | null;
            projectedRisks?: components["schemas"]["SimulationRiskDto"][] | null;
            riskChanges?: components["schemas"]["SimulationRiskChangeDto"][] | null;
            recommendations?: components["schemas"]["SimulationRecommendationDto"][] | null;
            timeline?: components["schemas"]["SimulationTimelinePointDto"][] | null;
            appliedAssumptions?: components["schemas"]["SimulationAssumptionDto"][] | null;
            /** Format: date-time */
            generatedAtUtc?: string;
            engineVersion?: string | null;
        };
        SimulationRiskChangeDto: {
            key?: string | null;
            title?: string | null;
            impact?: components["schemas"]["SimulationImpactType"];
            baseline?: components["schemas"]["SimulationRiskDto"];
            projected?: components["schemas"]["SimulationRiskDto"];
            explanation?: string | null;
        };
        SimulationRiskDto: {
            key?: string | null;
            title?: string | null;
            category?: components["schemas"]["IntelligenceCategory"];
            severity?: components["schemas"]["IntelligenceSeverity"];
            /** Format: int32 */
            score?: number;
            explanation?: string | null;
            evidence?: string[] | null;
            /** Format: int32 */
            effectiveDay?: number;
        };
        SimulationRunRequestDto: {
            /** Format: int32 */
            shelterId?: number | null;
            scopeType?: components["schemas"]["SimulationScopeType"];
            /** Format: int32 */
            horizonDays?: number;
            assumptions?: components["schemas"]["SimulationAssumptionDto"][] | null;
        };
        SimulationSaveRequestDto: {
            /** Format: int32 */
            scenarioId?: number | null;
            name?: string | null;
            description?: string | null;
            request?: components["schemas"]["SimulationRunRequestDto"];
            isPinned?: boolean;
        };
        SimulationSavedRunDto: {
            /** Format: int32 */
            scenarioId?: number;
            /** Format: int32 */
            runId?: number;
            result?: components["schemas"]["SimulationResultDto"];
        };
        SimulationScenarioListItemDto: {
            /** Format: int32 */
            id?: number;
            name?: string | null;
            description?: string | null;
            /** Format: int32 */
            shelterId?: number | null;
            shelterName?: string | null;
            scopeType?: components["schemas"]["SimulationScopeType"];
            /** Format: int32 */
            horizonDays?: number;
            status?: components["schemas"]["SimulationScenarioStatus"];
            assumptions?: components["schemas"]["SimulationAssumptionDto"][] | null;
            isPinned?: boolean;
            isTemplate?: boolean;
            /** Format: date-time */
            lastRunAtUtc?: string | null;
            /** Format: date-time */
            updatedAtUtc?: string;
        };
        /** @enum {string} */
        SimulationScenarioStatus: "Draft" | "Completed" | "Archived";
        /** @enum {string} */
        SimulationScopeType: "Shelter" | "Platform";
        SimulationStateDto: {
            /** Format: int32 */
            dogCapacity?: number;
            /** Format: int32 */
            reservedEmergencySpaces?: number;
            /** Format: int32 */
            currentDogs?: number;
            /** Format: int32 */
            specialNeedsDogs?: number;
            /** Format: int32 */
            activeVolunteers?: number;
            /** Format: int32 */
            openVolunteerTasks?: number;
            /** Format: int32 */
            overdueVolunteerTasks?: number;
            /** Format: int32 */
            pendingApplications?: number;
            /** Format: int32 */
            incompleteProfiles?: number;
            /** Format: int32 */
            incomingTransfers?: number;
            /** Format: int32 */
            outgoingTransfers?: number;
            /** Format: int32 */
            failedNotifications?: number;
            /** Format: int32 */
            readonly normalCapacity?: number;
            /** Format: int32 */
            readonly availableNormalSpaces?: number;
            /** Format: int32 */
            readonly occupancyPercent?: number;
        };
        SimulationTemplateDto: {
            key?: string | null;
            name?: string | null;
            description?: string | null;
            /** Format: int32 */
            horizonDays?: number;
            assumptions?: components["schemas"]["SimulationAssumptionDto"][] | null;
        };
        SimulationTimelinePointDto: {
            /** Format: int32 */
            day?: number;
            /** Format: int32 */
            currentDogs?: number;
            /** Format: int32 */
            availableSpaces?: number;
            /** Format: int32 */
            workloadScore?: number;
            /** Format: int32 */
            riskCount?: number;
            appliedAssumptions?: string[] | null;
        };
        SimulationWorkloadFactorDto: {
            label?: string | null;
            /** Format: int32 */
            baselineContribution?: number;
            /** Format: int32 */
            projectedContribution?: number;
            formula?: string | null;
        };
        SnoozeInsightRequest: {
            /** Format: double */
            hours?: number;
        };
        UpdateAdopterProfileApiRequest: {
            fullName?: string | null;
            profileImageUrl?: string | null;
            address?: string | null;
            city?: string | null;
            phoneNumber?: string | null;
            housingType?: components["schemas"]["HousingType"];
            hasYard?: boolean;
            hasOtherPets?: boolean;
            hasChildren?: boolean;
            experienceWithDogs?: string | null;
            additionalNotes?: string | null;
        };
        UpdateNotificationPreferenceApiRequest: {
            notificationType?: components["schemas"]["NotificationEventType"];
            inAppEnabled?: boolean;
            emailEnabled?: boolean;
        };
        UpdateSimulationScenarioRequest: {
            name?: string | null;
            isPinned?: boolean | null;
        };
        VolunteerProfileDto: {
            /** Format: int32 */
            id?: number;
            userId?: string | null;
            displayName?: string | null;
            email?: string | null;
            phoneNumber?: string | null;
            /** Format: int32 */
            preferredShelterId?: number | null;
            preferredShelterName?: string | null;
            skills?: string | null;
            availabilityNotes?: string | null;
            isActive?: boolean;
        };
        VolunteerTaskActionRequest: {
            notes?: string | null;
        };
        VolunteerTaskActivityDto: {
            /** Format: int32 */
            id?: number;
            activityType?: components["schemas"]["VolunteerTaskActivityType"];
            message?: string | null;
            actorDisplayName?: string | null;
            /** Format: date-time */
            createdAtUtc?: string;
        };
        /** @enum {string} */
        VolunteerTaskActivityType: "Created" | "Assigned" | "Accepted" | "Started" | "Completed" | "Cancelled" | "Reopened" | "CommentAdded";
        VolunteerTaskAssignRequest: {
            /** Format: int32 */
            volunteerProfileId?: number | null;
            notes?: string | null;
        };
        /** @enum {string} */
        VolunteerTaskCategory: "DogWalking" | "Feeding" | "Cleaning" | "Transport" | "MedicalVisitSupport" | "AdoptionEventSupport" | "Socialization" | "Grooming" | "Administrative" | "Other";
        VolunteerTaskCreateRequest: {
            title?: string | null;
            description?: string | null;
            category?: components["schemas"]["VolunteerTaskCategory"];
            priority?: components["schemas"]["VolunteerTaskPriority"];
            /** Format: date-time */
            scheduledStartUtc?: string;
            /** Format: date-time */
            scheduledEndUtc?: string;
            /** Format: date-time */
            dueAtUtc?: string | null;
            /** Format: int32 */
            dogId?: number | null;
            /** Format: int32 */
            assignedVolunteerProfileId?: number | null;
            location?: string | null;
            requiredSkills?: string | null;
            shelterNotes?: string | null;
        };
        VolunteerTaskDetailsDto: {
            /** Format: int32 */
            id?: number;
            /** Format: int32 */
            shelterId?: number;
            shelterName?: string | null;
            /** Format: int32 */
            dogId?: number | null;
            dogName?: string | null;
            dogBreed?: string | null;
            title?: string | null;
            description?: string | null;
            category?: components["schemas"]["VolunteerTaskCategory"];
            status?: components["schemas"]["VolunteerTaskStatus"];
            priority?: components["schemas"]["VolunteerTaskPriority"];
            /** Format: date-time */
            scheduledStartUtc?: string;
            /** Format: date-time */
            scheduledEndUtc?: string;
            /** Format: date-time */
            dueAtUtc?: string | null;
            location?: string | null;
            requiredSkills?: string | null;
            shelterNotes?: string | null;
            volunteerNotes?: string | null;
            completionNotes?: string | null;
            /** Format: int32 */
            assignedVolunteerProfileId?: number | null;
            assignedVolunteerName?: string | null;
            createdByDisplayName?: string | null;
            /** Format: date-time */
            assignedAtUtc?: string | null;
            /** Format: date-time */
            startedAtUtc?: string | null;
            /** Format: date-time */
            completedAtUtc?: string | null;
            /** Format: date-time */
            cancelledAtUtc?: string | null;
            /** Format: date-time */
            createdAtUtc?: string;
            /** Format: date-time */
            updatedAtUtc?: string;
            canAssign?: boolean;
            canAccept?: boolean;
            canStart?: boolean;
            canComplete?: boolean;
            canCancel?: boolean;
            activities?: components["schemas"]["VolunteerTaskActivityDto"][] | null;
        };
        VolunteerTaskDto: {
            /** Format: int32 */
            id?: number;
            /** Format: int32 */
            shelterId?: number;
            shelterName?: string | null;
            /** Format: int32 */
            dogId?: number | null;
            dogName?: string | null;
            title?: string | null;
            description?: string | null;
            category?: components["schemas"]["VolunteerTaskCategory"];
            status?: components["schemas"]["VolunteerTaskStatus"];
            priority?: components["schemas"]["VolunteerTaskPriority"];
            /** Format: date-time */
            scheduledStartUtc?: string;
            /** Format: date-time */
            scheduledEndUtc?: string;
            /** Format: date-time */
            dueAtUtc?: string | null;
            location?: string | null;
            requiredSkills?: string | null;
            /** Format: int32 */
            assignedVolunteerProfileId?: number | null;
            assignedVolunteerName?: string | null;
            /** Format: date-time */
            assignedAtUtc?: string | null;
            /** Format: date-time */
            startedAtUtc?: string | null;
            /** Format: date-time */
            completedAtUtc?: string | null;
            /** Format: date-time */
            cancelledAtUtc?: string | null;
            canAssign?: boolean;
            canAccept?: boolean;
            canStart?: boolean;
            canComplete?: boolean;
            canCancel?: boolean;
        };
        /** @enum {string} */
        VolunteerTaskPriority: "Low" | "Normal" | "High" | "Urgent";
        VolunteerTaskStatsDto: {
            /** Format: int32 */
            openTasks?: number;
            /** Format: int32 */
            assignedTasks?: number;
            /** Format: int32 */
            inProgressTasks?: number;
            /** Format: int32 */
            completedThisWeek?: number;
            /** Format: int32 */
            overdueTasks?: number;
            /** Format: int32 */
            activeVolunteers?: number;
            /** Format: int32 */
            totalTasks?: number;
        };
        /** @enum {string} */
        VolunteerTaskStatus: "Open" | "Assigned" | "InProgress" | "Completed" | "Cancelled";
        VolunteerTaskUpdateRequest: {
            title?: string | null;
            description?: string | null;
            category?: components["schemas"]["VolunteerTaskCategory"];
            priority?: components["schemas"]["VolunteerTaskPriority"];
            /** Format: date-time */
            scheduledStartUtc?: string;
            /** Format: date-time */
            scheduledEndUtc?: string;
            /** Format: date-time */
            dueAtUtc?: string | null;
            /** Format: int32 */
            dogId?: number | null;
            location?: string | null;
            requiredSkills?: string | null;
            shelterNotes?: string | null;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export type operations = Record<string, never>;
