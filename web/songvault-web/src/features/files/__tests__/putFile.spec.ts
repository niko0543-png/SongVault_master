import { afterEach, describe, expect, it, vi } from 'vitest'
import { putFile } from '../api/putFile'
import { ApiError } from '@/shared/api/ApiError'

/** Faux XMLHttpRequest : enregistre la requête, le test choisit la réponse. */
class FakeXhr {
  static last: FakeXhr
  method = ''
  url = ''
  headers: Record<string, string> = {}
  body: unknown
  status = 0
  responseText = ''
  upload: { onprogress: ((event: ProgressEvent) => void) | null } = { onprogress: null }
  onload: (() => void) | null = null
  onerror: (() => void) | null = null

  constructor() {
    FakeXhr.last = this
  }
  open(method: string, url: string) {
    this.method = method
    this.url = url
  }
  setRequestHeader(name: string, value: string) {
    this.headers[name] = value
  }
  send(body: unknown) {
    this.body = body
  }
  respond(status: number, responseText = '') {
    this.status = status
    this.responseText = responseText
    this.onload?.()
  }
}

describe('putFile', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('envoie le fichier par PUT avec les en-têtes demandés et suit la progression', async () => {
    vi.stubGlobal('XMLHttpRequest', FakeXhr)
    const file = new Blob(['ID3'])
    const progress: number[] = []

    const sent = putFile('https://stockage/b?sig=x', { 'x-ms-blob-type': 'BlockBlob' }, file, (r) => progress.push(r))
    const xhr = FakeXhr.last
    xhr.upload.onprogress?.({ lengthComputable: true, loaded: 1, total: 4 } as ProgressEvent)
    xhr.respond(201)
    await sent

    expect(xhr.method).toBe('PUT')
    expect(xhr.url).toBe('https://stockage/b?sig=x')
    expect(xhr.headers).toEqual({ 'x-ms-blob-type': 'BlockBlob' })
    expect(xhr.body).toBe(file)
    expect(progress).toEqual([0.25])
  })

  it("rejette avec le message de l'API quand elle refuse", async () => {
    vi.stubGlobal('XMLHttpRequest', FakeXhr)

    const sent = putFile('/api/bands/b/x', {}, new Blob(['a']))
    FakeXhr.last.respond(400, JSON.stringify({ status: 400, detail: 'La taille envoyée ne correspond pas.' }))

    await expect(sent).rejects.toEqual(new ApiError(400, { title: 'Envoi refusé', status: 400, detail: 'La taille envoyée ne correspond pas.' }))
  })

  it('rejette avec « Envoi refusé » quand le stockage répond en XML', async () => {
    vi.stubGlobal('XMLHttpRequest', FakeXhr)

    const sent = putFile('https://stockage/b?sig=x', {}, new Blob(['a']))
    FakeXhr.last.respond(403, '<Error><Code>AuthenticationFailed</Code></Error>')

    await expect(sent).rejects.toMatchObject({ status: 403, message: 'Envoi refusé' })
  })
})