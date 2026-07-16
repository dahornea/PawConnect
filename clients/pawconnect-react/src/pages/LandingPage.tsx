import { useQuery } from '@tanstack/react-query'
import { ArrowRight, HeartHandshake, Search, ShieldCheck } from 'lucide-react'
import { Link } from 'react-router-dom'
import { api } from '@/api/client'
import { queryKeys } from '@/api/queryKeys'
import type { DogPage } from '@/api/types'
import { DogGrid } from '@/components/dogs/DogGrid'

export function LandingPage() {
  const dogsQuery = useQuery({
    queryKey: queryKeys.dogs('landing'),
    queryFn: ({ signal }) => api.get<DogPage>('/dogs?page=1&pageSize=24&sort=NameAsc', signal),
  })
  const allDogs = dogsQuery.data?.items ?? []
  const dogs = [
    ...allDogs.filter((dog) => dog.mainImageUrl),
    ...allDogs.filter((dog) => !dog.mainImageUrl),
  ].slice(0, 3)
  const heroImage = dogs.find((dog) => dog.mainImageUrl)?.mainImageUrl

  return (
    <>
      <section className="home-hero" style={heroImage ? { backgroundImage: `url("${heroImage}")` } : undefined}>
        <div className="container home-hero__content">
          <span className="eyebrow">Dog adoption, with better context</span>
          <h1>Find a dog who fits your real life.</h1>
          <p>Explore public shelter profiles, compare practical compatibility details, and manage every adoption step in one place.</p>
          <div className="hero-actions">
            <Link className="button button--primary button--md" to="/dogs">Browse dogs <ArrowRight /></Link>
            <Link className="button button--secondary button--md" to="/copilot">Try Adoption Copilot</Link>
          </div>
        </div>
      </section>

      <section className="feature-band">
        <div className="container feature-grid">
          <div><Search /><h2>Discover clearly</h2><p>Use server-side filters and public-safe shelter data to narrow the search.</p></div>
          <div><HeartHandshake /><h2>Apply thoughtfully</h2><p>Review the dog profile before sending a trackable adoption application.</p></div>
          <div><ShieldCheck /><h2>Stay informed</h2><p>Favorites, saved searches, notifications, and status updates stay tied to your account.</p></div>
        </div>
      </section>

      <section className="container section-stack">
        <header className="section-heading"><div><span className="eyebrow">Recently listed</span><h2>Dogs waiting to meet someone</h2></div><Link className="text-link" to="/dogs">See all dogs <ArrowRight /></Link></header>
        <DogGrid dogs={dogs} isLoading={dogsQuery.isLoading} />
      </section>
    </>
  )
}
