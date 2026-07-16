import { useState, type ImgHTMLAttributes } from 'react'
import { DogImageFallback } from '@/components/ui/States'

export function DogImage({ src, alt, ...props }: ImgHTMLAttributes<HTMLImageElement>) {
  const [failedSrc, setFailedSrc] = useState<string>()
  if (!src || failedSrc === src) return <DogImageFallback />
  return <img src={src} alt={alt} onError={() => setFailedSrc(src)} {...props} />
}
