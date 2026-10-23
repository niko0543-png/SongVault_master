import { isReadable, type SongFile } from '@/features/files/types'

/** #30 : les fichiers Guitar Pro d'une version que l'on peut ouvrir (analyse antivirus passée ou désactivée). */
export function guitarProFiles(files: SongFile[]): SongFile[] {
  return files.filter((f) => f.fileType === 'Tablature' && isReadable(f))
}

/** Le fichier de A à opposer à celui de B : le même nom (sans la casse), sinon le premier. */
export function partnerFile(filesA: SongFile[], fileB: SongFile | undefined): SongFile | undefined {
  const name = fileB?.originalFileName.toLocaleLowerCase('fr')
  return filesA.find((f) => f.originalFileName.toLocaleLowerCase('fr') === name) ?? filesA[0]
}