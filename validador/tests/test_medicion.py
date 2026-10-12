"""Prueba contra OFIQ real. Solo corre dentro del contenedor, donde ofiq está instalado."""

import numpy as np
import pytest

pytest.importorskip("ofiq")

from validador.medicion import Medidor  # noqa: E402


@pytest.fixture(scope="module")
def medidor():
    return Medidor()


def test_imagen_sin_cara_no_detecta_cara(medidor):
    gris = np.full((500, 400, 3), 120, dtype=np.uint8)
    medidas, cara = medidor.medir(gris)
    assert cara is False
    assert medidas is None
