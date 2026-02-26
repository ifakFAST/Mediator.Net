import DOMPurify, { type Config } from 'dompurify'
import { marked } from 'marked'

const SANITIZE_OPTIONS: Config = {
  USE_PROFILES: { html: true },
}

export function sanitizeHtml(html: string): string {
  return DOMPurify.sanitize(html || '', SANITIZE_OPTIONS)
}

export function markdownToSafeHtml(markdown: string): string {
  const renderedHtml = marked.parse(markdown || '') as string
  return sanitizeHtml(renderedHtml)
}
