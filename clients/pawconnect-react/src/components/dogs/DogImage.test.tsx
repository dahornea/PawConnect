import { fireEvent, render, screen } from '@testing-library/react'
import { DogImage } from '@/components/dogs/DogImage'

describe('DogImage', () => {
  it('replaces a failed remote image with the clean PawConnect fallback', () => {
    render(<DogImage src="https://example.invalid/dog.jpg" alt="Buddy" />)
    fireEvent.error(screen.getByAltText('Buddy'))
    expect(screen.getByText('No photo available')).toBeInTheDocument()
    expect(screen.queryByAltText('Buddy')).not.toBeInTheDocument()
  })
})
