import { ApiError } from '@/shared/api/ApiError'

/**
 * Envoie le fichier par PUT à l'adresse reçue à l'annonce : le stockage lui-même, ou l'API en stockage local.
 * XMLHttpRequest plutôt que fetch : seul il donne la progression d'un envoi.
 */
export function putFile(
  url: string,
  headers: Record<string, string>,
  file: Blob,
  onProgress?: (ratio: number) => void,
): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest()
    xhr.open('PUT', url)
    for (const [name, value] of Object.entries(headers)) xhr.setRequestHeader(name, value)

    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) onProgress?.(event.loaded / event.total)
    }
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) resolve()
      else reject(new ApiError(xhr.status, { title: 'Envoi refusé', status: xhr.status, detail: detailOf(xhr) }))
    }
    xhr.onerror = () => reject(new ApiError(0, { title: 'Serveur injoignable', status: 0 }))
    xhr.send(file)
  })
}

// L'API répond en ProblemDetails (JSON) ; le stockage Azure en XML, sans message utile pour l'utilisateur
function detailOf(xhr: XMLHttpRequest): string | undefined {
  try {
    const body: unknown = JSON.parse(xhr.responseText)
    return typeof body === 'object' && body !== null && 'detail' in body && typeof body.detail === 'string'
      ? body.detail
      : undefined
  } catch {
    return undefined
  }
}