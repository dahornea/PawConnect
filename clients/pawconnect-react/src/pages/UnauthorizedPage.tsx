import { ShieldX } from 'lucide-react'
import { Link } from 'react-router-dom'

export function UnauthorizedPage() {
  return <div className="container standalone-state"><ShieldX /><h1>This portal is for adopter accounts</h1><p>Your PawConnect account does not have access to the React adopter portal.</p><Link className="button button--primary button--md" to="/">Return home</Link></div>
}
