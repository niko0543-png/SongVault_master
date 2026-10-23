/// <reference lib="webworker" />
import type { PlainScore } from './plain'
import { diffScores } from './scoreDiff'

/** #30 : le calcul des différences, hors du fil de la page, pour qu'un long morceau ne la fige pas. */
self.onmessage = (event: MessageEvent<{ a: PlainScore; b: PlainScore }>) => {
  self.postMessage(diffScores(event.data.a, event.data.b))
}