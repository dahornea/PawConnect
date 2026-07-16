import { ApiError, normalizeApiError } from '@/api/errors'

describe('normalizeApiError', () => {
  it('keeps the safe backend message and correlation id', async () => {
    const response = new Response(JSON.stringify({ message: 'Application already exists.' }), {
      status: 400,
      headers: { 'Content-Type': 'application/json', 'X-Correlation-ID': 'test-123' },
    })
    const error = await normalizeApiError(response)
    expect(error).toBeInstanceOf(ApiError)
    expect(error.message).toBe('Application already exists.')
    expect(error.correlationId).toBe('test-123')
  })

  it('uses a readable fallback for non-json responses', async () => {
    const error = await normalizeApiError(new Response('gateway failed', { status: 500 }))
    expect(error.message).toBe('PawConnect could not complete the request right now.')
  })
})
