const supportedDogSearchKeys = [
  'search', 'breed', 'maxAge', 'size', 'location', 'status', 'sort', 'shelterId',
  'neighborhood', 'coatColor', 'catCompatibility', 'childrenCompatibility',
  'activityLevel', 'apartmentSuitability', 'page',
]

export function buildDogApiPath(searchParams: URLSearchParams, pageSize = 12) {
  const query = new URLSearchParams()
  for (const key of supportedDogSearchKeys) {
    const value = searchParams.get(key)?.trim()
    if (value) query.set(key, value)
  }
  query.set('pageSize', String(pageSize))
  return `/dogs?${query.toString()}`
}
