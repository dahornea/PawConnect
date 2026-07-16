export type ApiProblem = {
  message: string
  status: number
  correlationId?: string
  fieldErrors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly correlationId?: string
  readonly fieldErrors?: Record<string, string[]>

  constructor(problem: ApiProblem) {
    super(problem.message)
    this.name = 'ApiError'
    this.status = problem.status
    this.correlationId = problem.correlationId
    this.fieldErrors = problem.fieldErrors
  }
}

const statusMessages: Record<number, string> = {
  400: 'Please review the information and try again.',
  401: 'Your session has expired. Please sign in again.',
  403: 'You do not have permission to perform this action.',
  404: 'The requested information could not be found.',
  409: 'This action conflicts with the latest saved information.',
  429: 'Too many requests were sent. Please wait a moment and try again.',
  500: 'PawConnect could not complete the request right now.',
}

export async function normalizeApiError(response: Response): Promise<ApiError> {
  const correlationId = response.headers.get('X-Correlation-ID') ?? undefined
  let payload: Record<string, unknown> | undefined

  try {
    payload = await response.json() as Record<string, unknown>
  } catch {
    payload = undefined
  }

  const message = typeof payload?.message === 'string'
    ? payload.message
    : typeof payload?.title === 'string'
      ? payload.title
      : statusMessages[response.status] || 'The backend is unavailable. Check that PawConnect is running.'

  const errors = payload?.errors
  const fieldErrors = errors && typeof errors === 'object'
    ? errors as Record<string, string[]>
    : undefined

  return new ApiError({ message, status: response.status, correlationId, fieldErrors })
}

export function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'An unexpected error occurred.'
}
