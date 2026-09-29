import { afterEach, describe, expect, it, vi } from 'vitest'
import { request } from '../httpClient'
import { ApiError } from '../ApiError'

function mockFetch(response: Response | Error) {
  const fn =
    response instanceof Error
      ? vi.fn<() => Promise<never>>().mockRejectedValue(response)
      : vi.fn<() => Promise<Response>>().mockResolvedValue(response)
  vi.stubGlobal('fetch', fn)
  return fn
}

const json = (body: unknown, status = 200, type = 'application/json') =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': type } })

describe('request', () => {
  afterEach(() => vi.unstubAllGlobals())

  it('préfixe /api et renvoie le JSON', async () => {
    const fetchMock = mockFetch(json({ id: '1', title: 'Nocturne' }))

    const result = await request<{ title: string }>('/songs/1')

    expect(result.title).toBe('Nocturne')
    expect(fetchMock).toHaveBeenCalledWith(
      '/api/songs/1',
      expect.objectContaining({ method: 'GET' }),
    )
  })

  it('envoie le corps en JSON', async () => {
    const fetchMock = mockFetch(json({ id: '1' }, 201))

    await request('/songs', { method: 'POST', body: { title: 'A' } })

    const init = fetchMock.mock.calls[0]![1] as RequestInit
    expect(init.body).toBe('{"title":"A"}')
    expect((init.headers as Record<string, string>)['Content-Type']).toBe('application/json')
  })

  it('transforme un ProblemDetails 404 en ApiError', async () => {
    mockFetch(
      json(
        { title: 'Ressource introuvable', status: 404, detail: "Song 'x' introuvable." },
        404,
        'application/problem+json',
      ),
    )

    const error = await request('/songs/x').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(404)
    expect((error as ApiError).message).toBe("Song 'x' introuvable.")
  })

  it('expose les erreurs de validation 400', async () => {
    mockFetch(
      json(
        { title: 'Validation', status: 400, errors: { Title: ['Le titre est obligatoire.'] } },
        400,
      ),
    )

    const error = (await request('/songs', { method: 'POST', body: {} }).catch(
      (e: unknown) => e,
    )) as ApiError

    expect(error.validationErrors.Title).toEqual(['Le titre est obligatoire.'])
  })

  it('renvoie undefined pour un 204', async () => {
    mockFetch(new Response(null, { status: 204 }))
    await expect(request<void>('/songs/1', { method: 'DELETE' })).resolves.toBeUndefined()
  })

  it('transforme une panne réseau en ApiError de statut 0', async () => {
    mockFetch(new TypeError('Failed to fetch'))
    await expect(request('/songs')).rejects.toMatchObject({ status: 0 })
  })
})
