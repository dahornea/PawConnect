import { Component, type ErrorInfo, type ReactNode } from 'react'

interface AppErrorBoundaryProps {
  children: ReactNode
}

interface AppErrorBoundaryState {
  hasError: boolean
}

export class AppErrorBoundary extends Component<AppErrorBoundaryProps, AppErrorBoundaryState> {
  state: AppErrorBoundaryState = { hasError: false }

  static getDerivedStateFromError(): AppErrorBoundaryState {
    return { hasError: true }
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('The adopter portal could not render the current page.', error, errorInfo)
  }

  private reload = () => {
    window.location.reload()
  }

  render() {
    if (!this.state.hasError) return this.props.children

    return (
      <main className="container page-stack">
        <section className="state-panel state-panel--error" role="alert">
          <h1>This page could not be displayed</h1>
          <p>Refresh the page to try again. Your saved PawConnect data has not been changed.</p>
          <button className="button button--primary button--md" type="button" onClick={this.reload}>
            Reload page
          </button>
        </section>
      </main>
    )
  }
}
