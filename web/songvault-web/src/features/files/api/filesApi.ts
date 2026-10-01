const base = (songId: string, versionId: string) => `/songs/${songId}/versions/${versionId}/files`

/** URL du contenu, utilisable directement dans <a href> et <audio src> (même origine grâce au proxy). */
function contentUrl(songId: string, versionId: string, fileId: string, download = false) {
  return `/api${base(songId, versionId)}/${fileId}/content${download ? '?download=true' : ''}`
}

export const filesApi = { contentUrl }
