import { buildDogApiPath } from '@/features/dogs/searchParams'

describe('buildDogApiPath', () => {
  it('serializes supported server filters and pagination', () => {
    const path = buildDogApiPath(new URLSearchParams({ search: 'black dog', size: 'Medium', page: '2' }))
    expect(path).toContain('search=black+dog')
    expect(path).toContain('size=Medium')
    expect(path).toContain('page=2')
    expect(path).toContain('pageSize=12')
  })

  it('does not forward unknown query parameters to the API', () => {
    const path = buildDogApiPath(new URLSearchParams({ returnTo: '/profile', size: 'Small' }))
    expect(path).not.toContain('returnTo')
    expect(path).toContain('size=Small')
  })
})
