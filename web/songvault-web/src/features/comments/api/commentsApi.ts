import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { Comment, CommentTarget } from '../types'

const path = (t: CommentTarget, suffix = '') =>
  bandPath(`/songs/${t.songId}/versions/${t.versionId}/files/${t.fileId}/comments${suffix}`)

export const commentsApi = {
  list: (t: CommentTarget) => request<Comment[]>(path(t)),
  create: (t: CommentTarget, body: string, positionMs: number | null) =>
    request<Comment>(path(t), { method: 'POST', body: { body, positionMs } }),
  reply: (t: CommentTarget, commentId: string, body: string) =>
    request<Comment>(path(t, `/${commentId}/replies`), { method: 'POST', body: { body } }),
  edit: (t: CommentTarget, commentId: string, body: string) =>
    request<void>(path(t, `/${commentId}`), { method: 'PUT', body: { body } }),
  remove: (t: CommentTarget, commentId: string) =>
    request<void>(path(t, `/${commentId}`), { method: 'DELETE' }),
  resolve: (t: CommentTarget, commentId: string) =>
    request<void>(path(t, `/${commentId}/resolve`), { method: 'POST' }),
  reopen: (t: CommentTarget, commentId: string) =>
    request<void>(path(t, `/${commentId}/resolve`), { method: 'DELETE' }),
}