import { SearchX } from 'lucide-react'
import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return <div className="container standalone-state"><SearchX /><h1>Page not found</h1><p>The page may have moved, or the address is incomplete.</p><Link className="button button--primary button--md" to="/dogs">Browse dogs</Link></div>
}
