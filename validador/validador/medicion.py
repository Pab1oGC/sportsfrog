"""Mide una imagen con OFIQ y devuelve las medidas al motor de reglas.

Solo mide. No decide nada: eso es de reglas.evaluar.

OFIQ no es thread-safe (ver el stub de python-ofiq), así que un Medidor
serializa el acceso con un lock. La API usa un solo Medidor por proceso.
"""

import threading

import numpy as np

from ofiq import OFIQ, FaceDetectionError, LandmarkError


class Medidor:
    def __init__(self, ofiq: OFIQ | None = None):
        self._ofiq = ofiq if ofiq is not None else OFIQ()
        self._lock = threading.Lock()

    def medir(self, rgb: np.ndarray) -> tuple[dict | None, bool]:
        """Devuelve (medidas, cara_detectada).

        Si OFIQ no encuentra una cara, o no puede ubicar sus puntos clave, la
        cara no se considera detectada y las medidas son None. Cualquier otro
        error sale hacia arriba: es un fallo del sistema, no de la foto.
        """
        try:
            with self._lock:
                unificado = self._ofiq.scalar_quality(rgb)
                vector = self._ofiq.vector_quality(rgb)
        except (FaceDetectionError, LandmarkError):
            return None, False

        medidas = {"UnifiedQualityScore": unificado, **vector}
        return medidas, True
