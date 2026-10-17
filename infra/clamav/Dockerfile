# Antivirus de SongVault (ADR 0024) : l'image officielle de ClamAV, réglée pour nos fichiers.
# Signatures incluses à la construction, puis mises à jour par freshclam au démarrage et toutes les 2 heures.
FROM clamav/clamav:1.4

# Nombre de vérifications de freshclam par jour (lu par le script de démarrage de l'image)
ENV FRESHCLAM_CHECKS=12

# clamd refuse par défaut les flux de plus de 25 Mo ; nos WAV vont jusqu'à 2 Go (ADR 0012).
# Rechargement des signatures sans seconde copie en mémoire : les analyses attendent quelques secondes, la mémoire ne double pas.
RUN test -f /etc/clamav/clamd.conf && test -f /etc/clamav/freshclam.conf \
 && sed -i -E '/^#?[[:space:]]*(StreamMaxLength|MaxScanSize|MaxFileSize|ConcurrentDatabaseReload)[[:space:]]/d' /etc/clamav/clamd.conf \
 && printf '%s\n' 'StreamMaxLength 2100M' 'MaxScanSize 2000M' 'MaxFileSize 2000M' 'ConcurrentDatabaseReload no' >> /etc/clamav/clamd.conf \
 && sed -i -E '/^#?[[:space:]]*TestDatabases[[:space:]]/d' /etc/clamav/freshclam.conf \
 && printf '%s\n' 'TestDatabases no' >> /etc/clamav/freshclam.conf

EXPOSE 3310