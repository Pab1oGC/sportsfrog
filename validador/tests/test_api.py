import io

import numpy as np
import pytest
from fastapi.testclient import TestClient
from PIL import Image

from validador.api import crear_app
from validador.reglas import REGLAS, cargar_config


class MedidorFalso:
    """Stand-in de Medidor: devuelve lo que le digamos, sin cargar OFIQ."""

    def __init__(self, medidas=None, cara=True):
        self.medidas = medidas
        self.cara = cara
        self.llamadas = 0

    def medir(self, rgb):
        self.llamadas += 1
        if not self.cara:
            return None, False
        return dict(self.medidas), True


def medidas_buenas():
    return {r.medida: 100.0 for r in REGLAS}


def imagen_bytes(ancho=400, alto=500, formato="JPEG"):
    buf = io.BytesIO()
    Image.new("RGB", (ancho, alto), (120, 120, 120)).save(buf, format=formato)
    return buf.getvalue()


@pytest.fixture
def cliente():
    def construir(medidor=None):
        medidor = medidor or MedidorFalso(medidas_buenas())
        app = crear_app(medidor=medidor, config=cargar_config())
        return TestClient(app), medidor
    return construir


def test_salud_reporta_version(cliente):
    c, _ = cliente()
    with c:
        r = c.get("/salud")
    assert r.status_code == 200
    assert r.json()["version_reglas"] == cargar_config()["version_reglas"]
    assert r.json()["calibrado"] is False


def test_validar_foto_buena_devuelve_formato_del_plan(cliente):
    c, medidor = cliente()
    with c:
        r = c.post("/validar", files={"archivo": ("f.jpg", imagen_bytes(), "image/jpeg")})
    assert r.status_code == 200
    cuerpo = r.json()
    assert cuerpo["estado"] == "aprobada"
    assert cuerpo["no_verificado"]  # quedan reglas sin umbral
    assert set(cuerpo) >= {"estado", "motivos", "advertencias", "no_verificado",
                           "controles", "version_reglas", "calibrado"}
    assert medidor.llamadas == 1


def test_validar_rechaza_mascarilla(cliente):
    medidas = medidas_buenas()
    medidas["MouthOcclusionPrevention"] = 6.7
    c, _ = cliente(MedidorFalso(medidas))
    with c:
        r = c.post("/validar", files={"archivo": ("f.jpg", imagen_bytes(), "image/jpeg")})
    assert r.json()["estado"] == "rechazada"
    assert any("mascarilla" in m for m in r.json()["motivos"])


def test_sin_cara_rechaza(cliente):
    c, _ = cliente(MedidorFalso(cara=False))
    with c:
        r = c.post("/validar", files={"archivo": ("f.jpg", imagen_bytes(), "image/jpeg")})
    assert r.json()["estado"] == "rechazada"
    assert "No se pudo encontrar una cara" in r.json()["motivos"][0]


def test_archivo_que_no_es_imagen_responde_422(cliente):
    c, medidor = cliente()
    with c:
        r = c.post("/validar", files={"archivo": ("x.jpg", b"no soy una foto", "image/jpeg")})
    assert r.status_code == 422
    assert "JPG o PNG" in r.json()["detail"]
    assert medidor.llamadas == 0  # no se gasta OFIQ en basura


def test_png_no_se_rechaza_por_formato(cliente):
    c, _ = cliente()
    with c:
        r = c.post("/validar", files={"archivo": ("f.png", imagen_bytes(formato="PNG"), "image/png")})
    assert r.status_code == 200


def test_gif_se_rechaza_por_formato(cliente):
    c, _ = cliente()
    with c:
        r = c.post("/validar", files={"archivo": ("f.gif", imagen_bytes(formato="GIF"), "image/gif")})
    assert r.status_code == 422


def test_foto_chica_se_rechaza_sin_gastar_ofiq(cliente):
    c, medidor = cliente()
    with c:
        r = c.post("/validar", files={"archivo": ("f.jpg", imagen_bytes(200, 250), "image/jpeg")})
    cuerpo = r.json()
    assert cuerpo["estado"] == "rechazada"
    assert "muy chica" in cuerpo["motivos"][0]
    assert medidor.llamadas == 0


def test_archivo_demasiado_grande_responde_413(cliente):
    c, _ = cliente()
    grande = b"\xff\xd8" + b"0" * (10 * 1024 * 1024 + 1)
    with c:
        r = c.post("/validar", files={"archivo": ("f.jpg", grande, "image/jpeg")})
    assert r.status_code == 413
